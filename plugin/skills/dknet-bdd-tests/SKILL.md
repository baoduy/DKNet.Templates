---
name: dknet-bdd-tests
description: Create and maintain Reqnroll + NUnit BDD .feature scenarios and step bindings in Minimal.App.BDDTests — request/status/response-body scenarios and domain-event side effects observed via log capture, for the hand-written PurchaseOrder and generator-driven Product samples. Use when adding or updating HTTP-facing scenarios for a DKNet.Templates feature. Result-level Result-object assertions, architecture rules and pure functional tests belong in the `dknet-unit-tests` skill instead — do not duplicate a behavior here that xUnit already covers. Invoke as `/dknet-bdd-tests <Feature>` to scaffold it for a feature.
metadata:
  kind: workflow
  arguments: "<Feature> e.g. Orders"
allowed-tools: Read, Grep, Glob, Edit, Write, Bash
---

Usage: `/dknet-bdd-tests <Feature> e.g. Orders`

# BDD tests (Minimal.App.BDDTests)

## What BDD owns vs xUnit

BDD owns user-facing HTTP behavior: request → status code → response body, and domain-event side effects
observed through captured log lines. xUnit (`dknet-unit-tests`) owns architecture/convention rules, pure
functional tests (entity methods, validators, specs), and result-level integration assertions on the
handler's `IResult`/`IResultBase` object. Schema/model/migration assertions never belong in BDD. Don't
write a BDD scenario for a rule already proven at the `Result` level in xUnit unless it's the one place
that rule is reachable over HTTP — and don't add a duplicate xUnit HTTP test for a rule a BDD scenario
already proves.

Both shipped suites are teaching material: every scenario must be about the `PurchaseOrder` (manual) or
`Product` (automated) sample's business behavior, never about logging, health checks, CORS, security
headers, rate limits, or config binding.

## Infrastructure

`Support/BddApiFactory : TestApiFactoryBase("bdd-tests")` (built on the same base as the xUnit suite's
`ApiFixture` — see `dknet-unit-tests` for what the base class provides) overrides two things:

- `AddFeatureOverrides` sets `FeatureManagement:RequireAuthorization = "false"` unconditionally, and
  `ConnectionStrings:Redis` only for the one `@redis`-tagged scenario.
- `ConfigureTestServices` swaps in `UnexpectedErrorTriggerMapper` (an `IMapper` that throws when mapping a
  `PurchaseOrder` named `"__drk1515-unexpected-error-trigger__"`, the suite's only reachable seam for a
  genuine unhandled exception through a real route) and, for the `@redis` scenario, swaps the idempotency
  key store for the Redis-backed one.

`Support/ApiHooks` is a static `[Binding]` class:

```csharp
[BeforeTestRun]
public static void BeforeTestRun()
{
    _factory = new BddApiFactory();
    _client = _factory.CreateClient();
}

[BeforeScenario(Order = 0)]
public async Task BeforeScenarioAsync()
{
    await _factory.ResetDatabaseAsync();
    _factory.LogCapture.Clear();
    objectContainer.RegisterInstanceAs<HttpClient>(_client);
    objectContainer.RegisterInstanceAs(_factory);
    objectContainer.RegisterInstanceAs(new ScenarioState());
}
```

The host boots once for the whole run; every scenario gets a reset database, a cleared `LogCapture`, and
fresh `ScenarioState`. **Never add a second `[BeforeTestRun]`** — Reqnroll runs it at assembly level, and a
second one races the shared host's startup/teardown.

`Support/ScenarioState` is a plain bag (`Response`, `ResponseBody`) a step class writes to and later steps
read from — inject it by constructor, same as `HttpClient` and `BddApiFactory`.

`Support/CommonSteps` holds steps shared by more than one feature so exact wording isn't duplicated (which
Reqnroll would flag as ambiguous): `the request is rejected`, `the response status is (\d+)`, `the response
is (\d+)` (a second wording for the same assertion — the acceptance criteria used frozen text, not
paraphrased to match house style), `catalogue-ops holds the scope "..."` /
`holds every scope the operation needs` / `is signed in` (no-ops — the BDD host runs with
`RequireAuthorization = false`, so no policy is ever evaluated; scope-gated behavior is xUnit's
`AuthOnApiFixture` territory), `the response body carries a trace identifier`, `... carries a code naming
the rule that refused` (asserts the `precondition.` prefix plus a rule segment, not just a non-empty
string), and `the response names the field it refused`. Reuse these before writing a new one with the same
meaning.

