# Feature Specification: BDD Test Setup with Reqnroll and NUnit

**Feature Branch**: `004-bdd-reqnroll-nunit-setup`  
**Created**: 2026-03-31  
**Status**: Draft  
**Input**: User description: "I would like to setup a best practise of the BDDTest using Reqnroll and NUnit for src/ApiEndpoints/Minimal.App.BDDTests and develop 1 feature test to test src/ApiEndpoints/Minimal.Api using WebApplicationFactory<Api.Program>"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - BDD Infrastructure with Hosted API (Priority: P1)

As a developer, I want the `Minimal.App.BDDTests` project to have a shared test host that boots the real `Minimal.Api` application using an in-memory database, so that BDD scenarios can invoke the running API over HTTP without requiring external infrastructure.

**Why this priority**: This is the foundational layer that all BDD scenarios depend on. Without a working test host, no Gherkin feature files can be executed.

**Independent Test**: Can be fully tested by running `dotnet test` on the BDDTests project and confirming the host initializes, the in-memory database seeds successfully, and the test client responds to a basic health/probe request.

**Acceptance Scenarios**:

1. **Given** the BDDTests project is configured, **When** the test run starts, **Then** a `WebApplicationFactory<Program>` instance boots the API with an in-memory database substituted for the real SQL Server connection
2. **Given** the test host is running, **When** a Reqnroll scenario begins, **Then** a pre-configured `HttpClient` is available to step definitions via Reqnroll dependency injection
3. **Given** the test host is running, **When** a scenario ends, **Then** the in-memory database is reset so no state leaks between scenarios

---

### User Story 2 - Customer Profile Create BDD Feature Test (Priority: P2)

As a developer, I want at least one complete `.feature` file that describes the Customer Profile creation workflow in business-readable Gherkin and has matching NUnit step definitions that call the live `Minimal.Api` HTTP endpoint, so that the BDD approach is demonstrated end-to-end.

**Why this priority**: This story delivers the first working BDD test and proves the infrastructure works with a real feature scenario pulled from the existing API.

**Independent Test**: Can be fully tested by running the single `CustomerProfile.feature` file's scenarios and observing that the "Create customer profile" happy-path scenario returns HTTP 200 and the failure scenario returns an appropriate error.

**Acceptance Scenarios**:

1. **Given** the API is running and the database is empty, **When** I POST a valid new customer profile, **Then** the API returns a success response and the profile is persisted
2. **Given** a customer profile with email `existing@example.com` already exists, **When** I POST a new profile with the same email, **Then** the API returns a failure response indicating the email is already in use
3. **Given** I POST a profile with a missing required field (e.g., email is blank), **Then** the API returns a validation error response

---

### User Story 3 - Developer Onboarding: Add New BDD Scenario (Priority: P3)

As a developer joining the project, I want clear conventions (folder structure, naming, step-reuse patterns) so that I can add a new Gherkin scenario without modifying infrastructure files.

**Why this priority**: Ensures the BDD setup is sustainable and not just a one-off demo; good conventions lower the cost of every future scenario.

**Independent Test**: Can be fully tested by a developer following only the project README/comments to add a second scenario (e.g., "Get customer profile") without touching any Hooks or fixture classes.

**Acceptance Scenarios**:

1. **Given** an established step definition file exists, **When** a new `.feature` scenario reuses existing step patterns, **Then** no new step definition methods are required
2. **Given** a new `.feature` file is added under the `Features/` folder, **When** the NUnit test runner discovers tests, **Then** the new scenarios appear automatically without project file changes

---

### Edge Cases

