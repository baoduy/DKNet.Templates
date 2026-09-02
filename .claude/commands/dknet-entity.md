---
description: Scaffold a new domain aggregate (entity + owned types + EF Core mapper + migration) inside a DKNet.Minimal solution.
argument-hint: <Feature> <Entity> [mode=manual|auto] [props…] e.g. Orders Order mode=manual Number:string Total:decimal
allowed-tools: Read, Grep, Glob, Edit, Write, Bash, Task
---

You are scaffolding the **Domain + Infra** layers of a vertical slice. Stop after the migration is generated and the solution builds — `/dknet-crud` will continue from there.

## Inputs

`$ARGUMENTS` — feature folder (plural, PascalCase), aggregate name (singular, PascalCase), an optional
`mode=manual|auto` (defaults to `manual`), and an optional list of properties (`Name:Type`, append `?`
for nullable).

**The mode changes what this command writes onto the entity.** In `auto` the entity itself carries the
CRUD and event surface as attributes — skipping them here makes `/dknet-crud mode=auto` unreachable,
because the generator has nothing to read. If the mode was not supplied, apply
`.claude/skills/dknet-feature-lifecycle/SKILL.md` §1 to choose one and say which you chose.

## Required reading

1. `.claude/skills/dknet-domain-entity/SKILL.md`
2. `.claude/skills/dknet-efcore-config/SKILL.md`
3. The exemplar for the selected mode:
   - `manual` — `ApiEndpoints/Minimal.Domains/Features/ManualSample/Entities/PurchaseOrder.cs` (hand-written entity, raises its own event via `AddEvent(...)`; no declarative attribute)
   - `auto` — `ApiEndpoints/Minimal.Domains/Features/AutomatedSample/Entities/Product.cs` (class-level `[RaisesEvent]`, `[CrudCreate]` ctor, `[CrudUpdate]` and `[CrudAction]` methods; no `AddEvent` anywhere) plus `docs/crud-attributes.md`
4. `ApiEndpoints/Minimal.Infra/Features/ManualSample/Mappers/PurchaseOrderConfigs.cs` (exemplar mapper — hand-written in **both** modes; no generator produces this)

## Steps

1. Use the `dknet-implementer` subagent (via `Task`) to execute Steps 1–4 of the implementer protocol: domain entity, schema constant, owned types, EF Core mapper, optional sequence/seed data, then `dotnet ef migrations add <Name> -c CoreDbContext -p Minimal.Infra/Minimal.Infra.csproj` from `ApiEndpoints/`.

   Mode-specific work on the entity:

   - **`manual`** — mutation methods enforce their own invariants and call `AddEvent(...)`; write the
     matching event record alongside the entity in `Entities/`.
   - **`auto`** — add these, and write nothing else that duplicates them:
     - class-level `[RaisesEvent(EventOperations.Created, Include = [...])]`, plus a
       `[RaisesEvent(EventOperations.Updated, nameof(Prop))]` per property whose change matters;
     - `[CrudCreate]` on the constructor with DataAnnotations on its parameters — and **no**
       acting-user parameter, which would make the acting user caller-settable (`DataOwnerHook`
       stamps it instead);
     - `[CrudUpdate]` on each mutation method;
     - `[CrudAction("segment")]` / `[CrudAction(Verb = CrudActionVerb.Put)]` for domain actions that
       mutate and return the DTO with no pre-condition to reject;
     - `IOwnedBy` if the aggregate needs row-level ownership isolation.

2. Run `dotnet build -c Release` and stop on first error.
3. **`auto` only** — confirm the composed event-record names against the compiled assembly; they have
   no source file to read, and a consumer wired to a guessed name compiles but never fires:
   ```bash
   strings ApiEndpoints/Minimal.Domains/bin/Release/net10.0/Minimal.Domains.dll | grep <Entity>
   ```
   `[RaisesEvent(EventOperations.Updated, nameof(Price))]` composes `ProductPriceUpdatedEvent`, **not**
   `ProductUpdatedEvent`.
4. Report:
   - mode used,
   - files created (relative paths),
   - migration name + tables/indexes,
   - for `auto`, the composed event-record names you verified,
   - the exact next command (`/dknet-crud <Feature> <Entity> mode=<mode>`).

## Constraints

- Do NOT touch `Minimal.AppServices` or `Minimal.Api` here — those belong to `/dknet-crud` and `/dknet-endpoint`.
- Entity must inherit `AggregateRoot`, properties `{ get; private set; }`, mutation only via methods — in **both** modes. `auto` changes how the CRUD surface is declared, not whether the domain model stays encapsulated.
- Mapper must be `internal sealed : DefaultEntityTypeConfiguration<T>` and call `base.Configure(builder)` first.
- In `auto`, never both declare `[RaisesEvent]` and call `AddEvent(...)` for the same change — the event fires twice.
