# Tasks: BDD Test Setup with Reqnroll and NUnit

**Input**: Design documents from `/specs/004-bdd-reqnroll-nunit-setup/`  
**Feature Branch**: `004-bdd-reqnroll-nunit-setup`  
**Generated**: 2026-03-31  
**Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md) | **Research**: [research.md](./research.md)

## Format: `[ID] [P?] [Story?] Description`

- **[P]**: Can run in parallel with other [P]-marked tasks in the same phase (different files, no shared dependencies)
- **[Story]**: Which user story this task belongs to — [US1], [US2], [US3]
- All paths are relative to the repo root unless shown as absolute

---

## Phase 1: Setup — Project File Changes

**Purpose**: Wire `Minimal.App.BDDTests.csproj` with the references and analyzer suppressions needed before any C# code can compile.

**File**: `src/ApiEndpoints/Minimal.App.BDDTests/Minimal.App.BDDTests.csproj`

> All five tasks in this phase touch the **same file** — apply them sequentially in the order listed.  
> Do **not** add `Version=` attributes to any `<PackageReference>` except where `VersionOverride=` is explicitly stated; all versions are centrally managed in `src/Directory.Packages.props`.

- [X] T001 Add `<ProjectReference Include="../Minimal.Api/Minimal.Api.csproj" />` inside a new `<ItemGroup>` in `src/ApiEndpoints/Minimal.App.BDDTests/Minimal.App.BDDTests.csproj` — required so `WebApplicationFactory<Minimal.Api.Program>` resolves at compile time

- [X] T002 Add `<PackageReference Include="Microsoft.AspNetCore.Mvc.Testing"/>` (no `Version=` attribute) inside an `<ItemGroup>` in `src/ApiEndpoints/Minimal.App.BDDTests/Minimal.App.BDDTests.csproj` — provides `WebApplicationFactory<TEntryPoint>` and related types

- [X] T003 Add `<PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" VersionOverride="10.0.5"/>` inside an `<ItemGroup>` in `src/ApiEndpoints/Minimal.App.BDDTests/Minimal.App.BDDTests.csproj` — the `VersionOverride` value must match the identical override in `Minimal.App.Tests.csproj`; this package is in `Directory.Packages.props` at version 9.0.10 but both test projects lock it to 10.0.5 via override

- [X] T004 Add the following analyzer suppression `<PropertyGroup>` block to `src/ApiEndpoints/Minimal.App.BDDTests/Minimal.App.BDDTests.csproj`, matching the identical block already present in `Minimal.App.Tests.csproj` — prevents warnings-as-errors from failing BDD test code that legitimately omits XML docs, uses public parameterless constructors, etc.:
  ```xml
  <PropertyGroup>
    <!-- Disable code analysis for test code -->
    <EnforceCodeStyleInBuild>false</EnforceCodeStyleInBuild>
    <EnableNETAnalyzers>false</EnableNETAnalyzers>
    <AnalysisMode>None</AnalysisMode>
    <RunAnalyzers>false</RunAnalyzers>
    <RunAnalyzersDuringBuild>false</RunAnalyzersDuringBuild>
    <RunAnalyzersDuringLiveAnalysis>false</RunAnalyzersDuringLiveAnalysis>
    <GenerateDocumentationFile>false</GenerateDocumentationFile>
    <NoWarn>$(NoWarn);CS1591;SA1600</NoWarn>
  </PropertyGroup>
  ```

- [X] T005 Add `<Using Include="System.Net"/>` and `<Using Include="Microsoft.Extensions.DependencyInjection"/>` entries inside the existing `<ItemGroup>` that already contains `<Using Include="NUnit.Framework"/>` in `src/ApiEndpoints/Minimal.App.BDDTests/Minimal.App.BDDTests.csproj` — makes `HttpStatusCode`, `IServiceProvider`, and `IServiceScope` available implicitly in all BDD test files

