---
name: dknet-scaffold
description: Scaffold a new solution from the DKNet.Minimal template and get it running — dotnet new install/new, the six template parameters, what the generated tree looks like, first build/run with or without Aspire, deleting the two shipped sample features, and installing this skill plugin into the generated repo. Use when starting a new DKNet.Minimal solution, or when orienting inside a freshly generated one.
---

# Scaffolding a DKNet.Minimal solution

This skill covers going from nothing to a green, running solution you can add business features to.
Once you are there, `dknet-feature-lifecycle` takes over.

## 1. Install and generate

```bash
dotnet nuget add source --username <GITHUB_USERNAME> --password <GITHUB_PAT_WITH_READ_PACKAGES> \
  --store-password-in-clear-text --name github "https://nuget.pkg.github.com/baoduy/index.json"
dotnet new install DKNet.Minimal.Template --nuget-source "https://nuget.pkg.github.com/baoduy/index.json"

dotnet new dknet-minimal -n <YourApp>
cd <YourApp>
```

`-n <YourApp>` sets the solution's root name. A dotted name works (`-n DKNet.Accounts` generates a
solution that builds and tests green with no hand edits) — the template's `sourceName` is `Minimal`,
and `dotnet new` rewrites that identifier to `<YourApp>` in **every** file name, folder name,
namespace, project reference, and the text inside the shipped `AGENTS.md`. Never hand-edit a
namespace after generating; the rename is already complete and consistent.

**What does NOT get renamed:** the `ApiEndpoints/` folder itself, and any repo path that never
contained the literal string `Minimal`. Only the project folders and files *inside* `ApiEndpoints/`
pick up the new name — confirmed by generating: `-n DKNet.Accounts` produces
`ApiEndpoints/DKNet.Accounts.Api/`, `ApiEndpoints/DKNet.Accounts.Domains/`, and so on, with
`ApiEndpoints/` unchanged, and the solution file renamed to `DKNet.Accounts.sln`.

### Parameters

All six are optional and every one has a working default, so `-n <YourApp>` alone generates a
solution that builds.

| Parameter | Default | What it sets |
|---|---|---|
| `--Framework` | `net10.0` | Target framework. `net10.0` is the only offered choice. |
| `--AuthorName` | `Steven Hoang` | `<Authors>` in `Directory.Packages.props`, inherited by every project. |
| `--CompanyUrl` | `https://drunkcoding.net` | `<Company>` in `Directory.Packages.props`. |
| `--RepositoryUrl` | `https://github.com/baoduy/DKNet` | `<PackageProjectUrl>`/`<RepositoryUrl>` in `Directory.Packages.props`. |
| `--TenantId` | all-zero GUID | `Authentication:Schemes:Bearer:MetadataAddress` + `:ValidIssuer` in `appsettings.json`. |
| `--ApiAudience` | `api://your-api` | The single entry in `Authentication:Schemes:Bearer:ValidAudiences`. |

```bash
dotnet new dknet-minimal -n <YourApp> \
  --AuthorName "Jane Smith" --CompanyUrl "https://example.com" \
  --RepositoryUrl "https://github.com/example/order-service" \
  --TenantId "11111111-2222-3333-4444-555555555555" --ApiAudience "api://order-service"
```

**Replace `--TenantId` and `--ApiAudience` before enabling `FeatureManagement:RequireAuthorization`.**
Left as shipped, the OIDC metadata fetch runs against a tenant that does not exist and every token
is rejected on audience mismatch — see `dknet-auth-and-ownership` and `dknet-platform-config` for
the full flag interaction. These are replace-on-generate values, not settings you can change by
re-running the template; change them later by editing `Directory.Packages.props` (metadata) or
`appsettings.json` (the auth pair) directly.

## 2. What you get

