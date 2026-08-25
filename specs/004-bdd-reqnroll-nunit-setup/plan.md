# Implementation Plan: BDD Test Setup with Reqnroll and NUnit

**Branch**: `004-bdd-reqnroll-nunit-setup` | **Date**: 2026-03-31 | **Spec**: [spec.md](./spec.md)  
**Input**: Feature specification from `specs/004-bdd-reqnroll-nunit-setup/spec.md`

> **2026-08-25 note:** This document is a historical record from when the template's demo
> features were `CustomerProfile`/`LoyaltyMembership`. Those were removed; current worked
> examples are the `PurchaseOrder` (hand-written) and `Product` (generator-driven) samples —
> see `docs/samples/manual-vs-automated.md`.

## Summary

Wire the existing `Minimal.App.BDDTests` project into a production-quality BDD harness: add `WebApplicationFactory<Program>` lifecycle management via Reqnroll hooks, substitute an in-memory EF Core database, and deliver one complete `.feature` file covering Customer Profile creation (happy-path, duplicate, and validation-error scenarios) with matching NUnit step definitions. The design mirrors the `ApiFixture` pattern already proven in `Minimal.App.Tests`.

## Technical Context

**Language/Version**: C# 13 / .NET 10.0 (pinned in `src/global.json`)  
**Primary Dependencies**:
- `Reqnroll.NUnit` 3.3.4 (already in `Directory.Packages.props`)
- `NUnit` 4.5.1 + `NUnit3TestAdapter` 6.2.0 (already in `.csproj`)
- `Microsoft.AspNetCore.Mvc.Testing` 10.0.5 (in `Directory.Packages.props`, needs adding to BDDTests `.csproj`)
- `Microsoft.EntityFrameworkCore.InMemory` 9.0.10 (in `Directory.Packages.props`, needs adding to BDDTests `.csproj`)
- `Shouldly` (already in BDDTests `.csproj`)

**Storage**: In-memory EF Core (`UseInMemoryDatabase`) — replaces SQL Server only within the test host  
**Testing**: Reqnroll.NUnit (Gherkin runner) + NUnit 4 (assertions/lifecycle) + Shouldly (fluent assertions in step defs)  
**Target Platform**: Local developer machine and CI, no external services required  
**Project Type**: BDD test project (test-only, no production runtime)  
**Performance Goals**: Full scenario suite completes in < 60 seconds  
**Constraints**: Zero external services (SQL Server, Azure Service Bus, Azure App Config all disabled); no xUnit or MSTest references; no `Version=` attribute in `.csproj` package references  
**Scale/Scope**: 1 feature file, 3 scenarios, 1 hooks class, 1 step definition class — intentionally minimal for onboarding

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

> Note: This feature is **test infrastructure**, not a production domain slice. Items V (Event-Driven), IV (EF Core Auto Config), and I (Vertical Slice) apply to the system under test, not to the BDD project itself.

- [x] **Vertical Slice** — N/A for test project; scenarios test the existing `CustomerProfiles/V1` slice end-to-end via HTTP, exercising all layers indirectly
- [x] **Layer Boundaries** — Test infrastructure (Hooks, step defs) contains no business logic; it delegates to the API under test
- [x] **Class-First Domain** — `ApiHooks`, `BddApiFactory`, and step definition classes are proper OOP classes; no module-level procedural logic (per user coding preferences)
- [x] **EF Core Configuration** — Production app's `UseAutoConfigModel` is reused transparently; only the in-memory DB provider swap is added in the test host
- [x] **Event-Driven Integration** — N/A; in-memory bus handles domain events within the hosted API without changes
- [x] **Test Coverage** — This feature IS the test layer; BDD scenarios verify API HTTP behavior from the outside
- [x] **Code-Verified Patterns** — Design mirrors `ApiFixture.cs` + `TestMembershipService.cs` from `Minimal.App.Tests`

**GATE: PASS — All checks satisfied. Proceeding to Phase 0.**

## Project Structure

### Documentation (this feature)

```text
specs/004-bdd-reqnroll-nunit-setup/
├── plan.md              ✓ this file
├── research.md          ✓ Phase 0 output
├── data-model.md        ✓ Phase 1 output
├── quickstart.md        ✓ Phase 1 output
├── contracts/           ✓ Phase 1 output
│   └── customer-profile-bdd.md  (canonical Gherkin template + HTTP contract + step binding table)
└── tasks.md             (Phase 2 — /speckit.tasks command)
```

### Source Code

```text
src/ApiEndpoints/Minimal.App.BDDTests/
├── Minimal.App.BDDTests.csproj          ← add Mvc.Testing + EF InMemory refs + ProjectRef to Minimal.Api
├── GlobalUsings.cs                      ← NUnit.Framework + System.Net + Microsoft.Extensions.DependencyInjection
│
├── Support/
│   ├── BddApiFactory.cs                 ← WebApplicationFactory<Minimal.Api.Program> with in-memory overrides
│   ├── TestMembershipService.cs         ← copied/shared from Minimal.App.Tests (sealed IMembershipService stub)
│   └── ApiHooks.cs                      ← [Binding] Reqnroll hooks: BeforeTestRun / AfterTestRun / BeforeScenario
│
└── Features/
    └── CustomerProfiles/
        ├── CreateCustomerProfile.feature  ← Gherkin: 3 scenarios
        └── Steps/
            └── CreateCustomerProfileSteps.cs  ← [Binding] step definitions with injected HttpClient
```

**Structure Decision**: Single test project pattern. `Support/` mirrors `Minimal.App.Tests/Integration/Support/`. `Features/<Name>/Steps/` keeps Gherkin and bindings co-located.

## Complexity Tracking

> No constitution violations — table not required.
