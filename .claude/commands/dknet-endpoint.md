---
description: Add a Minimal API IEndpointConfig that exposes CRUD actions for a DKNet feature with idempotency on POST.
argument-hint: <Feature> <Entity> [routePrefix] [version=V1]
allowed-tools: Read, Grep, Glob, Edit, Write, Bash, Task
---

You are wiring the **Api** layer for a feature whose AppServices CRUD already exists. Run `/dknet-crud` first if not.

## Inputs

`$ARGUMENTS` — feature folder, entity, optional kebab-case route prefix (defaults to entity plural lowercased), optional version.

## Required reading

1. `.claude/skills/dknet-endpoint-config/SKILL.md`
2. `src/ApiEndpoints/Minimal.Api/ApiEndpoints/CustomerProfileV1Endpoint.cs` (exemplar)
3. `src/ApiEndpoints/Minimal.Api/Configs/Endpoints/FluentEndpointMapperExtensions.cs` (the `MapGetList`/`MapGetById`/`MapPost`/`MapPut`/`MapDelete` helpers)

## Steps

1. Use the `dknet-implementer` subagent to execute Step 6 of the implementer protocol:
   - Create `<Feature>V<N>Endpoint : IEndpointConfig` with `Version` and `GroupEndpoint`.
   - Map the five fluent helpers in `Map(RouteGroupBuilder)`.
   - Call `.AddIdempotencyFilter()` on the `MapPost` chain.
   - Add `.WithDescription(...)` on each route.
2. Build the solution and confirm Scalar/OpenAPI lists the new endpoints (run the API briefly if practical).
3. Report files added and the next command (`/dknet-unit-tests <Feature> <Entity>` and `/dknet-bdd-test <Feature>`).

## Constraints

- Endpoint class MUST be `internal sealed` and implement `IEndpointConfig`.
- Do NOT register the endpoint manually — `EndpointConfig.CreateGroup` discovers it.
- Do NOT add controllers or attribute routing — this is Minimal API only.
- Idempotency filter is required on POST; without it, duplicate `X-Idempotency-Key` retries will not be deduped.
