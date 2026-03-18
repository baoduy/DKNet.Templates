# DKNet SlimBus API Solution Template

[![NuGet](https://img.shields.io/nuget/v/DKNet.SlimBus.Template.svg)](https://www.nuget.org/packages/DKNet.SlimBus.Template)
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

Install from [nuget.org](https://www.nuget.org/packages/DKNet.SlimBus.Template):

```bash
dotnet new install DKNet.SlimBus.Template
```

Or install a specific version:

```bash
dotnet new install DKNet.SlimBus.Template::1.0.0
```

---

## Usage

### Create a new solution

```bash
dotnet new dknet-slimbus -n MyCompany.MyService
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
| `-n`, `--name`  | `MySlimBusApp`                | Root namespace and folder name       |
| `--AuthorName`  | `Steven Hoang`                | Embedded in project metadata         |
| `--CompanyUrl`  | `https://drunkcoding.net`     | `<Company>` in project metadata      |
| `--RepositoryUrl` | `https://github.com/baoduy/DKNet` | `<RepositoryUrl>` in metadata  |

Example with all parameters:

```bash
dotnet new dknet-slimbus -n Acme.OrderService \
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

## Packaging & Publishing

### Build the NuGet template pack

```bash
cd src
dotnet pack DKNet.SlimBus.Template.csproj -c Release -o ./nupkgs
```

### Test locally before publishing

```bash
# Install from local pack
dotnet new install ./nupkgs/DKNet.SlimBus.Template.1.0.0.nupkg

# Uninstall local pack
dotnet new uninstall DKNet.SlimBus.Template
```

### Push to nuget.org

```bash
dotnet nuget push ./nupkgs/DKNet.SlimBus.Template.1.0.0.nupkg \
  --api-key <YOUR_NUGET_API_KEY> \
  --source https://api.nuget.org/v3/index.json
```

> Get an API key at [nuget.org/account/apikeys](https://www.nuget.org/account/apikeys).

---

## License

MIT © [Steven Hoang](https://drunkcoding.net)
