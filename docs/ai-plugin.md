# AI plugin — Claude Code, GitHub Copilot, and any agent that reads SKILL.md

The `dknet-minimal` plugin lives in [`plugin/`](../plugin): `plugin/.claude-plugin/plugin.json` +
`plugin/skills/` + `plugin/agents/` (Claude Code). The repository root keeps only the entry points
that have to be found there — `.claude-plugin/marketplace.json` (pointing at `./plugin`),
`plugin.json` (GitHub Copilot) and `package.json` (npm). One set of
[Agent Skills](https://agentskills.io) teaches an AI coding agent how to build on this template: the
layer boundaries, every endpoint shape (hand-mapped, generated `Map<Entity>Crud()`, or the generic
route helpers), actions and queries, FluentValidation + Mapster DTOs, domain entities and static
seeding, SlimMessageBus internal events and Azure Service Bus forwarding, auth/ownership and every
`FeatureManagement` flag. Eight of the skills are slash workflows that scaffold a vertical slice end to
end; three Claude Code subagents (`dknet-architect`, `dknet-implementer`, `dknet-bdd-engineer`) back
`/dknet-feature`. Index of every skill: [`plugin/skills/README.md`](../plugin/skills/README.md).

`dotnet new dknet-minimal` does **not** copy the plugin into a generated solution (only `AGENTS.md`
ships) — install it into the generated repo with one of the install channels below.

The install channels — Claude Code, GitHub Copilot, the skills CLI and npm — are in the repository
[README](../README.md#ai-plugin), where `validate-plugin.sh` checks that every advertised channel
carries a non-empty instruction.

**Working on this repository**

```bash
claude --plugin-dir plugin     # loads plugin/skills/ and plugin/agents/ from the checkout
./validate-plugin.sh           # manifests, README install channels, skill portability
```

| Workflow | Purpose |
|---|---|
| `/dknet-feature <Feature> <Entity> [mode=manual\|auto] [props…]` | Full vertical slice: plan → entity → CRUD → endpoint → tests → BDD → docs |
| `/dknet-feature-remove <Feature>` | Retire a slice, its touchpoints and its tables (drop migration) |
| `/dknet-entity <Feature> <Entity> [mode=…] [props…]` | Domain entity + EF mapper + migration |
| `/dknet-crud <Feature> <Entity> [mode=…]` | AppServices CRUD (DTO + Create/Update/Delete + spec + event), or the generator attributes |
| `/dknet-endpoint <Feature> <Entity> [mode=…]` | Minimal API `IEndpointConfig` — hand-mapped with idempotency, or `Map<Entity>Crud()` |
| `/dknet-unit-tests <Feature> <Entity> [mode=…]` | xUnit + Shouldly tests through `ApiFixture` + `IMessageBus` |
| `/dknet-bdd-tests <Feature>` | Reqnroll + NUnit BDD scenarios |
| `/dknet-docs <Feature>` | Feature documentation under `docs/features/<feature>/` |

| Reference skill | Teaches |
|---|---|
| `dknet-project-structure` | Layers, folder footprint, auto-discovery, which skill for what — read first |
| `dknet-ddd-principles` | Aggregate boundaries, invariants, when an event is warranted |
| `dknet-feature-lifecycle` | Manual vs automated flow decision, feature footprint, removal rules |
| `dknet-scaffold` | `dotnet new dknet-minimal`, parameters, first run, deleting the samples, installing this plugin |
| `dknet-entity` | `AggregateRoot` entities, `[CrudCreate]`/`[CrudUpdate]`/`[CrudAction]`/`[RaisesEvent]`, `IOwnedBy`, `[SensitiveData]` |
| `dknet-efcore-config` | Mappers, static data seeding (both wiring paths), `CoreDbContext`, migrations |
| `dknet-crud` | Commands, FluentValidation (incl. 409 preconditions), handlers, generated requests |
| `dknet-queries-specs` | `Specification<T>`, query handlers, paging, the generic filter/search/order list route |
| `dknet-dto-mapping` | Hand-written vs `[GenerateDto]` DTOs, Mapster config and `IRegister` custom mapping, `ResultOf` |
| `dknet-endpoint` | `IEndpointConfig`; raw routes, `Map<Entity>Crud()` + `CrudMapOptions`, generic helpers; idempotency; scopes |
| `dknet-messaging-events` | SlimMessageBus in-memory bus, domain events (manual/declared), Azure Service Bus Produce/Consume |
| `dknet-auth-and-ownership` | JWT + scopes, demo auth, `[FromClaim]`, `DataOwnerHook`, row-level isolation, sensitive data |
| `dknet-platform-config` | Start-up order, every `FeatureManagement` flag and config section, Aspire, jobs, health, OpenAPI |
| `dknet-unit-tests` / `dknet-bdd-tests` | What each suite owns, fixtures, worked examples, the business-tests-only rule |
| `dknet-docs` | README + Mermaid architecture + API reference for a finished feature |
| `dknet-package-adoption` | Using DKNet packages in a project not created from the template |

**Release.** Versions stay `0.0.0` in git. `publish-nuget-github.yml` computes the release version from
tags on `main`, packs and publishes the NuGet template, creates the GitHub release, then stamps the same
version into `package.json` / `plugin.json` / `plugin/.claude-plugin/plugin.json` (`npm version` →
`scripts/sync-version.mjs`), validates the skills (`agentskills validate`, `claude plugin validate`,
`npx skills add --list`, `./validate-plugin.sh`) and publishes `@drunkcoding/dknet-implementation-skills` to npm
with OIDC trusted publishing. A version already on npm is skipped.