- What happens when the API host fails to start (e.g., missing configuration key)? The test run should fail fast with a meaningful error, not time out silently.
- What happens when two scenarios in the same NUnit test run try to write conflicting data? The per-scenario database reset must guarantee isolation.
- What happens when a step definition is missing for a Gherkin step? The Reqnroll runner should report a pending/undefined step with a clear binding hint, not a cryptic exception.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The `Minimal.App.BDDTests.csproj` MUST reference `Minimal.Api.csproj` so that `WebApplicationFactory<Minimal.Api.Program>` can be instantiated within the test project.
- **FR-002**: A Reqnroll Hooks class (e.g., `ApiHooks`) MUST manage the `WebApplicationFactory` lifecycle — creating the host once per test run (or feature) and disposing it after all scenarios complete.
- **FR-003**: The test host MUST override the database connection with an in-memory EF Core database so no external SQL Server is required.
- **FR-004**: The test host MUST disable Azure App Configuration and database migration on startup so tests are self-contained.
- **FR-005**: A shared `ScenarioContext`-based service wrapper MUST provide step definitions with a pre-configured `HttpClient` targeting the test host.
- **FR-006**: At least one `.feature` file (e.g., `Features/CustomerProfiles/CreateCustomerProfile.feature`) MUST be present, containing a minimum of two Gherkin scenarios: a happy-path creation and a duplicate-email failure.
- **FR-007**: Step definitions MUST be co-located with their feature in a `Steps/` folder mirroring the `Features/` folder structure.
- **FR-008**: The project MUST use Reqnroll's built-in NUnit integration (already referenced as `Reqnroll.NUnit`) as the sole test runner — no xUnit or MSTest references.
- **FR-009**: All BDD test scenarios MUST pass when executing `dotnet test` with no external services running.
- **FR-010**: The `Minimal.App.BDDTests.csproj` MUST disable strict code analyzers (matching the pattern of `Minimal.App.Tests.csproj`) to avoid warnings-as-errors failures in test code.

### Key Entities

- **Feature File** (`.feature`): A Gherkin document describing one domain capability in business-readable language. Contains one or more `Scenario` or `Scenario Outline` blocks.
- **Step Definition**: A C# method bound to a Gherkin step via Reqnroll attributes (`[Given]`, `[When]`, `[Then]`). Belongs to a `[Binding]`-decorated class.
- **Hooks Class**: A `[Binding]`-decorated class using `[BeforeTestRun]` / `[AfterTestRun]` / `[BeforeScenario]` / `[AfterScenario]` to manage test infrastructure lifecycle.
- **BDD Test Host**: The `WebApplicationFactory<Program>` instance that wires the real API with test overrides (in-memory DB, stubbed membership, disabled external services).
- **Scenario Context**: Reqnroll's built-in per-scenario state bag, injected into step definition classes to share data (e.g., the HTTP response) between Given/When/Then steps.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Running `dotnet test src/DKNet.Templates.sln` completes with 0 failed tests, including all BDD scenarios in `Minimal.App.BDDTests`.
- **SC-002**: The entire BDD test suite (all scenarios in the project) finishes in under 60 seconds on a developer machine with no external services.
- **SC-003**: Each Gherkin scenario is individually identifiable by its Gherkin title in the NUnit test results output (not just by method name).
- **SC-004**: Adding a second `.feature` file with new scenarios — without touching `ApiHooks` or project files — results in those scenarios being discovered and executed automatically.
- **SC-005**: A developer unfamiliar with the test setup can understand the folder layout and add a new step definition by reading only the existing code (no external documentation needed).

## Assumptions

- The `Reqnroll.NUnit`, `NUnit`, `NUnit.Analyzers`, `NUnit3TestAdapter`, `Microsoft.NET.Test.Sdk`, and `Shouldly` packages are already present in `Minimal.App.BDDTests.csproj` and require no new package additions beyond `Microsoft.AspNetCore.Mvc.Testing` and `Microsoft.EntityFrameworkCore.InMemory`.
- The demonstration feature test will cover the **CustomerProfile Create** endpoint (POST `/api/v1/customer-profiles`) mirroring the existing integration tests in `Minimal.App.Tests`.
- The `IMembershipService` stub used in `Minimal.App.Tests` (`TestMembershipService`) will be referenced or replicated in the BDDTests project's hooks.
- No Azure Service Bus connection string will be present; the in-memory message bus will handle all domain events during tests.
- NUnit's parallel test execution will be kept at the default (no explicit `[Parallelizable]` on feature tests) to avoid database-isolation complexity in the initial setup.
