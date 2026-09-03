---
description: Retire a DKNet vertical-slice business feature end-to-end — deletes its folders across all six projects, cleans the out-of-folder touchpoints, and drops its tables via a new migration.
argument-hint: <Feature> [--dry-run] e.g. Orders
allowed-tools: Read, Grep, Glob, Edit, Write, Bash, TodoWrite
---

You are retiring a complete vertical slice. A feature is addressable by its folder name, but a
`rm -rf` is **not** a correct removal — schema constants, broker topology, feature flags, docs links,
and the database tables all live outside the feature folders.

## Inputs

`$ARGUMENTS` — the feature folder name (PascalCase, as it appears under
`Minimal.Domains/Features/`), plus optional `--dry-run`.

If no feature is named, list the candidates and stop:

```bash
ls ApiEndpoints/Minimal.Domains/Features/
```

## Required reading

1. `.claude/skills/dknet-feature-lifecycle/SKILL.md` — §2 footprint, §3 touchpoints, §4 migration
   rules, §5 removal order. This command is the executable form of that skill; do not improvise a
   different order.

## Phase 0 — Confirm and inventory (always, even under `--dry-run`)

1. Resolve the feature: confirm `ApiEndpoints/Minimal.Domains/Features/<Feature>/` exists. If it
   does not, list the candidates and STOP — do not guess at a near-match.
2. Inventory every path that will be deleted, using the §2 footprint. Report actual matches only:
   ```bash
   find ApiEndpoints -path '*<Feature>*' -not -path '*/obj/*' -not -path '*/bin/*' | sort
   find docs -path '*<slug>*' | sort
   ```
3. Grep for references from **outside** the slice — this is what catches the touchpoints and any
   coupling the footprint table doesn't predict:
   ```bash
   grep -rn '<Feature>\|<Entity>' ApiEndpoints --include=*.cs --include=*.json \
     -l | grep -v '<Feature>' | sort -u
   ```
   Inspect each hit. Anything in a *different* feature's folder is real coupling — surface it and
   STOP; the user decides whether to break it or keep the feature.
4. Print the inventory: files to delete (grouped by layer), touchpoints to edit, migration decision
   (§4 branch), and the exact table names that will be dropped.
5. **STOP and ask the user to confirm** before deleting anything. Under `--dry-run`, stop here
   permanently and report.

## Phase 1 — Delete, in dependent-first order

Do NOT reorder. Each step removes only things that nothing later in the list depends on, so an
intermediate build failure points at real coupling rather than at the ordering.

1. Docs — `docs/features/<slug>/` (or `docs/samples/<slug>/`).
2. BDD — `ApiEndpoints/Minimal.App.BDDTests/Features/<Plural>/` (both `*.feature` and the
   generated `*.feature.cs`, plus `Steps/`).
3. Tests — `Minimal.App.Tests/Unit/<Feature>/` and `Minimal.App.Tests/Integration/<Feature>/`.
   Also grep `Minimal.App.Tests/Architecture/` — a convention test may assert on this feature by name.
4. Api — `Minimal.Api/ApiEndpoints/<Feature>/`.
5. AppServices — `Minimal.AppServices/<Feature>/`.
6. Infra — `Minimal.Infra/Features/<Feature>/`.
7. Domains — `Minimal.Domains/Features/<Feature>/`.

## Phase 2 — Out-of-folder touchpoints

Work the §3 table. For each, edit surgically — remove the feature's lines, never the whole file:

1. `Minimal.Domains/Share/DomainSchemas.cs` — drop the feature's `const string`, if it added one.
   Leave `Migration` and `Profile` alone unless this feature owned one of them.
2. `Minimal.Infra/Extensions/ServiceBusSetup.cs` — drop the matching `azb.Produce<T>` /
   `azb.Consume<T>` pair and the now-unused `using`.
3. `Minimal.Share/Options/FeatureOptions.cs` + every `appsettings*.json` `FeatureManagement` section
   — drop the flag property and its JSON key together. A key with no property silently no-ops, so
   an orphan here fails no test; delete both halves or neither.
4. Docs cross-links — `docs/index.md` and any feature index that linked the slice.

## Phase 3 — Migration

Apply §4 of the lifecycle skill. State which branch you took and why:

- Feature's migration is the newest and unapplied → `cd ApiEndpoints && dotnet ef migrations remove -c CoreDbContext -p Minimal.Infra/Minimal.Infra.csproj`
- Otherwise → `cd ApiEndpoints && dotnet ef migrations add Drop<Feature> -c CoreDbContext -p Minimal.Infra/Minimal.Infra.csproj` and verify the generated `Up`
  contains the expected `DropTable` calls and nothing else.

Read the generated migration before moving on. A drop migration that also touches an unrelated table
means the model changed underneath you — STOP and report.

## Phase 4 — Verify

1. `dotnet build -c Release` — must be zero warnings (warnings-as-errors).
2. `dotnet test --settings coverage.runsettings` — all green.
3. Final residue sweep — must return nothing:
   ```bash
   grep -rn '<Feature>\|<Entity>' src docs --include=*.cs --include=*.json --include=*.md \
     --include=*.feature | grep -v '/obj/\|/bin/\|Migrations/'
   ```
   Hits under `Migrations/` are expected and correct — historical migrations keep the old table
   names and must not be edited.

## Report

1. Files/folders deleted, grouped by layer.
2. Touchpoints edited, with the specific lines removed.
3. Migration branch taken + migration name + tables dropped.
4. Build + test results.
5. Anything deliberately left behind, and why (historical migrations, shared types another feature
   uses).
6. Suggested commit title. Do not commit unless the user asks.

## Stop conditions

- Feature folder not found → list candidates, STOP.
- Another feature references this one → report the coupling, STOP.
- Build or tests fail after deletion and the cause is not an obvious leftover reference → STOP,
  summarize, ask. Do not start deleting unrelated code to make the build pass.
- Migration branch is ambiguous (unclear whether it was applied) → take the `add-migration` branch,
  say so, and continue. That branch is always safe.

## Constraints

- Never hand-delete an applied migration or edit migration history.
- Never delete `ManualSample` or `AutomatedSample` **in the template repo itself** — every skill and
  command cites them as exemplars. In a generated consumer solution, removing them is the expected
  first use of this command.
- Never widen scope to "cleanup" unrelated code you notice on the way through.
