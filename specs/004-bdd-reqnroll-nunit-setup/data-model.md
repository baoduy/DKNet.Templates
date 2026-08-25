# Data Model: BDD Test Infrastructure Classes

**Feature**: `004-bdd-reqnroll-nunit-setup`  
**Date**: 2026-03-31  
**Layer**: `src/ApiEndpoints/Minimal.App.BDDTests`

> This feature has no new domain entities or EF Core migrations. The "data model" here describes the class hierarchy and relationships of the BDD test infrastructure.

> **2026-08-25 note:** This document is a historical record from when the template's demo
> features were `CustomerProfile`/`LoyaltyMembership`. Those were removed; current worked
> examples are the `PurchaseOrder` (hand-written) and `Product` (generator-driven) samples —
> see `docs/samples/manual-vs-automated.md`.

---

## Class Hierarchy

```
WebApplicationFactory<Program>  (from Microsoft.AspNetCore.Mvc.Testing)
  └── BddApiFactory                     sealed class — test host with production overrides
        ├── ConfigureWebHost()           overrides: in-memory DB, disabled migrations, TestMembershipService
        ├── CreateScope()               convenience: IServiceScope from running host
        └── ResetDatabaseAsync()         [BeforeScenario] target: wipes and recreates in-memory DB

IMembershipService  (from Minimal.Domains.Services)
  └── TestMembershipService             sealed, internal — returns "TEST-MEM-{counter:D6}"

[Binding] class ApiHooks                Reqnroll Hooks — manages factory lifecycle + per-scenario wiring
  ├── [BeforeTestRun]                   creates BddApiFactory, HttpClient; registers in static store
  ├── [AfterTestRun]                    disposes BddApiFactory
  └── [BeforeScenario(Order=0)]         resets DB, registers HttpClient + ScenarioState in IObjectContainer

ScenarioState                           scenario-scoped state bag, injected into step defs
  ├── HttpResponseMessage? Response
  └── string? ResponseBody

[Binding] class CreateCustomerProfileSteps  step definitions for CreateCustomerProfile.feature
  ├── ctor(HttpClient, ScenarioState)   Reqnroll injects both from IObjectContainer
  ├── [Given("the API has no customer profiles")]   → calls ResetDatabaseAsync
  ├── [Given("a customer profile with email {string} already exists")]  → seeds via POST (happy-path request to CreateUrl with idempotency key)
  ├── [When("I send a create profile request with valid data")]  → POST /api/v1/customer-profiles
  ├── [When("I send a create profile request with duplicate email {string}")]  → POST with conflicting email
  ├── [When("I send a create profile request with blank email")]  → POST with empty email field
  ├── [Then("the response should be successful")]   → Response.StatusCode.IsSuccessStatusCode().ShouldBeTrue()
  ├── [Then("the response should indicate a conflict")]  → Response body contains error message
  └── [Then("the response should indicate a validation error")]  → 400 or validation failure body
```

---

## Key Types Reference

| Class | Namespace | Role |
|-------|-----------|------|
| `BddApiFactory` | `Minimal.App.BDDTests.Support` | Test host — owns `WebApplicationFactory<Program>` |
| `TestMembershipService` | `Minimal.App.BDDTests.Support` | Stub for `IMembershipService` |
| `ApiHooks` | `Minimal.App.BDDTests.Support` | Reqnroll `[Binding]` — lifecycle + DI wiring |
| `ScenarioState` | `Minimal.App.BDDTests.Support` | Mutable per-scenario HTTP state bag |
| `CreateCustomerProfileSteps` | `Minimal.App.BDDTests.Features.CustomerProfiles.Steps` | Step bindings |

---

## Relationships and Data Flow

```
[BeforeTestRun]
  BddApiFactory.CreateClient()
      │
      ▼
  WebApplicationFactory boots Minimal.Api
      └─ ConfigureWebHost overrides:
            FeatureManagement:RunDbMigrationWhenAppStart = false
            FeatureManagement:EnableSwagger = false
            FeatureManagement:EnableAzureAppConfig = false
            ConnectionStrings:AppDb = "UseInMemory"
            CoreDbContext → UseInMemoryDatabase("bdd-tests")
            IMembershipService → TestMembershipService
      │
      ▼
  HttpClient (BaseAddress = http://localhost)
      └─ stored in static ApiHooks._client

[BeforeScenario(Order=0)]
  ApiHooks registers in IObjectContainer:
      ├── HttpClient (the shared client from factory)
      └── new ScenarioState()
  ApiHooks calls BddApiFactory.ResetDatabaseAsync()

Scenario Execution:
  Reqnroll resolves CreateCustomerProfileSteps(HttpClient, ScenarioState)
      │
      [When] step → client.PostAsJsonAsync(url, body, headers)
                       └─ writes Result to ScenarioState.Response
      │
      [Then] step → reads ScenarioState.Response → Shouldly assertions
```

---

## Feature File Structure Convention

```
Features/
└── <DomainConcept>/                   folder per domain concept
    ├── <ActionName>.feature           Gherkin document
    └── Steps/
        └── <ActionName>Steps.cs       [Binding] class with step methods
```

**Rules**:
- One `.feature` file per top-level user action (Create, Update, Delete, Get)
- Step definition class name = feature file name + `Steps`
- Reusable "background" steps shared across features live in `Support/CommonSteps.cs`
- No `[Parallelizable(ParallelScope.All)]` on step classes — NUnit defaults to sequential within a feature; this avoids DB isolation complexity

---

## State Transitions: Customer Profile Creation

```
Initial state: DB empty
    │
POST /api/v1/customer-profiles  (valid payload + Idempotency-Key header)
    │
    ├── Happy path  → 200 OK + CustomerProfileDto body
    ├── Duplicate email → 200 with IsFailed result (API convention: always 200, result in body)
    └── Missing email → 400 Bad Request (FluentValidation middleware)
```

> **Note**: The API returns HTTP 200 for business-rule failures (duplicate email) and HTTP 400 for validation failures (missing required fields). Step definitions must assert against both HTTP status AND response body depending on scenario.