**Checkpoint — Phase 1 complete**: `dotnet restore src/DKNet.Templates.sln` should succeed without errors after these changes.

---

## Phase 2: Foundational — Shared Global Usings

**Purpose**: Provide a file-based global using declaration as a complement to the `.csproj` `<Using>` entries. This is required before any Support or Feature C# files will compile cleanly.

- [X] T006 Create `src/ApiEndpoints/Minimal.App.BDDTests/GlobalUsings.cs` with the following content — mirrors the pattern in `Minimal.App.Tests/GlobalUsings.cs`:
  ```csharp
  global using NUnit.Framework;
  global using System.Net;
  global using Microsoft.Extensions.DependencyInjection;
  ```

**Checkpoint — Phase 2 complete**: All subsequent C# files may use `NUnit.Framework`, `HttpStatusCode`, `IServiceScope`, etc. without explicit `using` directives.

---

## Phase 3: User Story 1 — BDD Infrastructure with Hosted API (Priority: P1) 🎯 MVP

**Goal**: A single `WebApplicationFactory<Program>` instance boots `Minimal.Api` with an in-memory database, disabled external services, and a stubbed `IMembershipService`. Reqnroll hooks wire this factory into every scenario via `IObjectContainer`.

**Independent Test**: Run `dotnet test src/ApiEndpoints/Minimal.App.BDDTests` — the host must start without errors and `BeforeScenario` must execute `ResetDatabaseAsync` without exception (even before any `.feature` files exist).

- [X] T007 [US1] Create `src/ApiEndpoints/Minimal.App.BDDTests/Support/BddApiFactory.cs` — a `sealed` class extending `WebApplicationFactory<Minimal.Api.Program>` that mirrors `ApiFixture.cs` from `Minimal.App.Tests`. Required implementation details:
  - Namespace: `Minimal.App.BDDTests.Support`
  - Field: `private readonly string _dbName = "bdd-tests";` (fixed name, not a Guid — the per-scenario `ResetDatabaseAsync` handles isolation)
  - Override `ConfigureWebHost(IWebHostBuilder builder)`:
    - `builder.UseEnvironment("Testing")`
    - `builder.ConfigureAppConfiguration` → add in-memory collection with keys:
      - `FeatureManagement:RunDbMigrationWhenAppStart` = `"false"`
      - `FeatureManagement:EnableSwagger` = `"false"`
      - `FeatureManagement:EnableAzureAppConfig` = `"false"`
      - `FeatureManagement:RequireAuthorization` = `"false"`
      - `ConnectionStrings:AppDb` = `"UseInMemory"`
    - `builder.ConfigureServices`: remove all registrations of `IDbContextOptionsConfiguration<CoreDbContext>`, `IConfigureOptions<DbContextOptions<CoreDbContext>>`, `IPostConfigureOptions<DbContextOptions<CoreDbContext>>`, `DbContextOptions<CoreDbContext>`, and `CoreDbContext`; then `AddDbContext<CoreDbContext>(o => o.UseInMemoryDatabase(_dbName).UseAutoConfigModel([typeof(CoreDbContext).Assembly]))`; then `RemoveAll<IMembershipService>()` + `AddSingleton<IMembershipService, TestMembershipService>()`
  - Method: `public IServiceScope CreateScope() => Services.CreateScope()`
  - Method: `public async Task ResetDatabaseAsync()` — creates a scope, resolves `CoreDbContext`, calls `EnsureDeletedAsync()` then `EnsureCreatedAsync()`
  - Required `using` directives: `Microsoft.AspNetCore.Hosting`, `Microsoft.AspNetCore.Mvc.Testing`, `Microsoft.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Infrastructure`, `Microsoft.Extensions.Configuration`, `Microsoft.Extensions.DependencyInjection.Extensions`, `Microsoft.Extensions.Options`, `Minimal.Domains.Services`, `Minimal.Infra.Contexts`

