# Auditing and Data Ownership

How `CreatedBy`/`CreatedOn`/`UpdatedBy`/`UpdatedOn` get populated, and why a caller can never forge them.
Full API surface for the two packages behind this: `DKNet.EfCore.Abstractions`
([docs/EfCore/DKNet.EfCore.Abstractions.md](https://github.com/baoduy/DKNet/blob/dev/docs/EfCore/DKNet.EfCore.Abstractions.md))
and `DKNet.EfCore.DataAuthorization`
([docs/EfCore/DKNet.EfCore.DataAuthorization.md](https://github.com/baoduy/DKNet/blob/dev/docs/EfCore/DKNet.EfCore.DataAuthorization.md)).

## The audit fields

Every aggregate in this template ultimately derives from `AuditedEntity<TKey>` (`DKNet.EfCore.Abstractions`),
which exposes `IAuditedProperties`:

| Field | Meaning |
|---|---|
| `CreatedBy` / `CreatedOn` | Who created the row, and when |
| `UpdatedBy` / `UpdatedOn` | Who last modified the row, and when |

`Minimal.Domains/Share/DomainEntity.cs` extends `AuditedEntity<Guid>` and calls `SetCreatedBy` from its
constructor; `Minimal.Domains/Share/AggregateRoot.cs` extends `DomainEntity` and is the base every
feature aggregate (`PurchaseOrder`, `Product`) ultimately uses.

## Who is allowed to set them

**Invariant: audit and ownership values come from the authenticated principal at save time, never from
a request property.** A generated create request deliberately carries no acting-user parameter.

Two mechanisms enforce this, depending on which sample you're looking at.

### Automated sample (`Product`) — the hook stamps it

`Minimal.Domains/Features/AutomatedSample/Entities/Product.cs`'s `[CrudCreate]` constructor takes only
`name` and `price` — there is no `createdBy`/`byUser` parameter to spoof, because the generated create
request's shape comes straight from that constructor's parameter list.

`CreatedBy`/`UpdatedBy` are stamped instead by `DKNet.EfCore.DataAuthorization`'s `DataOwnerHook`, wired via:

- `Minimal.AppServices/Share/IPrincipalProvider.cs` — extends `IDataOwnerProvider`, adding
  `ProfileId`/`Email`/`UserName` read from the bearer token's claims.
- `Minimal.Api/Configs/Handlers/PrincipalProvider.cs` — the implementation. `GetOwnershipKey()` returns
  `ProfileId.ToString()`, read from the `ClaimTypes.NameIdentifier` claim — not the caller's name.
- `Minimal.Api/Configs/ServiceConfigs.cs`: `.AddDataOwnerProvider<CoreDbContext, PrincipalProvider>()`
  registers the provider and wires `DataOwnerHook` onto `CoreDbContext`.

On `SaveChanges`, `DataOwnerHook`:

1. Stamps `CreatedBy`/ownership on every newly-added entity from `IDataOwnerProvider.GetOwnershipKey()`.
2. On a modified entity, stamps `UpdatedBy`/`UpdatedOn` from the same ownership key — **unless** a domain
   method already called `SetUpdatedBy` explicitly for this change set (detected by comparing the
   property's current value against its EF Core `OriginalValue`). If a domain method already set it, the
   hook leaves both fields untouched rather than overwriting them.
3. Guards `IOwnedBy.OwnedBy` on a modified entity against reassignment to a key the current context
   doesn't hold, preventing cross-tenant transfer.

Because the payload has no acting-user field at all, there is nothing for a caller to smuggle in — proven
by `Minimal.App.Tests/Integration/AutomatedSample/V1/ProductSecurityTests.cs`:
`Create_ShouldStampCreatedByFromAuthenticatedCallersOwnershipKey`,
`Create_ShouldIgnoreAnyExtraActingUserFieldInThePayload`, and
`Update_ShouldStampUpdatedByFromAuthenticatedCallersOwnershipKey` all assert the stamped value matches the
authenticated caller, never an attacker-supplied one.

### Manual sample (`PurchaseOrder`) — `[FromClaim]` populates it, the aggregate stamps it itself

`PurchaseOrder` is hand-written end to end — no declarative attribute raises its event or stamps its
audit fields. Instead, `Minimal.AppServices/ManualSample/V1/Actions/Create.cs` declares:

```csharp
[FromClaim(ClaimTypes.Name)]
public string? ByUser { get; set; }
```

The endpoint pipeline's contextual request population (`DKNet.AspCore.Extensions`, see
[api-pipeline.md](./api-pipeline.md)) overwrites `ByUser` from the caller's `ClaimTypes.Name` claim
**before** validation and before the handler runs — any value the caller sent in the body or query string
is discarded unconditionally, never trusted. The handler then passes `request.ByUser` into the aggregate's
constructor (`PurchaseOrder(customerName, amount, byUser)` → `base(byUser)` → `SetCreatedBy`), and
`PurchaseOrder.ChangeAmount` calls `SetUpdatedBy(userId)` itself on update.

Use this pattern instead of relying on `DataOwnerHook` when a domain method needs the acting user's
identity as domain data — not just an audit stamp — for example to pass into a further business rule, or
when the entity's own methods (not just `SaveChanges`) need to record who called them.

This is pinned by `Minimal.App.Tests/Integration/ManualSample/V1/PurchaseOrderSecurityTests.cs`:
`Create_ShouldAttributeCreatedByToAuthenticatedCaller_IgnoringPayloadByUser` and
`Update_ShouldAttributeUpdatedByToAuthenticatedCaller_IgnoringPayloadByUser` both send
`"byUser": "someone-else"` in the payload and assert the stored value is the authenticated caller's name,
never the spoofed one.

## Row-level ownership filtering

`Minimal.Infra/Contexts/OwnedDataContext.cs` implements `IDataOwnerDbContext`, exposing `AccessibleKeys`
from the same `IDataOwnerProvider` used for stamping. `DKNet.EfCore.DataAuthorization` uses this to apply
a global query filter on any entity implementing `IOwnedBy`, so a caller only ever sees rows whose
ownership key matches their own — filtering happens at the query level, not in application code.

## Where in the save pipeline this runs

`DataOwnerHook` runs as part of EF Core's `SaveChanges`/`SaveChangesAsync` pipeline (a `BeforeSaveAsync`
hook registered via `.AddDataOwnerProvider<CoreDbContext, PrincipalProvider>()`), after change tracking
has determined which entities are added/modified and before the `UPDATE`/`INSERT` statements are sent —
so the stamped values are always part of the same transaction as the data change itself.
