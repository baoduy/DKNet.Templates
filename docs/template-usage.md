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

## Scaffold-time parameters

Every parameter below is declared in `src/.template.config/template.json`. Each one carries a
`replaces` token: a literal string that `dotnet new` finds and rewrites throughout the generated
tree. Passing a parameter does not set a variable — it substitutes text, so the default value you
see in this repo is exactly the string that gets replaced.

| Parameter | Flag | Datatype | Default | `replaces` token | What it rewrites, and where |
|---|---|---|---|---|---|
| Name | `-n`, `--name` | string | `MyMinimalApp` | — (`sourceName`: `Minimal`) | Not a `replaces` symbol. `sourceName` rewrites the identifier `Minimal` in every file name, folder name, namespace and project reference, and renames `DKNet.Templates.sln` to `<Name>.sln`. |
| Framework | `--Framework` | choice (`net10.0`) | `net10.0` | `net10.0` | `<TargetFramework>` in `Directory.Packages.props`, and the `HintPath` on the `Aspire.Hosting` reference in `<Name>.AppHost.csproj`. Only `net10.0` is offered — the parameter exists so a later TFM can be added, not so you can target an older one. |
| AuthorName | `--AuthorName` | string | `Steven Hoang` | `Steven Hoang` | `<Authors>` in `Directory.Packages.props`, inherited by every project. |
| CompanyUrl | `--CompanyUrl` | string | `https://drunkcoding.net` | `https://drunkcoding.net` | `<Company>` in `Directory.Packages.props`. |
| RepositoryUrl | `--RepositoryUrl` | string | `https://github.com/baoduy/DKNet` | `https://github.com/baoduy/DKNet` | `<PackageProjectUrl>` and `<RepositoryUrl>` in `Directory.Packages.props`. |
| TenantId | `--TenantId` | string | `00000000-0000-0000-0000-000000000000` | `00000000-0000-0000-0000-000000000000` | `Authentication:Schemes:Bearer:MetadataAddress` and `:ValidIssuer` in `<Name>.Api/appsettings.json`, plus the `PlaceholderTenantGuid` constant in `<Name>.App.Tests/Architecture/AuthPlaceholderConfigTests.cs`. **Replace before enabling authorization.** |
| ApiAudience | `--ApiAudience` | string | `api://your-api` | `api://your-api` | The single entry in `Authentication:Schemes:Bearer:ValidAudiences` in `<Name>.Api/appsettings.json`. **Replace before enabling authorization.** |

```bash
dotnet new dknet-minimal -n Acme.OrderService \
  --AuthorName "Jane Smith" \
  --CompanyUrl "https://acme.com" \
  --RepositoryUrl "https://github.com/acme/order-service" \
  --TenantId "11111111-2222-3333-4444-555555555555" \
  --ApiAudience "api://order-service"
```

### What you must change before shipping

| What | Why | Where to change it after scaffolding |
|---|---|---|
| `--TenantId` | The shipped GUID is not a real tenant. The bearer scheme fetches its OIDC metadata from `https://login.microsoftonline.com/<TenantId>/v2.0/.well-known/openid-configuration`; against a non-existent tenant that fetch never yields signing keys, so no token can be validated. | `Authentication:Schemes:Bearer:MetadataAddress` and `:ValidIssuer` in `<Name>.Api/appsettings.json` |
| `--ApiAudience` | `ValidAudiences` deliberately lists only the API's own audience, so a token issued for any other resource is rejected. Left as `api://your-api`, every real token fails on audience mismatch. | `Authentication:Schemes:Bearer:ValidAudiences` in `<Name>.Api/appsettings.json` |
| `ConnectionStrings:AppDb` | Empty in the base file, and the API cannot open a `DbContext` without it. Supplied automatically only when you launch through the Aspire host. | `<Name>.Api/appsettings.json`, or a `ConnectionStrings__AppDb` environment variable |
| The `RateLimit` numbers | 100 requests and 20 concurrent per second is a placeholder ceiling, not a researched limit for your service. | `RateLimit` in `<Name>.Api/appsettings.json` |

Both auth parameters ship as placeholders on purpose — the template wires no identity provider of
its own. Enabling `FeatureManagement:RequireAuthorization` while they are still in place fails
twice over: the metadata document is fetched from a tenant that does not exist, so the scheme never
obtains its signing keys, and every presented token is rejected on audience mismatch.

> `--TenantId` also rewrites the `PlaceholderTenantGuid` constant inside
> `AuthPlaceholderConfigTests.cs`, because that constant is a literal copy of the token. The test
> keeps passing after scaffolding, but it now pins *your* tenant id rather than guarding a
> placeholder. Full configuration surface, key by key:
> [`configuration-reference.md`](./configuration-reference.md).

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

The scaffold also copies `AGENTS.md`, `.claude-plugin/`, `.specify/`, `.vscode/`, the
`agents`/`commands`/`skills` folders of `.claude/`, and the `agents`/`prompts`/`skills` folders of
`.github/` unchanged into every generated solution — the file list in
`src/DKNet.Minimal.Template.nuspec` is what decides. These are the same AI-assistant
agents/skills/prompts and Spec-Kit workflow the template itself is built with. Everything else in
`.github/` (workflows, hooks, instructions, `copilot-instructions.md`) belongs to this repository
and is **not** copied. See [`template-features.md`](./template-features.md) for details.

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
