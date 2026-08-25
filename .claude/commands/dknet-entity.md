---
description: Scaffold a new domain aggregate (entity + owned types + EF Core mapper + migration) inside a DKNet.Minimal solution.
argument-hint: <Feature> <Entity> [props…] e.g. Orders Order Number:string Total:decimal
allowed-tools: Read, Grep, Glob, Edit, Write, Bash, Task
---

You are scaffolding the **Domain + Infra** layers of a vertical slice. Stop after the migration is generated and the solution builds — `/dknet-crud` will continue from there.

## Inputs

`$ARGUMENTS` — feature folder (plural, PascalCase), aggregate name (singular, PascalCase), and an optional list of properties (`Name:Type`, append `?` for nullable).

## Required reading

1. `.claude/skills/dknet-domain-entity/SKILL.md`
2. `.claude/skills/dknet-efcore-config/SKILL.md`
3. `src/ApiEndpoints/Minimal.Domains/Features/ManualSample/Entities/PurchaseOrder.cs` (exemplar — hand-written entity, raises its own event via `AddEvent(...)` in the constructor; no declarative attribute)
4. `src/ApiEndpoints/Minimal.Infra/Features/ManualSample/Mappers/PurchaseOrderConfigs.cs` (exemplar mapper)

## Steps

1. Use the `dknet-implementer` subagent (via `Task`) to execute Steps 1–4 of the implementer protocol: domain entity, schema constant, owned types, EF Core mapper, optional sequence/seed data, then `./add-migration.sh <Name>` from `src/ApiEndpoints/`.
2. Run `dotnet build src/DKNet.Templates.sln -c Release` and stop on first error.
3. Report:
   - files created (relative paths),
   - migration name + tables/indexes,
   - the exact next command (`/dknet-crud <Feature> <Entity>`).

## Constraints

- Do NOT touch `Minimal.AppServices` or `Minimal.Api` here — those belong to `/dknet-crud` and `/dknet-endpoint`.
- Entity must inherit `AggregateRoot`, properties `{ get; private set; }`, mutation only via methods.
- Mapper must be `internal sealed : DefaultEntityTypeConfiguration<T>` and call `base.Configure(builder)` first.