## Layout

```
Features/<Domain>/<Name>.feature
Features/<Domain>/Steps/<Name>Steps.cs
```

Each step class is `[Binding]`, constructor-injects `HttpClient client, ScenarioState state, BddApiFactory
factory` (and any other step class it needs data from — `ProductSteps` injects `PurchaseOrderSteps` to look
up a purchase order id created cross-feature). Step regex must match feature text exactly; a phrase
typo'd between the `.feature` and the `[Given]/[When]/[Then]` attribute leaves the step undefined at run
time, not a compile error. Tag scenarios by domain (`@PurchaseOrder`, `@Product`) and, for cross-cutting
integration scenarios, `@integration`, filterable with `--filter "TestCategory=PurchaseOrder"`. Use
`Scenario Outline` + `Examples` for a validation table (several inputs, one shared assertion shape) instead
of repeating near-identical scenarios.

## Worked example — PurchaseOrder (manual mode, full feature)

`Features/PurchaseOrders/PurchaseOrder.feature`:

```gherkin
@PurchaseOrder
Feature: Purchase order lifecycle (manual sample)
  Background:
    Given the service is running with no Redis connection configured

  Scenario: Creating a purchase order persists it and returns its details
    When I create a purchase order for customer "Acme Pte Ltd" with amount 250.00
    Then the response status is 201
    And the purchase order response has customer name "Acme Pte Ltd" and amount 250.00
    And the purchase order response status is "placed"

  Scenario: Replaying the same idempotency key on create does not create a second order
    When I create a purchase order for customer "Acme Pte Ltd" with amount 250.00 using idempotency key "11111111-1111-1111-1111-111111111111"
    And I replay the same create request with idempotency key "11111111-1111-1111-1111-111111111111"
    Then both responses report the same purchase order id

  Scenario: Cancelling a purchase order succeeds once and fails the second time
    Given a purchase order exists for customer "Initech LLC" with amount 50.00
    When I cancel that purchase order
    Then the response status is 200
    When I cancel that purchase order again
    Then the response status is 409

  Scenario Outline: Creating a purchase order rejects invalid input
    When I create a purchase order for customer "<customerName>" with amount <amount>
    Then the response status is 400
    Examples:
      | customerName | amount |
      |               | 100.00 |
      | Acme Pte Ltd | 0      |

  Scenario: Creating a purchase order without an idempotency key is rejected
    When I create a purchase order for customer "Acme Pte Ltd" with amount 100.00 without an idempotency key
    Then the request is rejected
```

Key step bodies from `Steps/PurchaseOrderSteps.cs` — a fresh `Guid.NewGuid()` per plain create, a caller
supplied key for the idempotency scenarios, and the header on every create:

```csharp
[When(@"I create a purchase order for customer ""(.*)"" with amount (.*)")]
public Task WhenICreateAPurchaseOrder(string customerName, decimal amount) =>
    CreateAsync(customerName, amount, Guid.NewGuid().ToString());

private async Task CreateAsync(string customerName, decimal amount, string idempotencyKey)
{
    using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/purchase-orders")
    {
        Content = JsonContent.Create(new { customerName, amount })
    };
    request.Headers.Add("X-Idempotency-Key", idempotencyKey);
    state.Response = await client.SendAsync(request);
    state.ResponseBody = await state.Response.Content.ReadAsStringAsync();
    if (state.Response.IsSuccessStatusCode)
    {
        var dto = JsonSerializer.Deserialize<PurchaseOrderDto>(state.ResponseBody, SharedConsts.JsonSerializerOptions);
        _lastId = dto!.Id;
    }
}
```

Update/cancel/delete/get reuse `_lastId` from the last create; list deserializes the response as a bare
JSON array (`List<PurchaseOrderDto>`, the hand-mapped route's own shape) via
`SharedConsts.JsonSerializerOptions` — always deserialize through that options instance, not a fresh
default one, so casing matches what the API actually emits.

## Worked example — Product (automated mode: event log + precondition 409)

`Features/Products/Product.feature` proves the generator-driven CRUD slice. Two scenarios worth reading in
full:

```gherkin
Scenario: Creating a product raises the internal ProductCreatedEvent
  When I create a product named "Widget" with price 9.99
  Then a log line reports the automated sample product was created

@integration
Scenario: A product name that is already taken is refused
  Given catalogue-ops holds the scope "products.write"
  And the product "Widget" priced 9.99 SGD exists
  When catalogue-ops creates the product "Widget" priced 12.00 SGD
  Then the response is 409
  And exactly 1 product is named "Widget"
```

The log-line step just asserts on `LogCapture.Messages` — no polling needed here because the response body
already round-trips through the handler that logs synchronously before returning; if a future scenario
asserts on an *async consumer's* log line instead (a domain event's own subscriber, not the handler that
raised it), poll with `Eventually.IsTrueAsync(...)` from `Minimal.App.TestSupport` rather than asserting
immediately — the in-memory bus publishes non-blocking:

