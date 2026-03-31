# Research: BDD Test Setup with Reqnroll and NUnit

**Feature**: `004-bdd-reqnroll-nunit-setup`  
**Date**: 2026-03-31  
**Status**: Complete — all NEEDS CLARIFICATION resolved

---

## Decision 1: WebApplicationFactory Lifecycle Strategy

**Decision**: Create one `WebApplicationFactory<Program>` instance per test run using `[BeforeTestRun]` and dispose it in `[AfterTestRun]`.

**Rationale**: The factory boots the full ASP.NET Core host (middleware pipeline, DI container, EF Core schema). This is expensive and should happen once per process, not per scenario or feature. `[BeforeTestRun]` is a static Reqnroll hook that executes once before any scenario in the entire test assembly. Reqnroll registers the factory in a static field accessible via `IObjectContainer` injection.

**Alternatives considered**:
- `[BeforeFeature]` per-feature factory — feature-scoped; cheaper isolation but slow when many feature files exist; unnecessary for a small suite.
- `[BeforeScenario]` per-scenario factory — completely isolates scenarios at the cost of a full host boot per test; impractical (10–30 s per scenario).
- `IClassFixture<>` (xUnit pattern) — not available in NUnit/Reqnroll; excluded by FR-008.

**Codebase evidence**: `ApiFixture.cs` in `Minimal.App.Tests` uses a single `WebApplicationFactory<Program>` shared across all tests in a class via `IClassFixture<>`. The Reqnroll equivalent is a static test-run-scoped hook.

---

## Decision 2: Reqnroll Dependency Injection into Step Definitions

**Decision**: Use Reqnroll's built-in BoDi `IObjectContainer` to register the `HttpClient` and any scenario-scoped services in `[BeforeScenario]`. Step definition classes declare them as constructor parameters — Reqnroll resolves them automatically.

**Rationale**: Reqnroll uses BoDi (a lightweight IoC container) for step-definition binding. Any type registered in `IObjectContainer` in a `[Binding]` Hooks class is constructor-injectable into any other `[Binding]` class in the same scenario. This avoids `ScenarioContext` property bags and keeps step definitions clean and testable.

**Alternatives considered**:
- Static `HttpClient` field — avoids DI but violates class-first OOP preference and makes parallel test extension harder.
- `ScenarioContext` property bag — works but is untyped; swapping to constructor injection is idiomatic and preferred by Reqnroll docs.

---

## Decision 3: Database Isolation between Scenarios

**Decision**: Use a **single shared in-memory database** (one DB name per test run), but call `EnsureDeletedAsync()` + `EnsureCreatedAsync()` in `[BeforeScenario]` to reset state.

**Rationale**: Because the factory is shared across the run, all scenarios share the same `CoreDbContext` instance resolved from the `WebApplicationFactory`'s scope. Calling reset before each scenario removes stale data from previous scenarios without the cost of bootstrapping a new DB provider. This is the same pattern used by `ApiFixture.ResetDatabaseAsync()`.

**Alternatives considered**:
- Unique DB name per scenario (`$"bdd-{Guid.NewGuid()}"`) — cleanest isolation, but requires registering a new `DbContext` options per scenario inside the running host (invasive); not worth complexity for a small suite.
- Transaction rollback per scenario — does not work with in-memory EF Core (no real transactions).

---

## Decision 4: IMembershipService Stub

**Decision**: Copy `TestMembershipService` from `Minimal.App.Tests/Integration/Support/` directly into `Minimal.App.BDDTests/Support/`. Do not share it via a reusable test library project.

**Rationale**: The stub is six lines. Creating a shared test-helpers project to avoid duplication adds a project reference, a new `.csproj`, and messes with Scrutor scanning. Duplication cost is negligible; decoupling cost is high.

**Alternatives considered**:
- Shared `Minimal.App.TestHelpers` project — adds project complexity for a trivial class.
- Reference `Minimal.App.Tests` directly from BDDTests — creates an inappropriate dependency between two test projects.

---

## Decision 5: HTTP Response Sharing between Given/When/Then Steps

**Decision**: Register an `HttpResponseMessage` wrapper object (`ScenarioState`) in `IObjectContainer` during `[BeforeScenario]`. Step definitions mutate it in `[When]` steps and read it in `[Then]` steps.

**Rationale**: BDD steps in a scenario share state through a scenario-scoped object. Reqnroll's `IObjectContainer` is the correct mechanism — any `[Binding]` class that requests the same type gets the same instance within one scenario. A simple `ScenarioState` record (holding `HttpResponseMessage?` and `string? ResponseBody`) is sufficient and keeps `[When]` steps focused on sending requests and `[Then]` steps on asserting.

**Alternatives considered**:
- `ScenarioContext.Current["response"]` — untyped dictionary; error-prone; obsolete in Reqnroll.
- Direct field on `HttpClient` wrapper — coupling the HTTP sender to the assertion logic; harder to extend.

---

## Decision 6: Feature File HTTP API Route

**Decision**: Use `POST /api/v1/customer-profiles` as the target endpoint, matching the route produced by `CustomerProfileV1Endpoint` (`GroupEndpoint = "/customer-profiles"`, `Version = 1`).

**Rationale**: Verified from `CustomerProfileV1Endpoint.cs`. The `EndpointConfig.CreateGroup` convention prefixes with `/api/v{Version}`. Confirmed by `Minimal.Api/Configs/Endpoints` fluent helpers.

**Alternatives considered**: None — route is deterministic from existing code.

---

## Decision 7: Idempotency Key Header

**Decision**: BDD step for the "create profile" request MUST include an `X-Idempotency-Key` header with a random GUID, because `MapPost` on the CustomerProfile endpoint adds `AddIdempotencyFilter()`.

**Rationale**: Without the header, the middleware rejects the request. Step definitions should generate a new GUID per scenario to avoid cross-scenario idempotency cache collisions. The in-memory idempotency cache is scoped to the running host; because the host is shared across the run, keys persist between scenarios. Using a fresh GUID per send ensures uniqueness regardless.

**Alternatives considered**:
- Disabling the idempotency filter in test configuration — would hide real integration behavior; defeats the purpose of BDD.
- Reusing the same key per scenario — risks false negative from cache hit.

---

## Decision 8: NUnit Code Analyzer Suppression

**Decision**: Add the same analyzer suppression block used in `Minimal.App.Tests.csproj` to `Minimal.App.BDDTests.csproj`.

**Rationale**: `Directory.Packages.props` enforces `EnforceCodeStyleInBuild: true` and `TreatWarningsAsErrors` globally. Test code (BDD step classes, hooks) legitimately violates style rules (public parameterless constructors for Reqnroll, missing XML doc, etc.). These are intentionally suppressed for all test projects.

**Alternatives considered**: Per-file `#pragma warning disable` — verbose and easy to forget; project-level suppressions are the established pattern.
