---
name: dknet-feature-lifecycle
description: The add/remove lifecycle of a DKNet vertical-slice business feature — how to choose the manual vs automated flow, the exact file footprint a feature occupies across all six projects, and the out-of-folder touchpoints a delete must clean up. Use before /dknet-feature or /dknet-feature-remove, and whenever you need to enumerate or retire an existing feature.
---

# DKNet feature lifecycle

A **feature** in this template is one business capability delivered as a vertical slice. It is
addressable by a single PascalCase folder name (`<Feature>`) that appears literally in seven fixed
roots. That is what makes a feature addable and removable as a unit.

Two worked examples ship with the template and are the canonical reference for each flow:

| Flow | Exemplar feature | Exemplar entity |
|---|---|---|
| `manual` | `ManualSample` | `PurchaseOrder` |
| `auto` | `AutomatedSample` | `Product` |

`docs/samples/manual-vs-automated.md` is the authoritative layer-by-layer comparison. Read it before
committing to a flow.

## 1. Choosing the flow

Pick **one flow per aggregate** and do not mix them for the same entity. Mixing means some routes are
generated and some hand-mapped, and the two halves have different validation and idempotency
behavior — which is exactly the confusion this table exists to prevent.

Choose `manual` if **any** of these is true:

- A business rule must be enforced beyond DataAnnotations (state transitions, cross-field rules,
  duplicate checks against the DB).
- Writes must be idempotent — `POST` needs `.RequiredIdempotentKey()` and an `X-Idempotency-Key`
  contract.
- Validation must actually return `400`. See the validation gap below.
- The response DTO must hide or reshape fields rather than expose every audited property.
- Queries need filtering/specs beyond get-by-id and the generic list.
- The acting user must come from a claim on the request (`[FromClaim]`).

Choose `auto` only when the aggregate is genuinely plain CRUD:

- Create/update/delete carry no rule a `[Required]`/`[StringLength]`/`[Range]` cannot express — and
  you accept those are advisory, not enforced.
- No idempotency requirement on create.
- The DTO can be every audited property (narrowed with `Exclude`/`Include` at most).
- Any extra operations fit `[CrudAction]`'s shape: mutate the aggregate, return `200` + DTO, with no
  pre-condition to reject.

**The validation gap — confirmed live, state it whenever recommending `auto`.** A `[Range]` on a
`[CrudCreate]` parameter *is* forwarded onto the generated request property but is **never
enforced**: the .NET 10 validation source generator only sees literal `Map*(string, Delegate)` calls,
and every generated route goes through `DKNet.AspCore.Extensions`' generic `Map*<TRequest,TDto>`
wrapper. `POST /v1/products` with a negative price returns `201`, not `400`.

**Acting-user attribution differs by flow.** `manual` uses `[FromClaim(ClaimTypes.Name)]` on the
request. `auto` cannot — the generator forwards only `System.ComponentModel.DataAnnotations`
attributes — so it relies on `DKNet.EfCore.DataAuthorization`'s `DataOwnerHook`, wired once in
`ServiceConfigs.AddAllAppServices` and applying to every entity on `CoreDbContext`.

A mixed aggregate is a smell, but dropping **one** operation out of `auto` to a hand-written route is
legitimate when only that operation has a rule. Say so explicitly when you do it.

## 2. Feature footprint

Every path below is scoped by the feature folder name. `<Feature>` is PascalCase (`Orders`),
`<Plural>` is the BDD folder (usually the same), `<slug>` is kebab-case (`orders`).

Root for the first six: `ApiEndpoints/`.