- [X] T008 [P] [US1] Create `src/ApiEndpoints/Minimal.App.BDDTests/Support/TestMembershipService.cs` — a `sealed internal` class implementing `IMembershipService` identical to the copy in `Minimal.App.Tests/Integration/Support/TestMembershipService.cs`. Required implementation:
  - Namespace: `Minimal.App.BDDTests.Support`
  - Private field: `private int _current;`
  - Method: `public ValueTask<string> NextValueAsync()` — increments `_current` via `Interlocked.Increment(ref _current)` and returns `ValueTask.FromResult($"TEST-MEM-{next:D6}")`
  - Required `using` directive: `Minimal.Domains.Services`

- [X] T009 [P] [US1] Create `src/ApiEndpoints/Minimal.App.BDDTests/Support/ScenarioState.cs` — a simple mutable class that step definitions use to share HTTP response data across Given/When/Then steps within a single scenario:
  - Namespace: `Minimal.App.BDDTests.Support`
  - Property: `public HttpResponseMessage? Response { get; set; }`
  - Property: `public string? ResponseBody { get; set; }`
  - No constructor logic needed

- [X] T010 [US1] Create `src/ApiEndpoints/Minimal.App.BDDTests/Support/ApiHooks.cs` — a `[Binding]`-decorated class managing the `BddApiFactory` lifecycle and per-scenario Reqnroll DI wiring. Depends on T007, T008, T009 being created first. Required implementation:
  - Namespace: `Minimal.App.BDDTests.Support`
  - Class decorator: `[Binding]`
  - Constructor: `public ApiHooks(IObjectContainer objectContainer)` — stores `objectContainer` in a private field
  - Static fields: `private static BddApiFactory _factory = null!;` and `private static HttpClient _client = null!;`
  - `[BeforeTestRun]` static method: creates `_factory = new BddApiFactory()`, sets `_client = _factory.CreateClient()`
  - `[AfterTestRun]` static async method: calls `await _factory.DisposeAsync()`
  - `[BeforeScenario(Order = 0)]` async instance method: calls `await _factory.ResetDatabaseAsync()`, then calls `_objectContainer.RegisterInstanceAs<HttpClient>(_client)` and `_objectContainer.RegisterInstanceAs(new ScenarioState())`
  - Required `using` directives: `Reqnroll`, `Reqnroll.BoDi`

**Checkpoint — Phase 3 complete**: `dotnet build src/DKNet.Templates.sln` should compile with 0 errors. The test host can initialize, and `ResetDatabaseAsync` runs before each scenario.

---

## Phase 4: User Story 2 — Customer Profile Create BDD Feature Test (Priority: P2)

**Goal**: A complete, runnable `.feature` file covering three Customer Profile creation scenarios (happy path, duplicate email, validation error) with matching NUnit step definitions that call the live `POST /api/v1/customer-profiles` endpoint via the test `HttpClient`.

**Independent Test**: Run `dotnet test src/ApiEndpoints/Minimal.App.BDDTests` and confirm exactly 3 scenarios pass: "Happy path — create a new profile with valid data", "Duplicate email — create a profile with an already-registered email", and "Validation error — create a profile with a missing required field".

- [X] T011 [P] [US2] Create `src/ApiEndpoints/Minimal.App.BDDTests/Features/CustomerProfiles/CreateCustomerProfile.feature` with the following exact Gherkin content (copied verbatim from `specs/004-bdd-reqnroll-nunit-setup/contracts/customer-profile-bdd.md`):
  ```gherkin
  Feature: Create Customer Profile
    As an API consumer
    I want to create a new customer profile via the REST API
    So that the profile is persisted and can be retrieved later

    Background:
      Given the API has no customer profiles

    Scenario: Happy path — create a new profile with valid data
      When I send a create profile request with the following data:
        | Name             | Email                       | Phone        |
        | Integration User | bdd.create@example.com      | +6598765432  |
      Then the response should be successful
      And the response body should contain the profile name "Integration User"

    Scenario: Duplicate email — create a profile with an already-registered email
      Given a customer profile with email "bdd.dup@example.com" already exists
      When I send a create profile request with the following data:
        | Name       | Email                  | Phone        |
        | Duplicate  | bdd.dup@example.com    | +6500011122  |
      Then the response should be successful
      And the response body should contain an error message for duplicate email "bdd.dup@example.com"

    Scenario: Validation error — create a profile with a missing required field
      When I send a create profile request with the following data:
        | Name          | Email | Phone        |
        | Missing Email |       | +6511122233  |
      Then the response should indicate a validation error
  ```

