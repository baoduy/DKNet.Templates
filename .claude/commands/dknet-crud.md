---
description: Scaffold AppServices CRUD (DTO + Create/Update/Delete + spec + event) for an existing DKNet aggregate — hand-written path, or point to the declarative CRUD-generation attributes for a plain entity.
argument-hint: <Feature> <Entity> [mode=manual|auto] [version=V1]
allowed-tools: Read, Grep, Glob, Edit, Write, Bash, Task
---

You are scaffolding the **AppServices** layer for an aggregate that already has Domain + Infra wiring. Run `/dknet-entity` first if the entity does not exist yet.

The `mode` argument selects the path. It must match the mode `/dknet-entity` ran in — `auto` requires
the entity to already carry `[CrudCreate]`/`[CrudUpdate]`/`[RaisesEvent]`, and there is nothing to
generate without them. If `mode=` was not supplied, detect it: grep the entity for `[CrudCreate]`;
present it means `auto`, absent means `manual`. Say which you detected.

Pick one per aggregate, don't mix them for the same entity:

- **Hand-written** (below) — every request/validator/handler/spec/DTO is a file you write. Needed whenever the aggregate has a business rule beyond DataAnnotations, a filtered query, idempotent writes, or a DTO that must hide fields.
- **Declarative CRUD generation** (further down) — `[CrudCreate]`/`[CrudUpdate]`/`[GenerateDto]` on the entity itself; `DKNet.SlimBus.Generators` produces the request/handler/route types for you. Only for genuinely plain CRUD — read the validation-gap caveat before choosing it.

`docs/samples/manual-vs-automated.md` is the authoritative layer-by-layer comparison between the two; use its "When to pick which" section to decide.

## Inputs

`$ARGUMENTS` — feature slice folder (e.g. `ManualSample`), entity (`PurchaseOrder`), optional
`mode=manual|auto`, optional API version (defaults to `V1`).

`.claude/skills/dknet-feature-lifecycle/SKILL.md` §1 is the decision procedure if the mode is still open.

## Path 1: Hand-written CRUD (`mode=manual`)

### Required reading

1. `.claude/skills/dknet-appservices-actions/SKILL.md`
2. `ApiEndpoints/Minimal.AppServices/ManualSample/V1/` (exemplar: `Actions/{Create,Update,Cancel,Delete}.cs`, `Specs/SpecGetPurchaseOrder.cs`, `Queries/{GetPurchaseOrderById,ListPurchaseOrders}.cs`, `Events/`, `PurchaseOrderDto.cs`)

### Steps

