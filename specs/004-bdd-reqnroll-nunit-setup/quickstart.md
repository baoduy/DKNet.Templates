# Quickstart: BDD Tests with Reqnroll and NUnit

**Feature**: `004-bdd-reqnroll-nunit-setup`  
**Project**: `src/ApiEndpoints/Minimal.App.BDDTests`

---

## Run the BDD Tests

No external services required. From the repo root:

```bash
# Run only BDD tests
dotnet test src/ApiEndpoints/Minimal.App.BDDTests

# Run the full solution (includes BDD + unit + integration + architecture tests)
dotnet test src/DKNet.Templates.sln --settings src/coverage.runsettings
```

Expected output:
```
Passed!  - Failed: 0, Passed: 3, Skipped: 0, Total: 3
```

---

## Project Structure

```
src/ApiEndpoints/Minimal.App.BDDTests/
├── Minimal.App.BDDTests.csproj
├── GlobalUsings.cs
│
├── Support/
│   ├── BddApiFactory.cs          ← WebApplicationFactory with test overrides
│   ├── TestMembershipService.cs  ← IMembershipService stub ("TEST-MEM-{n}")
│   ├── ApiHooks.cs               ← Reqnroll [Binding]: lifecycle + DI wiring
│   └── ScenarioState.cs          ← per-scenario HTTP response state bag
│
└── Features/
    └── CustomerProfiles/
        ├── CreateCustomerProfile.feature
        └── Steps/
            └── CreateCustomerProfileSteps.cs
```

---

## Add a New BDD Scenario to an Existing Feature

1. Open the relevant `.feature` file, e.g. `Features/CustomerProfiles/CreateCustomerProfile.feature`
2. Add a new `Scenario:` block using the Gherkin language
3. If all steps are already bound (the IDE will highlight unbound steps in yellow), run `dotnet test` — done
4. If a new step is needed, add a new `[When]`/`[Then]`/`[Given]` method to the matching `Steps/*.cs` file using the Reqnroll attribute

Example — adding a "create profile and verify membership number" scenario:

```gherkin
Scenario: Membership number is assigned on creation
  When I send a create profile request with the following data:
    | Name      | Email                     | Phone        |
    | Test User | bdd.memno@example.com     | +6512300000  |
  Then the response should be successful
  And the membership number in the response should start with "TEST-MEM-"
```

New step binding in `CreateCustomerProfileSteps.cs`:

```csharp
[Then("the membership number in the response should start with {string}")]
public void ThenMembershipNumberStartsWith(string prefix)
{
    var doc = JsonDocument.Parse(_state.ResponseBody!);
    var membershipNo = doc.RootElement
        .GetProperty("value")
        .GetProperty("membershipNo")
        .GetString();
    membershipNo.ShouldStartWith(prefix);
}
```

---

## Add a New Feature File

1. Create a new `.feature` file under `Features/<DomainConcept>/`  
   Example: `Features/CustomerProfiles/DeleteCustomerProfile.feature`
2. Create matching step definitions at `Features/<DomainConcept>/Steps/<ActionName>Steps.cs`
3. Decorate the step class with `[Binding]` — Reqnroll auto-discovers all `[Binding]` classes in the assembly
4. Reuse existing steps from `CreateCustomerProfileSteps.cs` or `Support/CommonSteps.cs` where possible
5. Run `dotnet test` — the new scenarios appear automatically

---

## Architecture: How It Works

```
dotnet test
  └─ NUnit test runner starts
       └─ Reqnroll discovers all [Binding] classes
            └─ [BeforeTestRun] in ApiHooks
                 └─ new BddApiFactory() bootstraps Minimal.Api with:
                      - InMemory EF Core DB ("bdd-tests")
                      - RunDbMigrationWhenAppStart = false
                      - EnableAzureAppConfig = false
                      - IMembershipService = TestMembershipService
                 └─ factory.CreateClient() → HttpClient stored as static
            │
            └─ [BeforeScenario] in ApiHooks (per scenario)
                 └─ ResetDatabaseAsync() clears state from previous scenario
                 └─ registers HttpClient + new ScenarioState in IObjectContainer
            │
            └─ Reqnroll executes .feature file steps
                 └─ CreateCustomerProfileSteps receives HttpClient + ScenarioState
                      via constructor injection from IObjectContainer
            │
            └─ [AfterTestRun] in ApiHooks
                 └─ BddApiFactory.DisposeAsync()
```

---

## Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| `No matching step definition found` | Step text doesn't match any `[Given/When/Then]` attribute | Copy Reqnroll's suggestion from test output to add a new binding |
| `System.InvalidOperationException: UseInMemory` error | Missing `Microsoft.EntityFrameworkCore.InMemory` package reference | Ensure package is in `.csproj` |
| HTTP 401 on POST | Auth middleware active in test | Verify `FeatureManagement:RequireAuthorization = false` in `BddApiFactory.ConfigureWebHost` |
| HTTP 409 / idempotency replay | Same `X-Idempotency-Key` sent twice | Each `[When]` step must generate a fresh `Guid.NewGuid()` key |
| `NUnit.Framework` not found | Missing global using | Add `<Using Include="NUnit.Framework"/>` to `.csproj` `<ItemGroup>` |