- [X] T012 [US2] Create `src/ApiEndpoints/Minimal.App.BDDTests/Features/CustomerProfiles/Steps/CreateCustomerProfileSteps.cs` — a `[Binding]`-decorated class containing all step definitions required by `CreateCustomerProfile.feature`. Depends on T010 (ApiHooks) and T011 (feature file). Required implementation details:
  - Namespace: `Minimal.App.BDDTests.Features.CustomerProfiles.Steps`
  - Class decorator: `[Binding]`
  - Constructor: `public CreateCustomerProfileSteps(HttpClient client, ScenarioState state, BddApiFactory factory)` — store all three in private readonly fields
  - **Endpoint constant**: `private const string CreateUrl = "/api/v1/customer-profiles";`
  - **Step: `[Given("the API has no customer profiles")]`** — calls `await _factory.ResetDatabaseAsync()` (additional reset on top of the hook-level reset, for clarity in the Background step)
  - **Step: `[Given("a customer profile with email {string} already exists")]`** — sends `POST CreateUrl` with a seed payload `{ name = "Seed User", email = capturedEmail, phone = "+6500000001" }` plus `X-Idempotency-Key: Guid.NewGuid().ToString()` header; asserts that the seed response is HTTP 200 before continuing
  - **Step: `[When("I send a create profile request with the following data:")]`** with a `DataTable table` parameter — reads `Name`, `Email`, `Phone` from `table.Rows[0]`; sends `POST CreateUrl` with JSON body `{ name, email, phone }` and `X-Idempotency-Key: Guid.NewGuid().ToString()` header; stores `response` in `_state.Response` and `_state.ResponseBody = await response.Content.ReadAsStringAsync()`
  - **Step: `[Then("the response should be successful")]`** — parses `_state.ResponseBody` with `JsonDocument.Parse`; asserts `isSuccess` property is `true` using Shouldly (`ShouldBeTrue()`)
  - **Step: `[Then("the response body should contain the profile name {string}")]`** with `string expectedName` — parses `_state.ResponseBody`; reads `value.name`; asserts equals `expectedName` via Shouldly
  - **Step: `[Then("the response body should contain an error message for duplicate email {string}")]`** with `string email` — parses `_state.ResponseBody`; asserts `isSuccess` is `false`; asserts `errors[0].message` contains `email` via Shouldly (`ShouldContain(email)`)
  - **Step: `[Then("the response should indicate a validation error")]`** — asserts `_state.Response!.StatusCode == HttpStatusCode.BadRequest` via Shouldly
  - Required `using` directives: `System.Text.Json`, `Reqnroll`, `Minimal.App.BDDTests.Support`
  - Helper method (private): `SendCreateRequest(object payload)` — creates `StringContent` from `JsonSerializer.Serialize(payload)` with `application/json` media type; adds `X-Idempotency-Key` header per-call using `new StringContent(...)` + `request.Headers.Add(...)`; or use `HttpRequestMessage` to attach header cleanly

**Checkpoint — Phase 4 complete**: `dotnet test src/ApiEndpoints/Minimal.App.BDDTests` produces `Passed: 3, Failed: 0, Skipped: 0`.

---

## Phase 5: User Story 3 — Developer Onboarding Conventions (Priority: P3)

