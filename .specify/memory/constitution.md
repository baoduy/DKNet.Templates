# DKNet.Templates Constitution

## Core Principles

### I. Vertical Slice Architecture

Every feature is organized as a self-contained, independently deployable slice that spans all layers
(Api → AppServices → Domains → Infra). Each slice contains everything needed for that feature: endpoint
contracts, commands, domain entities, persistence mappings, and event handlers. This ensures low coupling,
high cohesion, and clear ownership boundaries. Slices are the atomic unit of feature delivery; a feature
is not complete until all layers reflect the slice structure (see `Profiles/V1` as the canonical example).

**Why this matters**: Reduces blast radius of changes, enables parallel development, simplifies onboarding.

### II. Strict Layer Boundaries

Layer separation is non-negotiable: `Api` orchestrates endpoints, `AppServices` handles commands and
workflows, `Domains` defines entities and business rules, `Infra` wires persistence and external services.
No business logic leaks into Api. No infra concerns bleed into Domains. Layer crossing violates this
principle and MUST trigger a code review rejection. Use dependency injection and interfaces to enforce
these boundaries at compile time.

**Why this matters**: Ensures testability, prevents infrastructure decisions from polluting business logic.

### III. Class-First Domain Design (NON-NEGOTIABLE)

Domain entities are classes with encapsulated state and mutation methods—never anemic data classes.
Business rules live as methods on entities (`Update()`, `Validate()`, etc.), not in procedural services.
Commands are `BaseCommand` derivatives mapped to entity methods. This pattern ensures the domain model
accurately reflects real-world rules and is self-documenting (per coding preferences: strict OOP design).

**Why this matters**: Domain becomes the single source of truth for business rules; easier to test and reason about.

### IV. EF Core Auto Configuration and Seeding

All EF model configuration is declarative and centralized using `UseAutoConfigModel` and automatic
mapper discovery from `SlimBus.Infra/Features/<Feature>/Mappers`. No fluent API scattered across the
codebase. Seeding uses `UseAutoDataSeeding` in `InfraSetup.AddInfraServices`. This approach keeps the
context clean and configuration patterns consistent.

**Why this matters**: Single point of control for schema, indexes, and relationships; easier to track migrations.

### V. Event-Driven Integration

Domain events are the primary integration mechanism. All cross-aggregate or cross-service communication
flows through domain events published via `EventPublisher.Publish()`. In-process events use the in-memory
bus (`ImMemory`); external events use Azure Service Bus (`AzureBus`) when configured. This ensures
loose coupling and provides an audit trail of all significant state changes.

**Why this matters**: Supports distributed system patterns and enables event sourcing/replay strategies.

### VI. Test-First Quality Discipline

All features MUST have unit and integration tests before production deployment. Tests follow xUnit +
Shouldly patterns. Coverage metrics are tracked via `src/coverage.runsettings`. Production code uses
warnings-as-errors in `Directory.Packages.props`. Tests should validate behavior, not implementation
details; they document the contract of each component.

**Why this matters**: Prevents regressions, provides living documentation, enables safe refactoring.

### VII. Code-Verified Patterns (Source of Truth)

The project maintains an AGENTS.md file documenting established patterns with code examples (e.g.,
`ProfileV1Endpoint` as the template for new endpoints). This file MUST be kept up-to-date whenever a
new pattern is discovered or documented. Code examples in AGENTS.md and actual codebase are the source
of truth; they supersede outdated README or wiki statements. When patterns need to evolve, update
AGENTS.md and add a migration guide for existing code.

**Why this matters**: Eliminates tribal knowledge; enables consistent onboarding and review standards.

## Development Constraints

### Technology Stack

- **Framework**: .NET 10.0 (pinned in `src/global.json` and `src/Directory.Packages.props`)
- **Persistence**: EF Core with SQL Server
- **Mapping**: Mapster (global configuration in `SlimBus.AppServices/AppSetup.cs`)
- **Validation**: FluentValidation
- **Messaging**: In-memory bus + Azure Service Bus (optional)
- **Hosting**: Optional Aspire orchestration (`SlimBus.AppHost`)

All packages are centralized in `Directory.Packages.props`; projects reference without version numbers.

### Feature Configuration

Feature flags are managed via `FeatureOptions` (JSON key: `FeatureManagement` in settings).
Must align with `SlimBus.Share/Options/FeatureOptions.cs`. Never hardcode feature state; always
bind and inject `IOptions<FeatureOptions>`.

### Naming and Structure Rules

- **Namespaces**: Follow folder structure exactly; no divergence between namespace and path.
- **Tests**: Live under `SlimBus.App.Tests/<Category>/<Feature>.cs` (Unit, Integration, Architecture).
- **Infra classes**: MUST be `sealed` and placed in `.Repos` or `.Services` subdirectories for Scrutor auto-registration.
- **Feature slices**: Mirror the `Profiles/V1` template structure across all layers.

## Review and Compliance Process

### Pull Request Reviews

All PRs MUST verify:
1. Feature follows vertical slice pattern and all layers are updated consistently.
2. Layer boundaries are respected (no business logic in Api, no infra concerns in Domains).
3. Domain entities use class-first design with encapsulated mutation.
4. Tests are provided and pass with coverage tracked.
5. AGENTS.md is updated if a new pattern emerges or is refined.
6. Code follows naming and structure rules outlined above.

### Architecture Validation

New feature additions MUST be validated against this constitution before merge. If a feature violates
a principle, it is sent back for redesign—no exceptions. Complexity is justified in the PR description.

### Maintenance and Evolution

AGENTS.md is the living document of verified patterns. When a pattern is refined or a new pattern
emerges, add it to AGENTS.md with a code example and update this constitution if the pattern affects
core principles. Use the version bump rules below to signal the scope of change.

## Governance

**This constitution supersedes all prior practices.** In cases of conflict, this document directs
implementation decisions.

### Amendment Procedure

1. Identify the change (Principle update, new constraint, process clarification).
2. Update `.specify/memory/constitution.md` with the new text.
3. Update dependent templates in `.specify/templates/` using the sync checklist (see below).
4. Include in commit message: `docs: amend constitution [reason]`.
5. Bump version per semver rules (MAJOR: backward-incompatible removals; MINOR: new principles/constraints;
   PATCH: clarifications/wording).

### Sync Checklist for Dependent Templates

Whenever constitution is amended:
- [ ] `.specify/templates/spec-template.md` — validate scope/requirements sections align with new constraints.
- [ ] `.specify/templates/plan-template.md` — verify architecture/design sections reflect updated principles.
- [ ] `.specify/templates/tasks-template.md` — ensure task categorization reflects new principle-driven types.
- [ ] `AGENTS.md` — confirm documented patterns remain compatible; add code-verified examples for new principles.
- [ ] `README.md` / runtime guidance — update references to changed principles.

### Compliance Review Cadence

Architecture reviews occur during feature specification and PR review. No feature ships without
validation against this constitution.

---

**Version**: 1.0.0 | **Ratified**: 2026-03-17 | **Last Amended**: 2026-03-17