```csharp
[Then("a log line reports the automated sample product was created")]
public void ThenALogLineReportsTheAutomatedSampleProductWasCreated() =>
    factory.LogCapture.Messages.ShouldContain(m => m.Contains("AutomatedSample product created", StringComparison.Ordinal));
```

Product create sends **no** `X-Idempotency-Key` header at all — the generated create route has no
`.RequiredIdempotentKey()` call, so a replayed request is a fresh create, not a replay. Never assert
idempotent-replay behavior on a generated create route.

Another scenario in the same file documents a real, permanent gap rather than a bug to fix:

```gherkin
Scenario: A negative price is still accepted — a known and accepted limitation of the generated path
  When I create a product named "Broken Widget" with price -1
  Then the response status is 201
```

`[Range(0.01, double.MaxValue)]` is forwarded onto the generated request but never evaluated, because the
.NET 10 minimal-API validation source generator can't see through `DKNet.AspCore.Extensions`'s generic
`MapPost<TRequest,TResponse>` wrapper. **Never assert 400 for a DataAnnotations violation on a generated
create/update route** — write the scenario the way this one is written, asserting the (surprising) 201.

`ProductList.feature` is a regression fence around the generic `MapGetList<Product, Guid, ProductDto>()`
list route: paging envelope (`totalItemCount`/`hasNextPage`/`hasPreviousPage`), `pageSize` clamped (not
rejected) above the max, `orderBy`/`desc`, `filter=field:op:value` (including the `In` operator and
multiple filters ANDed), `search` (substring, 2-char minimum), and two security-boundary scenarios: an
unknown filter field 400s, and filtering by an excluded DTO column (`ownedBy`, excluded via
`[GenerateDto(... Exclude ...)]`) 400s rather than silently matching against the underlying entity. Summarize
new list-contract scenarios the same way when a feature's list route gains its own filters or computed DTO
fields.

## Assertion depth

