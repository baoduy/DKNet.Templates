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

| Parameter       | Default                        | Description                          |
|-----------------|-------------------------------|--------------------------------------|
| `-n`, `--name`  | `MyApp`                | Root namespace and folder name       |
| `--AuthorName`  | `Steven Hoang`                | Embedded in project metadata         |
| `--CompanyUrl`  | `https://drunkcoding.net`     | `<Company>` in project metadata      |
| `--RepositoryUrl` | `https://github.com/baoduy/DKNet` | `<RepositoryUrl>` in metadata  |

Example with all parameters:

```bash
dotnet new dknet-minimal -n Acme.OrderService \
  --AuthorName "Jane Smith" \
  --CompanyUrl "https://acme.com" \
  --RepositoryUrl "https://github.com/acme/order-service"
```

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

Subagents (`dknet-architect`, `dknet-implementer`, `dknet-bdd-engineer`) and nine domain skills back the commands — including `dknet-project-structure` (layer/folder orientation) and `dknet-ddd-principles` (aggregate boundaries, entity vs. value object, invariants, domain events); see `.claude/agents/` and `.claude/skills/`.

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

Status-counts endpoints (`GET .../status-counts`) no longer default to the last 30 days when no `from`/`to` bounds are supplied. An unbounded call now reports counts over the **entire history**. Explicit `from`/`to` bounds behave as before and are unaffected. Callers relying on the old 30-day default will see larger counts after upgrading — pass explicit `from`/`to` to preserve a bounded window.

## License

MIT © [Steven Hoang](https://drunkcoding.net)