**Goal**: Ensure the project structure, naming, and step-reuse conventions are self-documenting so a new developer can add a second `.feature` file and its step definitions without reading external docs.

**Independent Test**: A developer follows only the code in `Features/CustomerProfiles/` to create `Features/CustomerProfiles/DeleteCustomerProfile.feature` with a new scenario and corresponding step binding — all new scenarios appear in the `dotnet test` output automatically.

- [X] T013 [US3] Add XML `<summary>` doc comments to the `ApiHooks` class and each hook method in `src/ApiEndpoints/Minimal.App.BDDTests/Support/ApiHooks.cs` that explain:  
  (1) the `[BeforeTestRun]` / `[AfterTestRun]` static lifecycle methods boot and tear down the `BddApiFactory` **once per test run** — do not add more `[BeforeTestRun]` hooks in feature step files  
  (2) the `[BeforeScenario]` method resets the in-memory database and registers `HttpClient` + `ScenarioState` in `IObjectContainer` — any `[Binding]` class can take these types as constructor parameters  
  (3) to add a new feature, create `Features/<Domain>/<Action>.feature` and `Features/<Domain>/Steps/<Action>Steps.cs` with `[Binding]` — Reqnroll auto-discovers all `[Binding]` classes in the assembly without project-file changes

**Checkpoint — Phase 5 complete**: The `ApiHooks.cs` file is self-explanatory as a developer reference; structure conventions are encoded in comments rather than a separate wiki page.

---

## Final Phase: Polish & Validation

**Purpose**: Remove scaffolding noise and verify the full solution builds and all 3 BDD scenarios pass end-to-end.

- [X] T014 Delete `src/ApiEndpoints/Minimal.App.BDDTests/UnitTest1.cs` — this is the NUnit SDK placeholder file created by `dotnet new`; it has no meaningful content and its presence causes "test has no tests" warnings in the build output

- [X] T015 Run `dotnet build src/DKNet.Templates.sln -c Release` from the repo root and confirm the output ends with `Build succeeded` and `0 Error(s)` — fix any compile errors before proceeding to T016

- [X] T016 Run `dotnet test src/ApiEndpoints/Minimal.App.BDDTests` from the repo root and confirm the output shows `Passed: 3, Failed: 0, Skipped: 0` with scenario titles visible:
  - `Happy path — create a new profile with valid data`
  - `Duplicate email — create a profile with an already-registered email`
  - `Validation error — create a profile with a missing required field`

- [X] T017 Verify `src/ApiEndpoints/Minimal.App.BDDTests/Minimal.App.BDDTests.csproj` contains no `xunit`, `MSTest`, or `Microsoft.Testing.Platform` `<PackageReference>` entries (FR-008 enforcement) — open the file and confirm `Reqnroll.NUnit` and `NUnit` are the **only** test runner references; if any disallowed runner is found, remove it

- [X] T018 Update `AGENTS.md` (repo root) to document the new BDD test pattern under a new "BDD Testing" section (Constitution principle VII — code-verified patterns). Add the following after the "Testing and quality constraints" section:
  ```
  ## BDD Testing (Reqnroll + NUnit)
  - BDD tests live in `src/ApiEndpoints/Minimal.App.BDDTests/`.
  - `Support/BddApiFactory.cs` boots `WebApplicationFactory<Program>` once per test run using Reqnroll `[BeforeTestRun]` hook in `ApiHooks.cs`.
  - In-memory EF Core + disabled migrations/AzureAppConfig — no external services required.
  - Each scenario resets the DB in `[BeforeScenario(Order=0)]`; `HttpClient` and `ScenarioState` are injected into step defs via Reqnroll's BoDi `IObjectContainer`.
  - Add new scenarios: create `.feature` under `Features/<Domain>/` and matching `[Binding]` step class under `Features/<Domain>/Steps/`.
  - POST endpoints require `X-Idempotency-Key: {Guid}` header — generate `Guid.NewGuid()` per request in `[When]` steps.
  ```
