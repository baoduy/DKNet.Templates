# Auditing and Data Ownership

How `CreatedBy`/`CreatedOn`/`UpdatedBy`/`UpdatedOn` get populated, and why a caller can never forge
them. Full API surface for the three packages behind this: `DKNet.EfCore.Abstractions`
([docs/EfCore/DKNet.EfCore.Abstractions.md](https://github.com/baoduy/DKNet/blob/dev/docs/EfCore/DKNet.EfCore.Abstractions.md)),
`DKNet.EfCore.DataAuthorization`
([docs/EfCore/DKNet.EfCore.DataAuthorization.md](https://github.com/baoduy/DKNet/blob/dev/docs/EfCore/DKNet.EfCore.DataAuthorization.md))
and `DKNet.EfCore.AuditLogs`
([docs/EfCore/DKNet.EfCore.AuditLogs.md](https://github.com/baoduy/DKNet/blob/dev/docs/EfCore/DKNet.EfCore.AuditLogs.md)).

## The audit fields

Every aggregate in this template ultimately derives from `AuditedEntity<TKey>`
(`DKNet.EfCore.Abstractions`), which exposes `IAuditedProperties`:

| Field | Meaning |
|---|---|
| `CreatedBy` / `CreatedOn` | Who created the row, and when |
| `UpdatedBy` / `UpdatedOn` | Who last modified the row, and when |

`Minimal.Domains/Share/DomainEntity.cs` extends `AuditedEntity<Guid>` and calls `SetCreatedBy` from
its constructor. `Minimal.Domains/Share/AggregateRoot.cs` extends `DomainEntity` and is the base
every feature aggregate (`PurchaseOrder`, `Product`) ultimately uses.

## Who is allowed to set them

**Invariant: audit and ownership values come from the authenticated principal at save time, never
from a request property.** A generated create request deliberately carries no acting-user
parameter.

Two mechanisms enforce this, depending on which sample you're looking at:

| | `Product` (automated) | `PurchaseOrder` (manual) |
|---|---|---|
| Mechanism | `DKNet.EfCore.AuditLogs`'s audit hook stamps `CreatedBy`/`UpdatedBy` on `SaveChanges`; `DKNet.EfCore.DataAuthorization`'s `DataOwnerHook` stamps `OwnedBy` | `[FromClaim]` populates a request property; the aggregate stamps itself |
| Where the acting user is read | `ICurrentUserProvider.GetCurrentUser()` — the tenant `OwnedBy` key is read separately, from `IDataOwnerProvider.GetOwnershipKey()` | `ClaimTypes.Name`, via the pipeline's contextual request population |
| Use it when | The entity is plain CRUD and an audit stamp is all you need | A domain method needs the acting user's identity as domain data, not just an audit stamp |

### Automated sample (`Product`) — the hook stamps it

`Minimal.Domains/Features/AutomatedSample/Entities/Product.cs`'s `[CrudCreate]` constructor takes
only `name` and `price`. There is no `createdBy`/`byUser` parameter to spoof, because the generated
create request's shape comes straight from that constructor's parameter list.

`CreatedBy`/`UpdatedBy` are stamped instead on save, from the signed-in user rather than from
anything the caller sent, wired via:

- `Minimal.AppServices/Share/IPrincipalProvider.cs` — extends `IDataOwnerProvider, ICurrentUserProvider`, adding
  `ProfileId`/`Email`/`UserName` read from the bearer token's claims.
- `Minimal.Api/Configs/Handlers/PrincipalProvider.cs` — the implementation. `GetOwnershipKey()`
  returns the caller's subject claim, never their name: the first non-empty of
  `http://schemas.microsoft.com/identity/claims/objectidentifier`, `oid`,
  `ClaimTypes.NameIdentifier`, `sub`. `SharedConsts.SystemAccount` is still the value this method
  returns when the caller is not authenticated, but with the built-in demonstration authentication
  provider on (`FeatureManagement:EnableDemoAuthentication`) there is no unauthenticated caller on a
  local request: the demonstration identity issues `ClaimTypes.NameIdentifier` as
  `SharedConsts.SystemAccount` directly, so `GetOwnershipKey()` resolves it from that subject claim
  instead of falling through to the unauthenticated branch — and row-level ownership filtering keeps
  working unchanged, because the value is the same either way. (`ProfileId` on the same class exposes that key parsed as a
  `Guid`, or `Guid.Empty` when it does not parse — it is a convenience for domain code, not what
  the hook stamps.) `GetCurrentUser()` returns that same subject claim today and is deliberately kept
  as its own method, so the acting user and the ownership key can diverge later without either hook
  changing.
- `Minimal.Api/Configs/ServiceConfigs.cs`: `.AddDataOwnerProvider<CoreDbContext, PrincipalProvider>()`
  registers the provider and wires `DataOwnerHook` onto `CoreDbContext`.
- `Minimal.Api/Configs/ServiceConfigs.cs`: `.AddCurrentUserProvider<CoreDbContext, PrincipalProvider>()`
  wires the `DKNet.EfCore.AuditLogs` hook onto the same context, which makes `GetCurrentUser()` — not
  the ownership key — the source of `CreatedBy`/`UpdatedBy`.

On `SaveChanges`, the two hooks split the entity between them:

1. The `DKNet.EfCore.AuditLogs` hook stamps `CreatedBy`/`CreatedOn` on every newly-added entity and
   `UpdatedBy`/`UpdatedOn` on every modified one, from `ICurrentUserProvider.GetCurrentUser()`. On a
   modified entity it first checks whether a domain method already called `SetUpdatedBy` explicitly
   for this change set, by comparing the property's current value against its EF Core
   `OriginalValue`. If a domain method already set it, both fields are left untouched rather than
   overwritten.
2. `DataOwnerHook` stamps ownership on every newly-added entity, from
   `IDataOwnerProvider.GetOwnershipKey()`. While a current-user provider is registered and returns a
   non-empty value it stamps `OwnedBy` only, leaving the audit fields to the hook above.
3. `DataOwnerHook` guards `IOwnedBy.OwnedBy` on a modified entity against reassignment to a key the
   current context doesn't hold, preventing cross-tenant transfer.

Because the payload has no acting-user field at all, there is nothing for a caller to smuggle in.
This is proven by `Minimal.App.Tests/Integration/AutomatedSample/V1/ProductSecurityTests.cs`:
`Create_ShouldStampCreatedByFromAuthenticatedCallersOwnershipKey`,
`Create_ShouldIgnoreAnyExtraActingUserFieldInThePayload`, and
`Update_ShouldStampUpdatedByFromAuthenticatedCallersOwnershipKey` all assert the stamped value
matches the authenticated caller, never an attacker-supplied one.

### Manual sample (`PurchaseOrder`) — `[FromClaim]` populates it, the aggregate stamps it itself

`PurchaseOrder` is hand-written end to end — no declarative attribute raises its event or stamps its
audit fields. Instead, `Minimal.AppServices/ManualSample/V1/Actions/Create.cs` declares:

```csharp
[FromClaim(ClaimTypes.Name)]
public string? ByUser { get; set; }
```

The endpoint pipeline's contextual request population (`DKNet.AspCore.Extensions`, see
[api-pipeline.md](./api-pipeline.md)) overwrites `ByUser` from the caller's `ClaimTypes.Name` claim
**before** validation and before the handler runs. Any value the caller sent in the body or query
string is discarded unconditionally, never trusted. The handler then passes `request.ByUser` into
the aggregate's constructor (`PurchaseOrder(customerName, amount, byUser)` → `base(byUser)` →
`SetCreatedBy`), and `PurchaseOrder.ChangeAmount` calls `SetUpdatedBy(userId)` itself on update.

Use this pattern instead of relying on the save-hook stamp when a domain method needs the acting user's
identity as domain data, not just an audit stamp. For example, to pass it into a further business
rule, or when the entity's own methods (not just `SaveChanges`) need to record who called them.

This is pinned by `Minimal.App.Tests/Integration/ManualSample/V1/PurchaseOrderSecurityTests.cs`:
`Create_ShouldAttributeCreatedByToAuthenticatedCaller_IgnoringPayloadByUser` and
`Update_ShouldAttributeUpdatedByToAuthenticatedCaller_IgnoringPayloadByUser` both send
`"byUser": "someone-else"` in the payload and assert the stored value is the authenticated caller's
name, never the spoofed one.

## Row-level ownership filtering

`Minimal.Infra/Contexts/CoreDbContext.cs` implements `IDataOwnerDbContext` directly, exposing
`AccessibleKeys` from the same `IDataOwnerProvider` used to stamp `OwnedBy`. `DKNet.EfCore.DataAuthorization`
uses this to apply a global query filter on any entity implementing `IOwnedBy`, so a caller only
ever sees rows whose ownership key matches their own. Filtering happens at the query level, not in
application code.

## Where in the save pipeline this runs

Both hooks run as part of EF Core's `SaveChanges`/`SaveChangesAsync` pipeline. Each is a
`BeforeSaveAsync` hook — `DataOwnerHook` registered via
`.AddDataOwnerProvider<CoreDbContext, PrincipalProvider>()`, the audit hook via
`.AddCurrentUserProvider<CoreDbContext, PrincipalProvider>()`. Neither depends on running before the
other: each decides what to stamp from `ICurrentUserProvider`'s own value, never from hook run order.
They run after change tracking has determined which entities are added or modified, and before the
`UPDATE`/`INSERT` statements are sent — so the stamped values are always part of the same
transaction as the data change itself.

## When the caller cannot be attributed

`CoreDbContext` overrides every `SaveChanges`/`SaveChangesAsync` entry point with
`EnsureOwnershipResolvable`. Before EF Core writes anything, it checks whether the ownership key is
empty while the change set contains a newly-added `IAuditedProperties` entity whose `CreatedBy`
column is non-nullable and still unset. If so it throws `OwnershipRequiredException` — a fail-closed
refusal rather than a row attributed to nobody.

The `StatusCode` branch of the `AddErrorResponses(...)` registration in
`Minimal.Api/Configs/FluentValidationConfig.cs` maps that exception to `403 Forbidden`, deliberately
separate from the generic `500` path.

No EF Core column or entity name leaks into that response either. The body is the service's standard
error body — `title` `"Error"`, `status`, `type`, `traceId` and an `errors` array — and outside
`Development` its single entry carries the fixed message
`"An unexpected error occurred. Quote the trace-id when reporting this."`, nothing the exception
carried. `type` names the **status** (`Forbidden`), never the exception's type name, so neither the
message nor the exception type reaches the caller. Shape and guarantees:
[`api-pipeline.md`](api-pipeline.md#error-responses).
