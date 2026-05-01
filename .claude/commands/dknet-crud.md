---
description: Scaffold AppServices CRUD actions (Create/Update/Delete + DTO + spec + domain event) for an existing DKNet aggregate.
argument-hint: <Feature> <Entity> [version=V1]
allowed-tools: Read, Grep, Glob, Edit, Write, Bash, Task
---

You are scaffolding the **AppServices** layer for an aggregate that already has Domain + Infra wiring. Run `/dknet-entity` first if the entity does not exist yet.

## Inputs

`$ARGUMENTS` — feature slice folder (e.g. `CustomerProfiles`), entity (`CustomerProfile`), optional API version (defaults to `V1`).

## Required reading

1. `.claude/skills/dknet-appservices-actions/SKILL.md`
2. `src/ApiEndpoints/Minimal.AppServices/CustomerProfiles/V1/` (exemplar: `Actions/`, `Specs/`, `Events/`, `CustomerProfileDto.cs`)

## Steps

1. Use the `dknet-implementer` subagent to execute Step 5 of the implementer protocol:
   - Response DTO with `[GenerateDto]` + `[MapsFrom]`.
   - `Create<Entity>Request` (`RequestBase`, `IWitResponse<TDto>`, `[MapsFrom(typeof(<Entity>))]`) + validator + handler with duplicate-spec, `IRepositorySpec.AddAsync`, domain event, lazy result.
   - `Update<Entity>Request` + handler that fetches via spec and calls the entity's mutation method.
   - `Delete<Entity>Request` (`INoResponse`) + handler.
   - `SpecGet<Entity>` query specification.
   - `<Entity>CreatedEvent` record + in-memory event handler.
2. Build: `dotnet build src/DKNet.Templates.sln -c Release`. Fix any analyzer/warning errors before continuing.
3. Report files created and the next command (`/dknet-endpoint <Feature> <Entity>`).

## Constraints

- Handlers, validators, specs, event handlers MUST be `internal sealed`.
- Use `IRepositorySpec` — never introduce a custom repo interface.
- Auto-fields the client must not set: `[JsonIgnore]` on the request property.
- Create handler returns `mapper.ResultOf<TDto>(entity)` (lazy mapping).
- Do not modify endpoint files in this command.
