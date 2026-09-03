# DKNet API Solution Template

[![NuGet](https://img.shields.io/nuget/v/DKNet.Minimal.Template.svg)](https://www.nuget.org/packages/DKNet.Minimal.Template)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A production-ready ASP.NET Core solution template built on **vertical slice architecture** combining:

- **DDD / CQRS** — strict layer boundaries (Api → AppServices → Domains → Infra)
- **.NET Aspire** orchestration (Redis + PostgreSQL)
- **EF Core** with auto model config and seeding (`UseAutoConfigModel`, `UseAutoDataSeeding`)
- **SlimMessageBus** — in-memory bus always wired; Azure Service Bus optional
- **FluentValidation**, **Mapster**, **OpenTelemetry**, **Azure App Configuration**, **JWT Bearer**
- **xUnit + Shouldly** unit/integration tests, plus **Reqnroll + NUnit** BDD tests — both scaffolded out-of-the-box

Full feature inventory: [`docs/template-features.md`](docs/template-features.md).

---

## AI Plugin

**Copilot**
```shell
copilot plugin marketplace add baoduy/DKNet.Templates
```

**Claude AI**
```shell
/plugin marketplace add baoduy/dknet.templates
/plugin install dknet-minimal
```

## Architecture

A generated solution is layered onion-style — each layer knows only about the layers inside it, never the layers outside it:

![Architecture diagram of a scaffolded solution: Minimal.AppHost orchestrates Redis and PostgreSQL and starts Minimal.Api, which dispatches through IMessageBus into Minimal.AppServices; AppServices calls Minimal.Domains aggregates, Minimal.Infra supplies the repository and event publisher to AppServices and the EF Core mapping and seeding to Domains, Minimal.Share is read by every layer, and the Minimal.App.Tests/Architecture project holds NetArchTest shape rules over the Api, AppServices, Infra and Domains projects.](docs/diagrams/templates-solution-layers.svg)


`Minimal.Domains` knows nothing above it — no reference to `AppServices`, `Infra`, or `Api`. `Minimal.AppServices`
depends only on `Domains` (plus `Share`); `Minimal.Infra` and `Minimal.Api` depend on `AppServices` and `Domains`,
never the reverse. `Minimal.Share` is the one cross-cutting exception — constants, options, and base types read by
every layer. `Minimal.AppHost` sits alongside `Minimal.Api` as the Aspire orchestrator and carries no business logic
of its own.

### Request flow

![Workflow diagram of the request pipeline: a request passes the edge middleware that applies forwarded headers, security response headers and CORS, then routing with the request bounds and the rate limiter, then authentication with its default-deny fallback, then the endpoint filters that populate FromClaim members and run FluentValidation, and finally the handler; opt-in routes take a detour through the idempotency filter, and each stage has its own short-circuit response — 413 for an oversized body, 429 or 504, 401 or 403, 400, and the 500 problem+json the global exception handler writes.](docs/diagrams/templates-request-pipeline.svg)

1. An HTTP request hits a `Minimal.Api` Minimal API endpoint (`IEndpointConfig`).
2. The request is validated (FluentValidation) and any `[FromClaim]` property is populated from the caller's claims — see [`docs/api-pipeline.md`](docs/api-pipeline.md).
3. The endpoint dispatches the request as a CQRS action/query in `Minimal.AppServices`, over the in-memory SlimMessageBus.
4. The action's handler calls a method on a `Minimal.Domains` entity, which mutates its own state and may raise a domain event.
5. `Minimal.Infra`'s `CoreDbContext.SaveChanges` persists the change.
6. EF Core hooks run around the save: `DataOwnerHook` stamps `CreatedBy`/`UpdatedBy` from the authenticated principal, then queued domain events are dispatched to their `Minimal.AppServices` handlers.
7. A dispatched event may be forwarded to an external bus (Azure Service Bus) when one is configured, in addition to the always-on in-memory dispatch.

See [`docs/api-pipeline.md`](docs/api-pipeline.md), [`docs/auditing-and-data-ownership.md`](docs/auditing-and-data-ownership.md), and [`docs/efcore-events.md`](docs/efcore-events.md) for the full detail behind each step.

### Enforced, not just documented

The layer boundary is held by the project-reference graph itself: every reference points inward, so
an outward one — `Minimal.Domains` to `Minimal.AppServices`, say — is a circular project reference
MSBuild refuses. On top of that, `Minimal.App.Tests/Architecture/` uses NetArchTest to assert the
shape rules that a compiler cannot: `internal sealed` on every endpoint, handler, validator, EF
config and seeder; an explicit max length on every mapped string; `HasConversion<string>()` on
every mapped enum; Npgsql-only package references; and secure defaults in the base
`appsettings.json`. Those fail the test run, not just review. Full list:
[`docs/extension-points.md`](docs/extension-points.md#boundaries-your-code-must-respect).

The HTTP surface ships hardened, not just documented: default-deny authorization, a status-only
public health probe with the detailed report behind auth, security response headers on every
response, stated request bounds (30 s / 1 MB / 10 s), forwarded caller information honoured only
from proxies you list, an enumerated CORS method and header allow-list, a non-root container image,
and a dependency audit that fails the build. Every one of them is on in the base `appsettings.json`
and relaxable for local work by configuration alone — the `Development` overlay already does it.
What each control enforces: [`docs/template-features.md`](docs/template-features.md#hardened-by-default).
Every key, default and effect: [`docs/configuration-reference.md`](docs/configuration-reference.md).

---

## Installation

Install from GitHub Packages:

```bash
dotnet nuget add source \
  --username <YOUR_GITHUB_USERNAME> \
  --password <YOUR_GITHUB_PAT_WITH_READ_PACKAGES> \
  --store-password-in-clear-text \
  --name github \
  "https://nuget.pkg.github.com/baoduy/index.json"
```

Install the latest template version directly from the GitHub feed:

```bash
dotnet new install DKNet.Minimal.Template --nuget-source "https://nuget.pkg.github.com/baoduy/index.json"
```

Or install a specific version:

```bash
dotnet new install DKNet.Minimal.Template::latest --nuget-source "https://nuget.pkg.github.com/baoduy/index.json"
```

---

## Usage

### Create a new solution

```bash
dotnet new dknet-minimal -n MyCompany.MyService
```

This generates a fully-wired solution under a `MyCompany.MyService/` folder:

```
MyCompany.MyService/
├── MyCompany.MyService.sln
├── global.json
├── Directory.Packages.props
├── coverage.runsettings
└── MyCompany.MyService.ApiEndpoints/
    ├── MyCompany.MyService.Api/           # Minimal API entry point
    ├── MyCompany.MyService.AppHost/       # .NET Aspire orchestration host
    ├── MyCompany.MyService.AppServices/   # Application/use-case layer (CQRS)
    ├── MyCompany.MyService.Domains/       # Domain entities, repos, events
    ├── MyCompany.MyService.Infra/         # EF Core, repos, event publisher
    ├── MyCompany.MyService.Share/         # Constants, options, shared types
    ├── MyCompany.MyService.App.Tests/     # xUnit + Shouldly unit/integration tests
    └── MyCompany.MyService.App.BDDTests/  # Reqnroll + NUnit BDD tests
```

### Available parameters

Every parameter is a literal text substitution: `dotnet new` finds the `replaces` token and
rewrites it throughout the generated tree.

| Parameter | Default | `replaces` token | What it rewrites |
|---|---|---|---|
| `-n`, `--name` | `MyMinimalApp` | `Minimal` (as `sourceName`) | Every file name, folder name, namespace and project reference; renames the `.sln` |
| `--Framework` | `net10.0` | `net10.0` | `<TargetFramework>` in `Directory.Packages.props`, and the `Aspire.Hosting` `HintPath` in the AppHost project. `net10.0` is the only choice offered |
| `--AuthorName` | `Steven Hoang` | `Steven Hoang` | `<Authors>` in `Directory.Packages.props` |
| `--CompanyUrl` | `https://drunkcoding.net` | `https://drunkcoding.net` | `<Company>` in `Directory.Packages.props` |
| `--RepositoryUrl` | `https://github.com/baoduy/DKNet` | `https://github.com/baoduy/DKNet` | `<PackageProjectUrl>` and `<RepositoryUrl>` in `Directory.Packages.props` |
| `--TenantId` | `00000000-0000-0000-0000-000000000000` | the same GUID | The bearer scheme's `MetadataAddress` and `ValidIssuer` in `appsettings.json` |
| `--ApiAudience` | `api://your-api` | `api://your-api` | The single entry in the bearer scheme's `ValidAudiences` |

Full reference, including what each one means for a deployed service:
[`docs/template-usage.md`](docs/template-usage.md#scaffold-time-parameters).

Example with all parameters:

```bash
dotnet new dknet-minimal -n Acme.OrderService \
  --AuthorName "Jane Smith" \
  --CompanyUrl "https://acme.com" \
  --RepositoryUrl "https://github.com/acme/order-service" \
  --TenantId "11111111-2222-3333-4444-555555555555" \
  --ApiAudience "api://order-service"
```

> **`--TenantId` and `--ApiAudience` ship as placeholders, not working values.** Replace them —
> at scaffold time with the parameters above, or afterwards in
> `<Name>.Api/appsettings.json` under `Authentication:Schemes:Bearer` — before you turn on
> `FeatureManagement:RequireAuthorization`. Left as shipped, the OIDC metadata fetch runs against a
> tenant that does not exist, so the bearer scheme never obtains its signing keys, and any token
> presented is rejected on audience mismatch because `ValidAudiences` still contains only
> `api://your-api`.

---

## Running, testing, and migrating the generated solution

```bash
dotnet build <Name>.sln -c Release
dotnet test <Name>.sln --settings coverage.runsettings --collect:"XPlat Code Coverage"

dotnet run --project <Name>.ApiEndpoints/<Name>.Api       # API only
dotnet run --project <Name>.ApiEndpoints/<Name>.AppHost   # full stack via Aspire (Redis + PostgreSQL)
```

Full usage reference (parameters, migrations, packaging): [`docs/template-usage.md`](docs/template-usage.md).

---

## Adding a new feature (vertical slice)

Two worked patterns ship in the template — a fully hand-written slice
(`ManualSample`/`PurchaseOrder`) and one built from DKNet's declarative
event/CRUD-generation attributes (`AutomatedSample`/`Product`). See
[`docs/samples/manual-vs-automated.md`](docs/samples/manual-vs-automated.md) for which shape fits
your feature, then follow [`docs/ddd-implementation-guide.md`](docs/ddd-implementation-guide.md)
for the end-to-end steps — domain entity, EF Core mapping, application action, endpoint, and tests.

See [AGENTS.md](AGENTS.md) for the condensed architecture reference used by AI coding agents.

---

## Documentation

| Guide | Covers |
|---|---|
| [`docs/ddd-implementation-guide.md`](docs/ddd-implementation-guide.md) | Adding a vertical-slice feature, end to end — entity → EF mapping → domain event → action → endpoint → unit/BDD tests |
| [`docs/template-features.md`](docs/template-features.md) | Every capability the template wires up out of the box, and where to configure it |
| [`docs/template-usage.md`](docs/template-usage.md) | `dotnet new` parameters, run/test/migrate/pack commands |
| [`docs/samples/manual-vs-automated.md`](docs/samples/manual-vs-automated.md) | Layer-by-layer comparison of the two worked samples the guides above reference |
| [`docs/samples/manual-purchase-orders/`](docs/samples/manual-purchase-orders/) | Hand-written vertical slice — `PurchaseOrder` |
| [`docs/samples/automated-products/`](docs/samples/automated-products/) | Generator-driven vertical slice — `Product` |
| [`docs/dknet-packages.md`](docs/dknet-packages.md) | Every DKNet NuGet package this template wires up, and what it's for |
| [`docs/api-pipeline.md`](docs/api-pipeline.md) | The full request pipeline — routing, versioning, auth, validation, idempotency, rate limiting — in the order it runs |
| [`docs/auditing-and-data-ownership.md`](docs/auditing-and-data-ownership.md) | How `CreatedBy`/`UpdatedBy` get stamped, and why a caller can never forge them |
| [`docs/efcore-events.md`](docs/efcore-events.md) | Domain events — manual `AddEvent` vs. declarative `[RaisesEvent]`, and dispatch ordering after `SaveChanges` |
| [`docs/crud-attributes.md`](docs/crud-attributes.md) | How `[CrudCreate]`/`[CrudUpdate]`/`[GenerateDto]` build a full CRUD slice for the generator-driven sample |
| [`docs/slimbus-messaging.md`](docs/slimbus-messaging.md) | How SlimMessageBus is wired, and how to forward a domain event to an external broker |
| [`docs/querying-and-specifications.md`](docs/querying-and-specifications.md) | The read side — filtering, paging, and projection via `DKNet.EfCore.Specifications` |
| [`docs/generic-list-endpoint.md`](docs/generic-list-endpoint.md) | The filter/search/order/page contract every generated CRUD list route exposes for free |
| [`docs/configuration-reference.md`](docs/configuration-reference.md) | Every `appsettings` key a generated solution reads — meaning, default, effect, and the code path that reads it |
| [`docs/extension-points.md`](docs/extension-points.md) | Where your own code attaches — endpoints, validators, claims, rate limiting, persistence, domain services, test hosts |

> These guides are visible on GitHub but are **not** packaged into scaffolded solutions — the
> nuspec's file list doesn't ship `docs/`. If you need this content inside a generated solution,
> copy it manually or add it to the nuspec.

---

## AI assistant plugins

The repo ships a Claude Code plugin and a GitHub Copilot plugin that drive vertical-slice features end-to-end. Generated solutions include both folders (`.claude/`, `.claude-plugin/`, `.github/`), so your team gets the same agents, skills, and slash commands the template authors use.

> Previously the two sides had separate identities — `dknet-minimal` for Claude Code, `dknet-plugin` for the Copilot-side manifest. Both are now unified under a single `dknet-minimal` name and version.

### Claude Code

```text
/plugin marketplace add baoduy/dknet.templates
/plugin install dknet-minimal
```

Once installed, the following slash commands are available:

| Command | Purpose |
|---|---|
| `/dknet-feature <Feature> <Entity> [props…]` | Orchestrates a full vertical slice: plan → entity → CRUD → endpoint → tests → BDD → docs |
| `/dknet-entity <Feature> <Entity> [props…]` | Domain entity + EF mapper + migration |
| `/dknet-crud <Feature> <Entity>` | AppServices CRUD (DTO + Create/Update/Delete + spec + event) |
| `/dknet-endpoint <Feature> <Entity>` | Minimal API `IEndpointConfig` with idempotency on POST |
| `/dknet-unit-tests <Feature> <Entity>` | `ApiFixture` + `IMessageBus` integration tests |
| `/dknet-bdd-test <Feature> <Entity>` | Reqnroll + NUnit BDD scenarios |
| `/dknet-docs <Feature>` | Feature documentation under `docs/features/<feature>/` |

Subagents (`dknet-architect`, `dknet-implementer`, `dknet-bdd-engineer`) and ten domain skills back the commands — including `dknet-project-structure` (layer/folder orientation) and `dknet-ddd-principles` (aggregate boundaries, entity vs. value object, invariants, domain events); see `.claude/agents/` and `.claude/skills/`.

### GitHub Copilot

Copilot auto-discovers `.github/agents/` and `.github/skills/` when you open the repo in VS Code. See `.github/skills/CATALOG.md` for the full skill list.

### Embedded in generated solutions

Running `dotnet new dknet-minimal -n MyApp` copies both plugin folders into `MyApp/`. Your team gets the agents, skills, and commands without any extra install step.

---

## Packaging & Publishing

### Build the NuGet template pack

```bash
cd src
dotnet pack DKNet.Minimal.Template.csproj -c Release -o ./nupkgs
```

### Test locally before publishing

```bash
# Install from local pack
dotnet new install ./nupkgs/DKNet.Minimal.Template.1.0.0.nupkg

# Uninstall local pack
dotnet new uninstall DKNet.Minimal.Template
```

### Push to nuget.org

```bash
dotnet nuget push ./nupkgs/DKNet.Minimal.Template.1.0.0.nupkg \
  --api-key <YOUR_NUGET_API_KEY> \
  --source https://api.nuget.org/v3/index.json
```

> Get an API key at [nuget.org/account/apikeys](https://www.nuget.org/account/apikeys).

---

## Release Notes

### Breaking change — status-counts default window (DRK-521)

Status-counts endpoints (the `GET` route `MapGetStatusCounts<TEntity>` maps, at the `status` segment by default) no longer default to the last 30 days when no `from`/`to` bounds are supplied. An unbounded call now reports counts over the **entire history**. Explicit `from`/`to` bounds behave as before and are unaffected. Callers relying on the old 30-day default will see larger counts after upgrading — pass explicit `from`/`to` to preserve a bounded window.

## License

MIT © [Steven Hoang](https://drunkcoding.net)