```
<YourApp>/
├── <YourApp>.sln                        ← the solution; dotnet build from here needs no path
├── Directory.Packages.props             ← ALL NuGet versions, centrally managed
├── global.json                          ← SDK pinned to net10.0
├── coverage.runsettings
├── AGENTS.md                            ← architecture reference, rewritten to your project names
└── ApiEndpoints/                        ← this folder name does NOT change
    ├── <YourApp>.Api/                   ← endpoints, auth, OpenAPI
    ├── <YourApp>.AppServices/           ← CQRS handlers, validators, DTOs
    ├── <YourApp>.Domains/               ← entities, aggregate roots
    ├── <YourApp>.Infra/                 ← EF Core, repos, event publisher
    ├── <YourApp>.Share/                 ← shared constants/options
    ├── <YourApp>.AppHost/               ← Aspire orchestration
    ├── <YourApp>.App.Tests/             ← xUnit + Shouldly
    ├── <YourApp>.App.BDDTests/          ← Reqnroll + NUnit
    └── <YourApp>.App.TestSupport/
```

**Path convention:** every other skill in this plugin writes paths relative to the solution root
using the template's own placeholder names (`Minimal.Api`, `ApiEndpoints/Minimal.Domains/...`) —
substitute your own `<YourApp>.*` prefix mentally when working in a generated solution.

**No `.claude/`, `.github/`, or `docs/` reaches a generated solution.** Only `AGENTS.md`, the four
solution-level files above, and `ApiEndpoints/**` are packed (`DKNet.Minimal.Template.nuspec`) —
confirmed by generating and inspecting the output tree. Get this plugin's skills into a generated
repo through §6 below, not through `dotnet new`.

**There are no `*.sh` migration scripts** in a generated solution — the pack excludes them. Run the
commands directly, from `ApiEndpoints/`:

```bash
cd ApiEndpoints
dotnet ef migrations add <Name>   -c CoreDbContext -p <YourApp>.Infra/<YourApp>.Infra.csproj
dotnet ef migrations remove       -c CoreDbContext -p <YourApp>.Infra/<YourApp>.Infra.csproj
```

## 3. Verify it is green before writing anything

```bash
dotnet build -c Release          # expect: 0 Warning(s), 0 Error(s)
dotnet test --settings coverage.runsettings
```

Production projects run **warnings-as-errors** (`EnforceCodeStyleInBuild`, `AnalysisMode=All`). A
new warning fails the build; that is deliberate. Test projects opt out.

## 4. First run

```bash
# Full stack — Redis + PostgreSQL via Aspire. Requires Docker running.
dotnet run --project ApiEndpoints/<YourApp>.AppHost

# API only — no containers, fastest inner loop.
dotnet run --project ApiEndpoints/<YourApp>.Api
```

The API-only path still needs a reachable database per `ConnectionStrings:AppDb`; the Aspire path
provisions one and also seeds it with 10,000 generated `Product`/`PurchaseOrder` rows each (tune or
disable via `SampleData:RecordsPerEntity` in the `AppHost`'s own `appsettings.json` — see
`dknet-platform-config`). Start with the AppHost unless Docker is unavailable.

Development defaults (`appsettings.Development.json`, applied when `ASPNETCORE_ENVIRONMENT=Development`,
which `dotnet run` uses by default): `RequireAuthorization=false`, `EnableDemoAuthentication=true`
(every request authenticates as a fixed demo identity), `EnableSwagger=true` (`/docs`),
`RunDbMigrationWhenAppStart=true`, `EnableHttps`/`EnableRateLimit`/`EnableSecurityHeaders`/
`EnableForwardedHeaders`/`EnableRequestBounds` all relaxed to `false`. None of this applies to a
deployed service — the base `appsettings.json` (what Production runs with, since no
`appsettings.Production.json` ships) keeps every security flag on. Full table in
`dknet-platform-config`.

Azure Service Bus stays off until `ConnectionStrings:AzureBus` is non-empty **and**
`FeatureManagement:EnableServiceBus` is on — an in-memory bus handles internal handlers either way,
so nothing extra is required for local development.

