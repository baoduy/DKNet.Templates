---
name: dknet-feature-lifecycle
description: The add/remove lifecycle of a DKNet vertical-slice business feature — how to choose the manual vs automated flow, the exact file footprint a feature occupies across all six projects, and the out-of-folder touchpoints a delete must clean up. Use before /dknet-feature or /dknet-feature-remove, and whenever you need to enumerate or retire an existing feature.
---

# DKNet feature lifecycle

A **feature** in this template is one business capability delivered as a vertical slice. It is
addressable by a single PascalCase folder name (`<Feature>`) that appears literally in fixed roots
across six projects. That is what makes a feature addable and removable as a unit.

Two worked examples ship with the template and are the canonical reference for each flow:

| Flow | Exemplar feature | Exemplar entity |
|---|---|---|
| `manual` | `ManualSample` | `PurchaseOrder` |
| `auto` | `AutomatedSample` | `Product` |

## 1. Choosing the flow

Pick **one flow per aggregate** and do not mix them for the same entity. Mixing means some routes
are generated and some hand-mapped, and the two halves have different validation and idempotency
behavior — which is exactly the confusion this section exists to prevent.

### At a glance: which one should I copy?

**Default to `auto` (mirror `Product`).** Declare the operation on the entity —
`[CrudCreate]`/`[CrudUpdate]`/`[CrudAction]`/`[RaisesEvent]`/`[GenerateDto]` — and let
`DKNet.SlimBus.Generators` emit the request, handler and route. Start here for every new aggregate
and only step down when one of the reasons below actually applies to the feature in front of you.

**Step down to `manual` (mirror `PurchaseOrder`)** when the feature needs any of:

- Idempotent writes — safe client retries on `POST`. The generated create route has no
  `.RequiredIdempotentKey()` and none can be added to it.
- An attribute-declared (`DataAnnotations`) rule that must actually return `400`, not just be
  present on the generated request (see the gap below) — and that cannot be re-expressed as a
  FluentValidation rule.
- An operation that writes more than one aggregate in one transaction.
- A filtered or customized list/get query beyond the generic list route's `filter`/`search`/`orderBy`
  contract.
- The acting user must come from a claim on the request itself (`[FromClaim]`).

None of these apply? Then `auto` is the answer, even for an aggregate with real business rules.
Specifically, **none of the following is a reason to hand-write**:

- **A rule that must refuse an operation.** A FluentValidation validator written against a generated
  request still runs — `UseEndpointConfigs` applies `AddFluentValidationAutoValidation()` to every
  endpoint group, generated routes included — so it can read stored data through `IRepositorySpec`
  and refuse before the generated handler runs. `Product` ships two such rules
  (`CreateProductRequestValidator`, `DeleteProductRequestValidator`), both answering `409` via a
  `PreconditionCodes`-prefixed error code.
- **A DTO that must hide fields.** `[GenerateDto(..., Exclude = [...])]` narrows the shape, and
  `[SensitiveData]` travels from the entity onto the generated DTO.
- **A derived response value.** A Mapster `IRegister` reaches the generated route's response with no
  endpoint change (`ProductDto.GrossMargin`).
- **A domain event.** `[RaisesEvent]` raises it from the save hook; the consumer is hand-written
  either way.

