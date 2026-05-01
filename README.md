# DKNet API Solution Template

[![NuGet](https://img.shields.io/nuget/v/DKNet.Minimal.Template.svg)](https://www.nuget.org/packages/DKNet.Minimal.Template)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A production-ready ASP.NET Core solution template built on **vertical slice architecture** combining:

- **DDD / CQRS** — strict layer boundaries (Api → AppServices → Domains → Infra)
- **.NET Aspire** orchestration (Redis + SQL Server)
- **EF Core** with auto model config and seeding (`UseAutoConfigModel`, `UseAutoDataSeeding`)
- **SlimMessageBus** — in-memory bus always wired; Azure Service Bus optional
- **FluentValidation**, **Mapster**, **OpenTelemetry**, **Azure App Configuration**, **JWT Bearer**
- **xUnit + Shouldly** test project scaffolded out-of-the-box

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
    └── MyCompany.MyService.App.Tests/     # xUnit + Shouldly test project
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

## Running the generated solution

```bash
# Restore
dotnet restore <Name>.sln

# Build
dotnet build <Name>.sln -c Release

# Run API only
dotnet run --project <Name>.ApiEndpoints/<Name>.Api

# Run with Aspire (Redis + SQL Server auto-provisioned via Docker)
dotnet run --project <Name>.ApiEndpoints/<Name>.AppHost

# Test
dotnet test <Name>.sln --settings coverage.runsettings --collect:"XPlat Code Coverage"
```

---

## EF Core Migrations

From inside `<Name>.ApiEndpoints/`:

```bash
# Add a new migration
./add-migration.sh <MigrationName>

# Remove the last migration
./remove-migration.sh <MigrationName>
```

---

## Adding a new feature (vertical slice)

Follow the **Profiles/V1** pattern already in the template:

1. **Domain entity** → `<Name>.Domains/Features/<Feature>/Entities/`
2. **EF Core mapping** → `<Name>.Infra/Features/<Feature>/Mappers/`
3. **Commands / Queries** → `<Name>.AppServices/<Feature>/V1/`
4. **Endpoint config** → `<Name>.Api/ApiEndpoints/<Feature>V1Endpoint.cs` (implement `IEndpointConfig`)

See [AGENTS.md](AGENTS.md) for the full architecture reference.

---

## AI assistant plugins

The repo ships a Claude Code plugin and a GitHub Copilot plugin that drive vertical-slice features end-to-end. Generated solutions include both folders (`.claude/`, `.claude-plugin/`, `.github/`), so your team gets the same agents, skills, and slash commands the template authors use.

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
| `/dknet-docs <Feature>` | Feature documentation under `docs/features/<feature>/` |

Subagents (`dknet-architect`, `dknet-implementer`, `dknet-bdd-engineer`) and seven domain skills back the commands; see `.claude/agents/` and `.claude/skills/`.

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

## License

MIT © [Steven Hoang](https://drunkcoding.net)