| # | Path | `manual` | `auto` |
|---|---|---|---|
| 1 | `Minimal.Domains/Features/<Feature>/Entities/` | entity + hand-written event record(s) | entity only — `[RaisesEvent]`/`[CrudCreate]`/`[CrudUpdate]`/`[CrudAction]` carry the rest |
| 2 | `Minimal.Infra/Features/<Feature>/Mappers/` | `IEntityTypeConfiguration<T>` | same — **no generator produces this** |
| 3 | `Minimal.Infra/Features/<Feature>/StaticData/` | optional `IDataSeedingConfiguration<T>` | optional |
| 4 | `Minimal.Infra/Features/<Feature>/ExternalEvents/` | optional broker consumer | optional broker consumer |
| 5 | `Minimal.AppServices/<Feature>/V1/` | `Actions/`, `Queries/`, `Specs/`, `Events/`, `<Feature>Dto.cs` | `<Feature>Dto.cs` (one `[GenerateDto]` line) + `Events/` consumers only |
| 6 | `Minimal.Api/ApiEndpoints/<Feature>/` | `<Entity>V1Endpoint.cs` — every route a literal `Map*` call | `<Entity>V1Endpoint.cs` — one `group.Map<Entity>Crud()` call |
| 7 | `Minimal.App.Tests/Unit/<Feature>/` | entity/validator/spec tests | entity/handler tests |
| 8 | `Minimal.App.Tests/Integration/<Feature>/V1/` | result-level handler + security tests | same |
| 9 | `Minimal.App.BDDTests/Features/<Plural>/` | `*.feature` + `Steps/*.cs` | same |
| 10 | `docs/features/<slug>/` *(repo root)* | README, architecture, data-model, api-reference | same |

Generated code for `auto` lands in `obj/Generated/DKNet.SlimBus.Generators/` — never committed,
never deleted by hand, disappears with the attributes.

## 3. Out-of-folder touchpoints

Deleting the ten folders above leaves these behind. **Every removal must check all five**; they are
the reason a feature delete is a command and not an `rm -rf`.

| Touchpoint | File | When it applies |
|---|---|---|
| Schema constant | `Minimal.Domains/Share/DomainSchemas.cs` | if the feature added its own `const string` |
| Broker topology | `Minimal.Infra/Extensions/ServiceBusSetup.cs` | the `azb.Produce<T>` / `azb.Consume<T>` pair, e.g. `ProductCreatedEvent` on `product-tp` |
| Feature flag | `Minimal.Share/Options/FeatureOptions.cs` + `appsettings*.json` `FeatureManagement` section | if the feature gated itself behind a flag |
| EF migration | `Minimal.Infra/Migrations/` | see §4 — never hand-delete an applied migration |
| Docs cross-links | `docs/index.md`, `docs/features/README.md` | any link pointing at the removed slice |

Enumerate existing features at any time — no registry file to keep in sync:

```bash
ls ApiEndpoints/Minimal.Domains/Features/
```

## 4. Migration rules on removal

The tables outlive the code. Decide by whether the feature's migration has been applied anywhere:

- **Not applied and it is the newest migration** — `cd ApiEndpoints && dotnet ef migrations remove -c CoreDbContext -p Minimal.Infra/Minimal.Infra.csproj`
- **Applied, or newer migrations sit on top of it** — do NOT touch the old migration. Delete the
  entity and mapper, then `dotnet ef migrations add Drop<Feature> -c CoreDbContext -p Minimal.Infra/Minimal.Infra.csproj` and let EF emit the drop. Rewriting
  applied history corrupts `__EFMigrationsHistory` for every environment already running it.

When in doubt, take the second branch — it is always correct, merely more verbose.

## 5. Order of operations

**Add** (each step builds green before the next): Domains → Infra → AppServices → Api → tests → BDD →
docs. The orchestrator is `/dknet-feature <Feature> <Entity> [mode=manual|auto] [props…]`.

**Remove** (reverse — drop dependents before dependencies, so the build never sees a dangling
reference): docs → BDD → tests → Api → AppServices → Infra → Domains → touchpoints → migration. The
orchestrator is `/dknet-feature-remove <Feature>`.

Never remove `ManualSample` or `AutomatedSample` from the template repo itself — they are the
exemplars every skill and command cites. In a *generated* solution they are the first thing a
consumer deletes, and `/dknet-feature-remove` is how.