## 5. The two sample features

The solution ships two complete worked examples of the same feature shape, built two different
ways. They exist to be read, then deleted:

| Feature folder | Entity | Flow |
|---|---|---|
| `ManualSample` | `PurchaseOrder` | Hand-written — enforced DataAnnotations validation, idempotent create, `[FromClaim]` acting user |
| `AutomatedSample` | `Product` | Generator-driven — `[CrudCreate]`/`[CrudUpdate]`/`[CrudAction]`/`[RaisesEvent]` attributes |

Read `dknet-feature-lifecycle` §1 before copying either — it has the full trade-off table, including
what the generated path gives up.

**Deleting them is the expected first step** once you have read them. Do not hand-delete: each
sample has out-of-folder touchpoints (`AutomatedSample` owns two `Produce`/`Consume` lines in
`ServiceBusSetup.cs` plus its own scopes class; `ManualSample` owns seed data). Use the command,
which handles those plus the drop migration:

```
/dknet-feature-remove ManualSample
/dknet-feature-remove AutomatedSample
```

Keep one of them until your first real feature works — a correct reference in-tree is worth more
than a tidy repo, and removal is one command whenever you want it.

## 6. Installing this plugin into the generated repo

The skills and workflow commands that guide feature work do not travel with `dotnet new` (§2). Add
them to the generated repo separately, whichever tool you work in:

```bash
# Claude Code
/plugin marketplace add baoduy/DKNet.Templates
/plugin install dknet-minimal@dknet-marketplace

# Any other agent (Codex, Cursor, Gemini CLI, ...)
npx skills add baoduy/DKNet.Templates
```

GitHub Copilot: install through `npx skills add baoduy/DKNet.Templates -a github-copilot` (writes `.agents/skills/`, which Copilot reads) or `npm i -D @drunkcoding/dknet-implementation-skills` + `npx skills experimental_sync`
once either directory exists in the cloned repo (e.g. after one of the two commands above has run
in that repo, or after cloning a repo that already committed them).

## 7. Where to go next

| Goal | Use |
|---|---|
| Choose manual vs auto for a new aggregate | `dknet-feature-lifecycle` §1 |
| Build a business feature end-to-end | `/dknet-feature <Feature> <Entity> mode=manual\|auto` |
| Remove a feature | `/dknet-feature-remove <Feature>` |
| Layer boundaries and auto-discovery wiring | `dknet-project-structure` |
| Start-up order, flags, config sections, jobs, Aspire, test hosts | `dknet-platform-config` |
| BDD scenarios | `dknet-bdd-tests`, `/dknet-bdd-tests` |

## Gotchas

- **Never add `Version=` to a `.csproj`.** All NuGet versions live in `Directory.Packages.props`
  (central package management); a version attribute on a `PackageReference` fails the build.
- **`FeatureManagement`, not `Features`,** is the config section backing `FeatureOptions`. Keys
  match property names one-for-one, and `Get<FeatureOptions>()` ignores unknown keys — a misspelled
  key silently no-ops instead of failing.
- **EF Core needs no `DbSet` declarations.** `UseAutoConfigModel` + `UseAutoDataSeeding` discover
  mappers and seeders by assembly scan. If you add seed data, wire `UseAutoDataSeeding` into **both**
  `InfraSetup.AddInfraServices` and `InfraMigration.MigrateDb` — missing the second is a real bug
  this template hit once, and seed rows silently never appear over HTTP.
- **Register every new Infra service explicitly** in `InfraSetup.AddInfraServices` (one
  `AddScoped<IService, Service>()` line) — there is no convention scan that finds it for you.
- **`--TenantId`/`--ApiAudience` are placeholders, not working values.** Enabling
  `RequireAuthorization` before replacing them fails every request on signature/audience mismatch,
  not just unauthenticated ones.