| Trade-off | `manual` | `auto` |
|---|---|---|
| Attribute-declared validation (`[Range]`, `[Required]` on a `[CrudCreate]`/`[CrudUpdate]` param) | enforced — literal `Map*` calls, seen by the .NET 10 validation source generator | forwarded onto the generated request but **never enforced** — every generated route goes through `DKNet.AspCore.Extensions`'s generic `Map*<TRequest,TDto>` wrapper, which the source generator can't see into |
| Idempotency on create | `.RequiredIdempotentKey()` — a replayed `X-Idempotency-Key` returns the original response | none — the generated create route has no such call; a duplicate submit creates a duplicate row |
| DTO default shape | hand-picked fields only (`PurchaseOrderDto` exposes 5) | every audited property by default (`Id`, `CreatedBy/On`, `UpdatedBy/On` + entity props); narrow with `[GenerateDto(..., Exclude=[...])]` |
| Filtered list / custom get-by-id | a hand-written `Specification<T>` + query handler, any shape | the generic list route only (`filter`/`search`/`orderBy`/paging against the DTO); a bespoke predicate or a "check tenant ownership on get" still forces a hand-written route |
| Event naming | you choose the record name (`AddEvent(new PurchaseOrderCreatedEvent(...))`) | composed by convention: `<Entity><NarrowingProps><Operation>Event` — e.g. `[RaisesEvent(EventOperations.Updated, nameof(Price))]` on `Product` → `ProductPriceUpdatedEvent`, never `ProductUpdatedEvent`; verify the composed name in the compiled assembly, it has no source file |
| Acting-user attribution | `[FromClaim(ClaimTypes.Name)]` on the request — visible in the request shape | never a request property (the generator forwards only `DataAnnotations` attributes) — comes from `DKNet.EfCore.DataAuthorization`'s `DataOwnerHook`/audit hook instead, wired once for every entity on `CoreDbContext`; the forgery guarantee is identical, only the visibility differs |
| External broker (Azure Service Bus) | not wired for `PurchaseOrder` at all — a deliberate scope split | `azb.Produce<T>`/`azb.Consume<T>` in `ServiceBusSetup.cs` works on a declaratively-raised event exactly like a hand-raised one, but only when `EnableServiceBus` is on **and** `ConnectionStrings:AzureBus` is non-empty |

A mixed aggregate is a smell, but dropping **one** operation out of `auto` to a hand-written route
is legitimate when only that operation needs what the generator can't express —
`CrudMapOptions.Exclude("Discontinue")` plus a literal `MapPut` below it is exactly how `Product`
handles the one operation that writes two aggregates in one transaction. Say so explicitly when you
do it.

## 2. Feature footprint

Every path below is scoped by the feature folder name. `<Feature>` is PascalCase (`Orders`),
`<Plural>` is the BDD folder (usually the same). Root for the first six: `ApiEndpoints/`. Verified
against the real tree under both sample features.

| # | Path | `manual` | `auto` |
|---|---|---|---|
| 1 | `<YourApp>.Domains/Features/<Feature>/Entities/` | entity + hand-written event record(s) | entity only — `[RaisesEvent]`/`[CrudCreate]`/`[CrudUpdate]`/`[CrudAction]` carry the rest |
| 2 | `<YourApp>.Infra/Features/<Feature>/Mappers/` | `IEntityTypeConfiguration<T>` | same — **no generator produces this** |
| 3 | `<YourApp>.Infra/Features/<Feature>/StaticData/` | optional `DataSeedingConfiguration<T>` | optional (neither sample ships one for `Product`) |
| 4 | `<YourApp>.Infra/Features/<Feature>/ExternalEvents/` | not used by `PurchaseOrder` | optional broker consumer (`ProductCreatedNotificationHandler`) |
| 5 | `<YourApp>.AppServices/<Feature>/V1/Actions/` | command requests + handlers | only operations excluded from the generated map (e.g. `Discontinue.cs`) |
| 6 | `<YourApp>.AppServices/<Feature>/V1/Queries/` | hand-written read requests + handlers | custom read shapes the generic list can't express (e.g. a price-summary query) |
| 7 | `<YourApp>.AppServices/<Feature>/V1/Specs/` | `Specification<T>` filters | specs backing a validator or an excluded query |
| 8 | `<YourApp>.AppServices/<Feature>/V1/Events/` | domain event consumers | domain event consumers only — the generator raises, it does not consume |
| 9 | `<YourApp>.AppServices/<Feature>/V1/Validators/` | not needed — validation is enforced on literal routes | validators against a **generated** request that must refuse (`CreateXRequestValidator`, `DeleteXRequestValidator`) |
| 10 | `<YourApp>.AppServices/<Feature>/V1/<Feature>Dto.cs` (+ optional `<Feature>MappingRegister.cs`) | hand-written DTO record | one `[GenerateDto(typeof(Entity))] public sealed partial record` line; a Mapster `IRegister` only if a response value needs deriving from more than one column |
| 11 | `<YourApp>.Api/ApiEndpoints/<Feature>/<Entity>V1Endpoint.cs` | every route a literal `Map*` call | one `group.Map<Entity>Crud(o => …)` call, plus any excluded route mapped literally below it |
| 12 | `<YourApp>.App.Tests/Unit/<Feature>/` | entity/validator/spec tests | entity/handler tests |
| 13 | `<YourApp>.App.Tests/Integration/<Feature>/V1/` | result-level handler + security tests | same |
| 14 | `<YourApp>.App.BDDTests/Features/<Plural>/` | `*.feature` + `Steps/*.cs` | same |

