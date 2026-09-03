---
description: Add a Minimal API IEndpointConfig that exposes CRUD actions for a DKNet feature with idempotency on POST.
argument-hint: <Feature> <Entity> [mode=manual|auto] [routePrefix] [version=V1]
allowed-tools: Read, Grep, Glob, Edit, Write, Bash, Task
---

You are wiring the **Api** layer for a feature whose AppServices CRUD already exists. Run `/dknet-crud` first if not.

## Inputs

`$ARGUMENTS` — feature folder, entity, optional `mode=manual|auto`, optional kebab-case route prefix
(defaults to entity plural lowercased), optional version.

The mode must match the one `/dknet-crud` ran in. `mode=manual` uses the hand-mapped steps below;
`mode=auto` skips straight to **Alternative: generated CRUD route** at the bottom — its single
`Map<Entity>Crud()` call replaces every step in the Steps section, and `.RequiredIdempotentKey()` does
not apply to it. If `mode=` was not supplied, detect it: a `[CrudCreate]` on the entity means `auto`.

## Required reading

1. `.claude/skills/dknet-endpoint-config/SKILL.md`
2. `ApiEndpoints/Minimal.Api/ApiEndpoints/ManualSample/PurchaseOrderV1Endpoint.cs` (exemplar — every route is a literal `group.MapPost/MapGet/MapPut/MapDelete(...)` call against the raw minimal-API surface, base route `/v1/purchase-orders`)
3. `docs/samples/manual-vs-automated.md` — read the "Endpoint registration & route mapping" and "Request idempotency" rows before choosing a mapping style

## Steps (`mode=manual`)

1. Use the `dknet-implementer` subagent to execute Step 6 of the implementer protocol:
   - Create `<Feature>V<N>Endpoint : IEndpointConfig` with `Version` and `GroupEndpoint`.
   - Map each route with a literal `group.MapPost(...)`/`MapGet(...)`/`MapPut(...)`/`MapDelete(...)` call, mirroring `PurchaseOrderV1Endpoint` (create, list, get-by-id, update, and any business action route such as `cancel`, plus delete).
   - Call `.RequiredIdempotentKey()` on the `MapPost` chain that creates the resource — clients then send `X-Idempotency-Key: {Guid}`; a replayed key returns the original response instead of creating a duplicate.
   - Add `.WithDescription(...)` on each route.
2. Build the solution and confirm Scalar/OpenAPI lists the new endpoints (run the API briefly if practical).
3. Report the mode used, files added, and the next command (`/dknet-unit-tests <Feature> <Entity> mode=<mode>` then `/dknet-bdd-test <Feature>`).

## Constraints

- Endpoint class MUST be `internal sealed` and implement `IEndpointConfig` — in **both** modes.
- The idempotency constraint below applies to `mode=manual` only; see the Alternative section for `auto`.
- Do NOT register the endpoint manually — `EndpointConfig.CreateGroup` discovers it.
- Do NOT add controllers or attribute routing — this is Minimal API only.
- `.RequiredIdempotentKey()` is required on the create route; without it, duplicate `X-Idempotency-Key` retries will not be deduped.

## Alternative: generated CRUD route

If the entity is plain CRUD with `[CrudCreate]`/`[CrudUpdate]`/`[GenerateDto]` already in place (see `Product`), skip hand-mapping entirely — the generator emits a `Map<Entity>Crud()` extension (namespace `Minimal.AppServices.Crud`) that wires GetById/GetList/Create/Update/Delete in one call. `ProductV1Endpoint` is the exemplar: 9 lines, `group.MapProductCrud()` plus a `.WithDescription`. This path does **not** get `.RequiredIdempotentKey()` and its DataAnnotations validation is not enforced (the .NET 10 validation source generator can't see through the generic `Map*<TRequest,TDto>` wrapper the generated route uses) — confirmed live: `POST /v1/products` with a negative price returns `201`. Only use this path when idempotency and enforced validation are not required.