1. Use the `dknet-implementer` subagent to execute Step 5 of the implementer protocol:
   - Hand-written response DTO record — no `[GenerateDto]` — exposing exactly the fields the API should return (see `PurchaseOrderDto`).
   - `Create<Entity>Request` (`Fluents.Requests.IWitResponse<TDto>`, `[FromClaim(ClaimTypes.Name)] ByUser` for the acting user — never trust a payload value) + `AbstractValidator` + `internal sealed` handler that constructs the aggregate (which raises its own event via `AddEvent(...)` in its constructor) and calls `IRepositorySpec.AddAsync`, returning `mapper.ResultOf<TDto>(entity)` (lazy mapping).
   - `Update<Entity>Request` + handler that fetches via `SpecGet<Entity>` (404 via `NotFoundError` on miss) and calls the entity's mutation method.
   - `Delete<Entity>Request` (`Fluents.Requests.INoResponse`) + handler, same fetch-then-404 shape.
   - Any business-action request (e.g. `Cancel`) that rejects an invalid state transition with a `Result.Fail(...)` — mirror `CancelPurchaseOrderRequest`/`CancelPurchaseOrderCommandHandler` rejecting an already-cancelled order.
   - `SpecGet<Entity>` query specification — remember an unfiltered predicate builder needs at least one `.And()`/`.Or()` call to avoid compiling to `WHERE FALSE` (see `SpecGetPurchaseOrder`'s explicit `predicator.And(_ => true)` fallback).
   - Domain event record + in-memory event handler (only if the plan calls for one beyond what the constructor already raises).
2. Build: `dotnet build -c Release`. Fix any analyzer/warning errors before continuing.
3. Report the mode used, files created, and the next command (`/dknet-endpoint <Feature> <Entity> mode=manual`).

### Constraints

- Handlers, validators, specs, event handlers MUST be `internal sealed`.
- Use `IRepositorySpec` — never introduce a custom repo interface.
- Auto-fields the client must not set: `[FromClaim(...)]` on the request property, always overwritten by the endpoint from the authenticated caller.
- Create handler returns `mapper.ResultOf<TDto>(entity)` (lazy mapping).
- Do not modify endpoint files in this command.

## Path 2: Declarative CRUD generation (`mode=auto`)

For a genuinely plain CRUD entity, declare the CRUD surface on the entity instead of writing it. Exemplar: `Product` (`ApiEndpoints/Minimal.Domains/Features/AutomatedSample/Entities/Product.cs`, `ApiEndpoints/Minimal.AppServices/AutomatedSample/V1/ProductDto.cs`).

Most of the entity-side attributes are `/dknet-entity mode=auto`'s job. This command's own output at
the AppServices layer is small on purpose: **one `[GenerateDto]` line, plus any hand-written event
consumer.** If you find yourself writing a request, validator, or handler here, the mode is wrong.

### What you write

- `[RaisesEvent(EventOperations.Created, Include=[nameof(Id), ...])]` and/or `[RaisesEvent(EventOperations.Updated, nameof(Prop))]` at the class level — replaces a hand-written event record + `AddEvent(...)` call. The composed event-record name is `<Entity><NarrowingProps><Operation>Event` — e.g. `[RaisesEvent(EventOperations.Updated, nameof(Price))]` on `Product` composes `ProductPriceUpdatedEvent`, **not** `ProductUpdatedEvent`. Verify the exact composed name against the compiled assembly before wiring a consumer — there is no hand-written source file for it.
- `[CrudCreate]` on the entity's constructor — its parameter list becomes the generated create request's payload (DataAnnotations attributes on the parameters, e.g. `[Required][StringLength(150)]`, forward onto the generated request property).
- `[CrudUpdate]` on a mutation method — same forwarding for its parameter(s).
- `[CrudAction("segment")]` on a domain-action method — publishes a route at the by-id path plus a
  segment, returning `200` + the entity DTO. The default verb is POST and the default segment is
  derived from the method name; `[CrudAction(Verb = CrudActionVerb.Put)]` overrides the verb.
  `Product.Approve` (segment override) and `Product.Discontinue` (verb override) are the two shipped
  exemplars. A generated action has **nowhere to hang a pre-condition** — re-running it on an entity
  already in that state is a no-op `200`, not a `409`. If the action must reject an invalid state
  transition, it belongs in Path 1.
- `[GenerateDto(typeof(Entity))] public sealed partial record <Entity>Dto;` — one line, generates every audited property by default (`Exclude`/`Include` to narrow).

### What gets generated

`DKNet.SlimBus.Generators` produces (namespace `Minimal.AppServices.Crud`, not committed — inspect via `dotnet build` then `obj/Generated/DKNet.SlimBus.Generators/`):

- `Create<Entity>Request` / `Change<Member><Entity>Request` (named after the `[CrudUpdate]` method, e.g. `ChangePriceProductRequest`) + matching `internal sealed` handlers (`Create<Entity>Handler` / `Change<Member><Entity>Handler`) — no hand-written request/validator/handler exists for these.
- `<Entity>CrudEndpointExtensions.Map<Entity>Crud()` — GetById/GetList/Delete map straight to `DKNet.AspCore.Extensions`'s generic `MapGetById<TEntity,TKey,TDto>`/`MapGetList`/`MapDeleteById`; Create/Update use the generated handlers above.

### Constraints and the validation-gap caveat

- Do NOT hand-write a request/validator/handler for a `[CrudCreate]`/`[CrudUpdate]` member — that defeats the point of the generator; if a business rule needs enforcing, drop that one operation to a hand-written route instead (Path 1) rather than mixing generated and hand-written CRUD on the same entity.
- **Validation gap, confirmed live**: a `[Range]`/`[Required]` on a `[CrudCreate]`/`[CrudUpdate]` parameter *is* forwarded onto the generated request property, but it is **never enforced** under this template's endpoint-registration convention — the .NET 10 validation source generator only recognizes literal `Map*(string, Delegate)` calls, and the generated route goes through `DKNet.AspCore.Extensions`'s generic `MapPost<TRequest,TDto>` wrapper instead. `POST /v1/products` with a negative price returns `201`, not `400`. Do not present a DataAnnotations attribute on a generated request as enforced without checking the endpoint's mapping style.
- Acting-user attribution cannot use `[FromClaim]` on a generated request (the generator forwards only `System.ComponentModel.DataAnnotations` attributes) — it goes through `DKNet.EfCore.DataAuthorization`'s `DataOwnerHook` instead, wired once in `Minimal.Api/Configs/ServiceConfigs.cs`, not per-entity.
- No idempotency key support on the generated create route — see `/dknet-endpoint`'s "Alternative: generated CRUD route" section if the feature needs it.

### Steps

1. Add the attributes to the entity and the one-line `[GenerateDto]` DTO (Domain + AppServices layers together — there's no separate scaffolding step).
2. Build: `dotnet build -c Release`, then inspect `obj/Generated/DKNet.SlimBus.Generators/` to confirm the expected types were produced.
3. Report the mode used, the generated type names, and the next command
   (`/dknet-endpoint <Feature> <Entity> mode=auto`, which for this path is just `group.Map<Entity>Crud()`).

An empty `obj/Generated/DKNet.SlimBus.Generators/` after a green build means the attributes did not
take — STOP and fix the entity rather than hand-writing the missing pieces.