---

## Dependency Graph

```
T001 → T002 → T003 → T004 → T005     (all project file edits, sequential in same file)
                                  ↓
                                T006  (GlobalUsings.cs — foundational)
                                  ↓
                    T007             T008 [P]     T009 [P]
                     ↓               ↓              ↓
                    T010  ←──────────┘──────────────┘   (ApiHooks needs all Support classes)
                     ↓
             T011 [P]   T012
              ↓          ↓
             T013       T013    (onboarding docs)
               ↓
             T014  (delete placeholder)
               ↓
             T015  (build validation)
               ↓
             T016  (test validation)
```

**Story completion order** (blocked stories listed after their dependency):
- US1 (T007–T010) — no cross-story dependencies; MVP deliverable
- US2 (T011–T012) — requires US1 (T010) complete
- US3 (T013) — requires US2 (T012) complete for meaningful documentation

---

## Parallel Execution

Within Phase 3, once T007 body is written, T008 and T009 can be written in parallel:

```bash
# In parallel: write TestMembershipService.cs and ScenarioState.cs
# Then sequentially: write ApiHooks.cs (T010) which imports both
```

Within Phase 4, T011 (`.feature` file — pure Gherkin text) can be written concurrently with T010 (ApiHooks completion):

```bash
# In parallel: finalize ApiHooks.cs + write CreateCustomerProfile.feature
# Then sequentially: write CreateCustomerProfileSteps.cs (needs both)
```

---

## Implementation Strategy (MVP First)

**MVP scope = Phase 1 + Phase 2 + Phase 3 (US1 only)** — delivers a compiling project with a working test host and database reset. At this checkpoint `dotnet build` passes with 0 errors and the Reqnroll infrastructure is proven even before the first `.feature` file exists.

**Increment 2 = Phase 4 (US2)** — adds `CreateCustomerProfile.feature` + step definitions; `dotnet test` shows 3 passing BDD scenarios.

**Increment 3 = Phase 5 + Final Phase (US3 + Validation)** — adds developer onboarding documentation and removes the placeholder file; full solution test suite passes.

---

## Key Technical Constraints (reference)

| Constraint | Value | Source |
|---|---|---|
| Target framework | `net10.0` | `src/global.json` |
| `WebApplicationFactory` entry point | `Minimal.Api.Program` | `Minimal.Api/Program.cs` is the entry class |
| In-memory DB name | `"bdd-tests"` | Fixed string (not Guid) — isolation via `ResetDatabaseAsync` |
| `UseAutoConfigModel` assembly arg | `typeof(CoreDbContext).Assembly` | Mirrors `ApiFixture.cs` pattern |
| POST route | `/api/v1/customer-profiles` | `CustomerProfileV1Endpoint` with `Version = 1` |
| Required POST header | `X-Idempotency-Key: <NewGuid per call>` | Idempotency filter on `MapPost` |
| HTTP 200 = business failure | `isSuccess: false` in body | API convention for duplicate email |
| HTTP 400 = validation failure | ProblemDetails body | FluentValidation middleware |
| `IMembershipService` registration | `AddSingleton` | Must outlive per-request scopes |
| `VersionOverride` for EF InMemory | `10.0.5` | Matches `Minimal.App.Tests.csproj` |
| No `Version=` on other packages | (omit entirely) | Centrally managed in `Directory.Packages.props` |

---

## Summary

| Metric | Value |
|---|---|
| Total tasks | 16 |
| US1 tasks | T007–T010 (4 tasks) |
| US2 tasks | T011–T012 (2 tasks) |
| US3 tasks | T013 (1 task) |
| Setup/Foundational tasks | T001–T006 (6 tasks) |
| Validation tasks | T014–T016 (3 tasks) |
| Parallelizable tasks | T008, T009 (Phase 3); T011 partial (Phase 4) |
| Scenarios verified at completion | 3 |
| External services required | None |