Assert status code, then the fields the scenario is actually about — not just "success". The manual
sample's error body shape: `errors[]` (each with `message`, optional `code`/`field`), `code`, `traceId`
(see `CommonSteps`'s trace-identifier and precondition-code steps above). Prefer a structured JSON property
check (`JsonDocument.Parse(...).RootElement.GetProperty(...)`) over a raw substring match on the response
body when the contract has a real shape to check against; a substring check is fine for a fixed literal
like `"status":"placed"` where the surrounding shape isn't in question.

## Mode differences

- **manual** (PurchaseOrder): create requires `X-Idempotency-Key`; a missing key is rejected, a replayed
  key returns the same order id. Acting user comes from `[FromClaim(ClaimTypes.Name)] ByUser`.
- **auto** (Product): create has no idempotency key and does not enforce DataAnnotations — never write a
  scenario expecting either. The acting user for audit stamps comes from the demo identity: the BDD host
  runs with `EnableDemoAuthentication = true` (`appsettings.Testing.json`) and
  `RequireAuthorization = false` (`BddApiFactory`), so every request is the built-in demonstration caller,
  not an anonymous one — `catalogue-ops holds the scope "..."` steps are no-ops for exactly that reason.

## Commands

```bash
dotnet test ApiEndpoints/Minimal.App.BDDTests/Minimal.App.BDDTests.csproj --filter "TestCategory=PurchaseOrder"
```

## Step-by-step

1. Confirm the behavior is HTTP-shaped (request → status → body) or an event side-effect via log capture —
   otherwise it belongs in `dknet-unit-tests`.
2. Add `Features/<Domain>/<Name>.feature` under the right tag; reuse `CommonSteps` wording before coining
   new phrasing for something already covered (rejected request, status code, trace id, precondition code).
3. Add `Features/<Domain>/Steps/<Name>Steps.cs`, `[Binding]`, constructor-inject `HttpClient`,
   `ScenarioState`, `BddApiFactory`, and any other step class whose state you need.
4. Manual-mode create: generate a fresh `Guid.NewGuid()` for `X-Idempotency-Key` in each `[When]` step that
   creates a resource, unless the scenario is specifically about idempotency (use a fixed key there).
5. Cover: happy path, not-found, a guarded transition refused on retry, and (auto mode) the event's log
   line and the generated route's un-enforced-DataAnnotations behavior where relevant.
6. Run the filtered test for the tag before the full BDD project.

## Common mistakes

- **What you might expect**: a generated Product create route rejects an idempotency replay like
  PurchaseOrder does. **What actually happens**: it creates a second product. **Why**: the generated route
  has no `.RequiredIdempotentKey()` call — there is nothing to replay against.
- **What you might expect**: a negative price on `Product` create returns 400. **What actually happens**:
  201. **Why**: the forwarded `[Range]` DataAnnotation is never evaluated on a generic-wrapper route — this
  is documented, accepted behavior, not a bug to "fix" with a failing scenario.
- **What you might expect**: asserting a log line right after the response comes back is safe. **What
  actually happens**: it's sometimes missing for an async consumer. **Why**: the in-memory bus publishes
  non-blocking; poll instead of asserting immediately when the log line comes from a separate event
  consumer rather than the handling code itself.
- **What you might expect**: adding a second `[BeforeTestRun]` hook in a new feature's step file to set up
  feature-specific state. **What actually happens**: it races `ApiHooks`'s host bootstrap. **Why**: Reqnroll
  runs every `[BeforeTestRun]` at assembly level with no defined ordering guarantee between classes; put
  one-time setup in `ApiHooks` or in `[BeforeScenario]` instead.
- **What you might expect**: forgetting `X-Idempotency-Key` on a manual-sample create just needs a retry.
  **What actually happens**: the request is rejected outright (400/`the request is rejected`) — the header
  is required, not merely deduplicating.

---

# Workflow: `/dknet-bdd-tests`

The procedure an agent follows when invoked with arguments. The reference sections above are the rules it applies.

You are DKNet BDD Test Engineer.

Your job is to create, update, and validate BDD scenarios for this repository with contract-first assertions and deterministic step bindings.

### User Input

$ARGUMENTS

### Required Skill Loading

Before any BDD design or edits:
1. Load and follow the BDD skill at the reference sections above.
2. Use this skill's `checklist.md` as the completion gate.

### Scope

Work only on BDD test artifacts and closely related support wiring:
- `ApiEndpoints/Minimal.App.BDDTests/Features/**/*.feature`
- `ApiEndpoints/Minimal.App.BDDTests/Features/**/Steps/*.cs`
- `ApiEndpoints/Minimal.App.BDDTests/Support/*.cs`
- `ApiEndpoints/Minimal.App.BDDTests/*.csproj`

### Constraints

- Use `specs/<feature>/contracts/*` as the assertion source of truth.
- Treat `docs/features/**` and `specs/**` as reference context for scenario coverage and wording.
- Keep step phrases and `[Given]/[When]/[Then]` attributes exactly matched.
- Validate response at three levels whenever applicable:
  - HTTP status code
  - response structure (`isSuccess`, `value`, `errors`, required objects/arrays)
  - key data fields and expected values
- Use `SharedConsts.JsonSerializerOptions` for request serialization.
- Include required request headers when contracts require them. A **manual-flow** create route
  requires a fresh `Guid.NewGuid()` `X-Idempotency-Key` per `[When]` step; an **automated-flow**
  generated create route has no idempotency filter, so do not assert replay behavior against it.
- Assert only behavior the endpoint actually has. An automated-flow route does not enforce its
  forwarded DataAnnotations — a scenario expecting `400` from an out-of-range value will fail against
  a `201`. Cover that gap by asserting what happens, or leave it to the manual flow.
- Do not implement unrelated domain/business logic outside BDD test scope.

### Workflow

1. Build context map from:
   - `docs/features/<feature>/`
   - `specs/<feature>/spec.md`
   - `specs/<feature>/contracts/*`
2. Produce or update `.feature` scenarios:
   - Happy path
   - Business-rule failure
   - Validation failure
3. Implement/adjust step bindings in `Steps/*.cs`.
4. Run validation:
   - `dotnet build -c Release`
   - `dotnet test ApiEndpoints/Minimal.App.BDDTests`
5. Report:
   - changed files
   - scenario count
   - pass/fail results
   - unresolved contract gaps (if any)

### Output Format

Always provide:
1. BDD phase status
2. Artifacts changed
3. Assertion coverage summary (status + shape + key fields)
4. Test results summary
5. Remaining risks or blockers
