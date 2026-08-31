# Template Usage Reference

How to install `DKNet.Minimal.Template`, scaffold a new solution from it, and run, test, migrate,
and publish that solution.

## Install

```bash
dotnet nuget add source \
  --username <YOUR_GITHUB_USERNAME> \
  --password <YOUR_GITHUB_PAT_WITH_READ_PACKAGES> \
  --store-password-in-clear-text \
  --name github \
  "https://nuget.pkg.github.com/baoduy/index.json"

dotnet new install DKNet.Minimal.Template --nuget-source "https://nuget.pkg.github.com/baoduy/index.json"
```

## Scaffold a solution

```bash
dotnet new dknet-minimal -n MyCompany.MyService
```

Parameters, straight from `src/.template.config/template.json`:

| Parameter | Default | Description |
|---|---|---|
| `-n`, `--name` | `MyMinimalApp` | Root namespace and folder name; also the template's `sourceName` substitution target (`Minimal` → your name throughout the generated tree). |
| `--Framework` | `net10.0` | Target framework. Only choice offered is `net10.0` — the parameter exists for forward compatibility, not to target an older TFM. |
| `--AuthorName` | `Steven Hoang` | Embedded in project metadata. |
| `--CompanyUrl` | `https://drunkcoding.net` | `<Company>` in project metadata. |
| `--RepositoryUrl` | `https://github.com/baoduy/DKNet` | `<RepositoryUrl>` in project metadata. |

```bash
dotnet new dknet-minimal -n Acme.OrderService \
  --AuthorName "Jane Smith" \
  --CompanyUrl "https://acme.com" \
  --RepositoryUrl "https://github.com/acme/order-service"
```

Generated layout:

```
MyCompany.MyService/
├── MyCompany.MyService.sln
├── global.json
├── Directory.Packages.props
├── coverage.runsettings
└── MyCompany.MyService.ApiEndpoints/
    ├── MyCompany.MyService.Api/           # Minimal API entry point
    ├── MyCompany.MyService.AppHost/       # .NET Aspire orchestration host
    ├── MyCompany.MyService.AppServices/   # CQRS actions, validators, event handlers
    ├── MyCompany.MyService.Domains/       # Entities, aggregate roots
    ├── MyCompany.MyService.Infra/         # EF Core, repositories, event publisher
    ├── MyCompany.MyService.Share/         # Constants, options, shared base types
    ├── MyCompany.MyService.App.Tests/     # xUnit + Shouldly unit/integration tests
    └── MyCompany.MyService.App.BDDTests/  # Reqnroll + NUnit BDD tests
```

The scaffold also copies `.claude/`, `.claude-plugin/`, `.github/`, `.specify/`, `AGENTS.md`, and
`SPEC_KIT.md` unchanged into every generated solution. These are the same AI-assistant
agents/skills/prompts and Spec-Kit workflow the template itself is built with. See
[`template-features.md`](./template-features.md) for details.

## Run

```bash
# API only, no containers
dotnet run --project <Name>.ApiEndpoints/<Name>.Api

# Full stack via Aspire (Redis + PostgreSQL auto-provisioned via Docker)
dotnet run --project <Name>.ApiEndpoints/<Name>.AppHost
```

## Test

```bash
dotnet test <Name>.sln --settings coverage.runsettings --collect:"XPlat Code Coverage"

# One project / one feature at a time
dotnet test <Name>.ApiEndpoints/<Name>.App.Tests --filter "FullyQualifiedName~<Feature>"
dotnet test <Name>.ApiEndpoints/<Name>.App.BDDTests --filter "TestCategory=<Feature>"
```

## EF Core migrations

Run these from inside `<Name>.ApiEndpoints/`. The scripts always target `CoreDbContext` in
`<Name>.Infra`.

```bash
./add-migration.sh <MigrationName>
./remove-migration.sh <MigrationName>
```

## Packaging & publishing (template maintainers)

```bash
cd src
dotnet pack DKNet.Minimal.Template.csproj -c Release -o ./nupkgs

# Test locally before publishing
dotnet new install ./nupkgs/DKNet.Minimal.Template.1.0.0.nupkg
dotnet new uninstall DKNet.Minimal.Template

# Publish
dotnet nuget push ./nupkgs/DKNet.Minimal.Template.1.0.0.nupkg \
  --api-key <YOUR_NUGET_API_KEY> \
  --source https://api.nuget.org/v3/index.json
```
