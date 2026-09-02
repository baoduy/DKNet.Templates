---
name: dknet-scaffold
description: Scaffold a new solution from the DKNet.Minimal template and get it running — dotnet new install/new, the six template parameters, what the generated tree looks like, first run with or without Aspire, and deleting the two shipped sample features. Use when starting a new DKNet.Minimal solution, or when orienting inside a freshly generated one.
---

# Scaffolding a DKNet.Minimal solution

This skill covers going from nothing to a green, running solution you can add business features to.
Once you are there, `dknet-feature-lifecycle` takes over.

> Project names below are shown as this solution's own names. If you are reading this *before*
> generating, they are the template's placeholder names and become yours at generation time — see
> the rename note under §1.

## 1. Install and generate

```bash
dotnet new install DKNet.Minimal.Template
dotnet new dknet-minimal -n <YourApp>
cd <YourApp>
```

`-n <YourApp>` sets the solution's root name. The template rewrites its own source name to yours in
**everything** — project names, folder names, namespaces, and the text inside the shipped AI guidance
files, including this one. Never hand-edit namespaces after generating; the rename is already
complete and consistent.

### Parameters

All six are optional and every one has a working default, so `-n <YourApp>` alone generates a solution
that builds. Supply the auth pair before your first deploy, not necessarily before your first run.

| Parameter | Default | What it sets |
|---|---|---|
| `--Framework` | `net10.0` | Target framework. `net10.0` is the only choice. |
| `--AuthorName` | `Steven Hoang` | NuGet package metadata `Authors`. |
| `--CompanyUrl` | `https://drunkcoding.net` | Project metadata `Company`. |
| `--RepositoryUrl` | `https://github.com/baoduy/DKNet` | Project metadata `RepositoryUrl`. |
| `--TenantId` | all-zero GUID | `Authentication:Schemes:Bearer:MetadataAddress` + `ValidIssuer`. |
| `--ApiAudience` | `api://your-api` | `Authentication:Schemes:Bearer:ValidAudiences`. |

```bash
dotnet new dknet-minimal -n <YourApp> \
  --AuthorName "Acme Corp" --CompanyUrl "https://acme.com" \
  --RepositoryUrl "https://github.com/acme/acme-api" \
  --TenantId "11111111-2222-3333-4444-555555555555" --ApiAudience "api://acme"
```

These are **replace-on-generate** values, not settings you can change by re-running the template. To
change one later, edit the file it landed in — `Directory.Packages.props` for the metadata,
`appsettings.json` for the auth pair.

## 2. What you get

```
<YourApp>/
├── Minimal.sln                  ← the solution; `dotnet build` from here needs no path
├── Directory.Packages.props     ← ALL NuGet versions, centrally managed
├── global.json                  ← SDK pinned to net10.0
├── coverage.runsettings
├── AGENTS.md                    ← full architecture reference
├── docs/                        ← guidance the skills link to
├── .claude/                     ← this plugin: skills, commands, agents
├── .github/                     ← the same guidance for Copilot
└── ApiEndpoints/
    ├── Minimal.Api/             ← endpoints, auth, OpenAPI
    ├── Minimal.AppServices/     ← CQRS handlers, validators, DTOs
    ├── Minimal.Domains/         ← entities, aggregate roots
    ├── Minimal.Infra/           ← EF Core, repos, event publisher
    ├── Minimal.Share/           ← shared constants/options
    ├── Minimal.AppHost/         ← Aspire orchestration
    ├── Minimal.App.Tests/       ← xUnit + Shouldly
    ├── Minimal.App.BDDTests/    ← Reqnroll + NUnit
    └── Minimal.App.TestSupport/
```

**Path convention used by every skill in this plugin:** paths are relative to the **solution root** —
the directory holding the `.sln`. In a generated solution that is the repo root, so
`ApiEndpoints/Minimal.Domains/...`. (In the DKNet.Templates repo itself the same tree is nested under
`src/`.)

**There are no `add-migration.sh` / `remove-migration.sh` scripts** in a generated solution — the
template pack excludes `*.sh`. Run the commands directly, from `ApiEndpoints/`:

```bash
cd ApiEndpoints
dotnet ef migrations add <Name>   -c CoreDbContext -p Minimal.Infra/Minimal.Infra.csproj
dotnet ef migrations remove       -c CoreDbContext -p Minimal.Infra/Minimal.Infra.csproj
```

## 3. Verify it is green before writing anything

Do this first. A failure here is an environment problem, not a code problem, and diagnosing it after
you have added a feature wastes time.

```bash
dotnet build -c Release          # expect: 0 Warning(s), 0 Error(s)
dotnet test --settings coverage.runsettings
```

Production projects run **warnings-as-errors** (`EnforceCodeStyleInBuild`, `AnalysisMode=All`). A new
warning fails the build; that is deliberate. Test projects opt out.

## 4. First run

Two ways, and the choice is about whether you want containers:

```bash
# Full stack — Redis + PostgreSQL via Aspire. Requires Docker running.
dotnet run --project ApiEndpoints/Minimal.AppHost

# API only — no containers, fastest inner loop.
dotnet run --project ApiEndpoints/Minimal.Api
```

The API-only path still needs a reachable database per `ConnectionStrings`; the Aspire path
provisions one for you. Start with the AppHost unless Docker is unavailable.

Azure Service Bus stays off until `ConnectionStrings:AzureBus` is non-empty — an in-memory bus handles
internal handlers either way, so nothing is required for local development.

## 5. The two sample features

The solution ships two complete worked examples of the same feature shape, built two different ways.
They exist to be read, then deleted:

| Feature folder | Entity | Flow |
|---|---|---|
| `ManualSample` | `PurchaseOrder` | Hand-written — enforced validation, idempotent create, `[FromClaim]` acting user |
| `AutomatedSample` | `Product` | Generator-driven — `[CrudCreate]`/`[CrudUpdate]`/`[CrudAction]`/`[RaisesEvent]` attributes |

Read `docs/samples/manual-vs-automated.md` before copying either — it is the layer-by-layer
comparison, including what the generated path gives up.

**Deleting them is the expected first step** once you have read them. Do not hand-delete: each sample
has out-of-folder touchpoints (`AutomatedSample` owns two `Produce`/`Consume` lines in
`ServiceBusSetup.cs`; `ManualSample` owns seed data). Use the command, which handles those plus the
drop migration:

```
/dknet-feature-remove ManualSample
/dknet-feature-remove AutomatedSample
```

Keep one of them until your first real feature works — a correct reference in-tree is worth more than
a tidy repo, and removal is one command whenever you want it.

## 6. Where to go next

| Goal | Use |
|---|---|
| Choose manual vs auto for a new aggregate | `dknet-feature-lifecycle` §1 |
| Build a business feature end-to-end | `/dknet-feature <Feature> <Entity> mode=manual\|auto` |
| Remove a feature | `/dknet-feature-remove <Feature>` |
| Layer boundaries and auto-discovery wiring | `dknet-project-structure`, `AGENTS.md` |
| BDD scenarios | `dknet-bdd-tests`, `/dknet-bdd-test` |

## Gotchas

- **Never add `Version=` to a `.csproj`.** All NuGet versions live in `Directory.Packages.props`
  (central package management); a version attribute on a `PackageReference` fails the build.
- **`FeatureManagement`, not `Features`,** is the config section backing `FeatureOptions`. Keys match
  property names one-for-one, and `Get<FeatureOptions>()` ignores unknown keys — a misspelled key
  silently no-ops instead of failing.
- **EF Core needs no `DbSet` declarations.** `UseAutoConfigModel` + `UseAutoDataSeeding` discover
  mappers and seeders by assembly scan. If you add seed data, wire `UseAutoDataSeeding` into **both**
  `InfraSetup.AddInfraServices` and `InfraMigration.MigrateDb` — missing the second is a real bug this
  template hit once, and seed rows silently never appear over HTTP.
- **Keep repos/services `sealed`** and under a `.Repos` or `.Services` namespace, or Scrutor's
  convention scan will not register them.