Generated code for `auto` lands in `obj/Generated/DKNet.SlimBus.Generators/` (requests, handlers,
route registration) and `obj/Generated/DKNet.EfCore.DtoGenerator/` (the DTO's generated members) —
never committed, never deleted by hand, disappears with the attributes.

## 3. Out-of-folder touchpoints

Deleting the folders above leaves these behind. **Every removal must check all of them** — they are
the reason a feature delete is a command and not an `rm -rf`.

| Touchpoint | File | When it applies |
|---|---|---|
| Schema constant | `<YourApp>.Domains/Share/DomainSchemas.cs` | if the feature added its own `const string` (the samples instead use literal schema strings — `"manual_sample"`/`"sample"` — directly in their mapper's `ToTable` call) |
| Broker topology | `<YourApp>.Infra/Extensions/ServiceBusSetup.cs` | the `azb.Produce<T>`/`azb.Consume<T>` pair, e.g. `ProductCreatedEvent` on `product-tp`/`product-sub` |
| Feature flag | `<YourApp>.Share/Options/FeatureOptions.cs` + `FeatureManagement` section in every `appsettings*.json` | if the feature gated itself behind a flag |
| Auth scopes | the feature's own scopes class (e.g. `ProductScopes`) + its `foreach` registration in `Configs/Auth/AuthConfig.cs` | if the feature registered per-route scope policies |
| Precondition codes | `<YourApp>.AppServices/Share/PreconditionCodes.cs` | if a validator added a `precondition.`-prefixed code for this feature |
| Test-support visibility | `InternalsVisibleTo` in the owning project's `.csproj` (e.g. `<YourApp>.Api.csproj` grants `<YourApp>.App.TestSupport` and `<YourApp>.App.Tests` visibility onto `internal` scope classes) | if the feature's `internal` types need to be visible to test doubles |
| EF migration | `<YourApp>.Infra/Migrations/` | see §4 — never hand-delete an applied migration |

Enumerate existing features at any time — no registry file to keep in sync:

```bash
ls ApiEndpoints/<YourApp>.Domains/Features/
```

## 4. Migration rules on removal

The tables outlive the code. Decide by whether the feature's migration has been applied anywhere:

- **Not applied and it is the newest migration** —
  `dotnet ef migrations remove -c CoreDbContext -p <YourApp>.Infra/<YourApp>.Infra.csproj` (run from
  `ApiEndpoints/`).
- **Applied, or newer migrations sit on top of it** — do NOT touch the old migration. Delete the
  entity and mapper, then
  `dotnet ef migrations add Drop<Feature> -c CoreDbContext -p <YourApp>.Infra/<YourApp>.Infra.csproj`
  and let EF emit the drop. Rewriting applied history corrupts `__EFMigrationsHistory` for every
  environment already running it.

When in doubt, take the second branch — it is always correct, merely more verbose.

## 5. Order of operations

**Add** (each step builds green before the next): Domains → Infra → AppServices → Api → tests → BDD
→ docs. The orchestrator is `/dknet-feature <Feature> <Entity> [mode=manual|auto] [props…]`.

**Remove** (reverse — drop dependents before dependencies, so the build never sees a dangling
reference): docs → BDD → tests → Api → AppServices → Infra → Domains → touchpoints → migration. The
orchestrator is `/dknet-feature-remove <Feature>`.

Never remove `ManualSample` or `AutomatedSample` from the template repository itself — they are the
exemplars every skill and command cites. In a *generated* solution they are the first thing a
consumer deletes, and `/dknet-feature-remove` is how.
