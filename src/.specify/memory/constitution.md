# DKNet.Templates Constitution

## Core Principles

### I. Layered Boundaries Are Mandatory
All changes SHALL preserve the dependency direction `Api -> AppServices -> Domains`, with infrastructure wiring isolated in `SlimBus.Infra`. Domain entities own mutation behavior and SHALL NOT depend on API or infrastructure concerns.

### II. Vertical Slice Delivery Is the Default
New features SHALL follow the existing slice pattern across Domains, Infra, AppServices, and Api. Endpoint contracts implement `IEndpointConfig`, application logic uses command/query handlers, and persistence mapping lives in Infra mappers.

### III. Deterministic Startup and Configuration
Application startup SHALL preserve the canonical sequence in `Program.cs`: bind `FeatureOptions`, then logging, Azure App Configuration, validation, migration check, and final app service wiring. Feature flags SHALL be sourced from `FeatureManagement` and remain strongly typed.

### IV. Eventing and Bus Safety
Internal message handling SHALL always function through the in-memory child bus, and Azure Service Bus integration SHALL be optional and configuration-driven. Domain events SHALL be published through the infrastructure event publisher abstraction, not direct transport calls from application logic.

### V. Quality Gates Before Merge
Every change SHALL pass restore, build, and test gates on `src/DKNet.Templates.sln`. Production code SHALL respect centralized analyzer and warnings-as-errors policy. New behavior SHALL include tests at the appropriate level (unit and/or integration) before merge.

## Additional Constraints

- Target framework, SDK, and package versions SHALL be managed centrally via `src/global.json` and `src/Directory.Packages.props`.
- Infrastructure services and repositories intended for scanning SHALL be `sealed` and placed in `.Repos` or `.Services` namespaces.
- EF Core persistence SHALL use `UseAutoConfigModel` and `UseAutoDataSeeding` conventions already established in `InfraSetup`.
- API versioning SHALL continue through endpoint groups (`/v{version}` pattern) and endpoint discovery via `IEndpointConfig` scanning.
- `BaseCommand.ByUser` population SHALL continue through endpoint filters, not manual assignment inside handlers.

## Development Workflow, Review Process, Quality Gates

Minimum local gate for a feature branch:

1. `dotnet restore src/DKNet.Templates.sln`
2. `dotnet build src/DKNet.Templates.sln -c Release`
3. `dotnet test src/DKNet.Templates.sln --settings src/coverage.runsettings --collect:"XPlat Code Coverage"`

Review checklist:

- Architecture boundaries and slice structure are preserved.
- New endpoints are versioned and mapped through existing fluent endpoint helpers.
- Data access and mapping changes include migration/configuration updates when required.
- Domain events are emitted and consumed through approved bus abstractions.
- Tests cover happy path and at least one failure/validation path for new behavior.

## Governance

This constitution is the highest-priority engineering policy for this repository. In case of conflict, this file overrides informal guidance and older README content.

Amendment rules:

1. Propose amendments in a pull request that includes rationale, impacted sections, and migration steps for in-flight work.
2. Obtain approval from at least one maintainer before merge.
3. Update constitution version using semantic versioning: MAJOR for principle removals/redefinitions, MINOR for new principles/sections, PATCH for clarifications.
4. Keep `.specify` templates and related guidance aligned after amendment.

Compliance rules:

- Every PR review SHALL verify constitution compliance explicitly.
- Exceptions SHALL be documented in the PR with scope, risk, and follow-up remediation issue.

**Version**: 1.0.0 | **Ratified**: 2026-03-17 | **Last Amended**: 2026-03-17
