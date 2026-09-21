---
name: dknet-unit-tests
description: Write xUnit + Shouldly tests for a DKNet.Templates feature in <YourApp>.App.Tests — architecture/convention rules, pure functional tests (entity methods, validators, specs, mapping), and result-level integration tests against ApiFixture + IMessageBus (handler Result failures, EF persistence, domain events). Use after AppServices actions and endpoint config are ready. Covers only what xUnit owns; HTTP request/response and event-log scenarios belong in the `dknet-bdd-tests` skill. Invoke as `/dknet-unit-tests <Feature> <Entity> [mode=manual|auto]` to scaffold it for a feature.
metadata:
  kind: workflow
  arguments: "<Feature> <Entity> [mode=manual|auto]"
allowed-tools: Read, Grep, Glob, Edit, Write, Bash, Agent
---

Usage: `/dknet-unit-tests <Feature> <Entity> [mode=manual|auto]`

# xUnit tests (<YourApp>.App.Tests)

## Both shipped suites are teaching material — business tests only

`<YourApp>.App.Tests` and `<YourApp>.App.BDDTests` ship inside every generated solution. A team reads them
to learn how tests are written here, so every test must be about the business domain: the `PurchaseOrder`
(manual) and `Product` (automated) samples — entity invariants, validators, specs, handler results, CRUD
over HTTP, domain events.

Never add a test for platform plumbing to either suite: logging/telemetry, host/startup wiring, security
middleware (CORS, HSTS, rate limits, JWT config), health/Swagger endpoints, `FeatureOptions`↔appsettings
key binding, or template/scaffolding shape. Those belong in this repository's own hygiene tests, not in a
consumer's shipped example — if you find yourself testing framework or ASP.NET behavior instead of the
sample's business rules, stop and delete the test.

## Test layering — where a test belongs

**Business behavior goes to BDD first.** If a rule can be stated as "a caller does X and gets Y", it
belongs in `<YourApp>.App.BDDTests` as a scenario — that is the readable, business-facing record of
the rule. xUnit is for what a scenario cannot express or cannot distinguish.

xUnit owns three things; BDD must not re-cover them:

1. **Architecture/convention** — `Architecture/*`: NetArchTest + reflection over the compiled assembly
   (handler/validator classes are `internal sealed`, repo interfaces inherit `IRepository<T>`, DTOs never
   expose a domain entity type). Cannot be expressed as an HTTP scenario; never port to BDD.
2. **Pure functional** — `Unit/*`: entity methods, validators, spec predicate filters, static data. No
   host, no DB, no HTTP.
3. **Result-level integration** — `Integration/<Feature>/V1/*`: EF model/schema shape, and a handler
   failure asserted on the `IResult`/`IResultBase` object **only when the HTTP response cannot tell it
   apart from another failure**. Two rules that both answer `400`, or a `NotFoundError` versus an
   ownership filter that also returns `404`, need the `Result`-level assertion to pin *which* rule
   fired. A failure whose status code and message already identify it uniquely does not — write that
   one as a BDD scenario and leave it out of here.

BDD owns user-facing HTTP behavior (request → status → response body) and domain-event side effects
observed via log capture — which is most of a feature's business rules. Never assert the same rule in
both suites: pick the suite that states it best, and delete the other copy.

## Fixtures (`<YourApp>.App.TestSupport` + `Integration/Support`)

`TestApiFactoryBase(string? dbName) : WebApplicationFactory<<YourApp>.Api.Program>` (in
`<YourApp>.App.TestSupport`) is the shared host substitution both xUnit and BDD build on. It:

- Sets `FeatureManagement:RunDbMigrationWhenAppStart/EnableSwagger/EnableAzureAppConfig = false` and
  `ConnectionStrings:AppDb = UseInMemory`.
- Swaps `CoreDbContext` for EF Core InMemory via `AddDbContextWithHook` (plain `AddDbContext` would drop
  the DKNet events hook — `AddEvent`/`[RaisesEvent]` domain events would never publish).
- Replaces `IMembershipService` with `TestMembershipService`.
- Exposes `LogCapture` (a `TestLogCapture : ILoggerProvider` — captures every log line for assertions),
  `CreateScope()`, and `ResetDatabaseAsync()` (drops + recreates the InMemory DB and clears `LogCapture`).

`Integration/Support/ApiFixture : TestApiFactoryBase, IAsyncLifetime` is the plain fixture — no auth, no
extra overrides:

```csharp
public sealed class ApiFixture : TestApiFactoryBase, IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        _ = CreateClient();
        await ResetDatabaseAsync();
    }

    Task IAsyncLifetime.DisposeAsync() => Task.CompletedTask;
}
```

Use `IClassFixture<ApiFixture>` and call `fixture.ResetDatabaseAsync()` at the top of every `[Fact]` —
xUnit shares one fixture instance across all tests in the class, so each test resets state itself rather
than relying on fixture disposal.

**Two ways to exercise a feature:** through `IMessageBus` from `fixture.CreateScope()` — resolve
`IMessageBus` and (for seeding/assertion) `IRepositorySpec`, call `bus.Send(request)`, assert on the
returned `IResult<TDto>`/`IResultBase` — this is the default for result-level integration tests; or
through `HttpClient` (`fixture.CreateClient()`), only for a precondition branch with no BDD scenario of
its own (see `ProductPreconditionTests` below). Prefer BDD for anything HTTP-shaped.

**Auth fixture variants** (`Integration/Support/`), used only when a test needs the authenticated/authorized
path: `AuthOnApiFixture` flips `FeatureManagement:RequireAuthorization` on and swaps the JWT bearer scheme
for `TestAuthHandler` (fixed caller identity, no live token needed); `AuthOnFixedTenantApiFixture`,
`AuthOnMultiSubjectApiFixture`, `AuthOnNoNameClaimApiFixture`, `DemoAuthenticationOffApiFixture`,
`RequireAuthorizationPlusDemoApiFixture`, `VersioningOffApiFixture` cover narrower combinations — read the
closest one before writing a new fixture. `AuthOnApiFixture` sets its flags via
`Environment.SetEnvironmentVariable` in the constructor, not `AddFeatureOverrides` — `Program.cs` binds
`FeatureOptions` before the fixture's `ConfigureAppConfiguration` override is merged in, so only an
environment variable set before `WebApplication.CreateBuilder(args)` runs takes effect. Safe only because
`AssemblyInfo.cs` disables assembly-wide test parallelization.

Use `TestLogCapture` (via `fixture.LogCapture.Messages`) together with `Eventually.IsTrueAsync(...)` when
asserting a domain-event handler's side effect — the in-memory bus publishes with
`EnableBlockingPublish = false`, so a consumer's log line lands on a background task, not before the
command's response returns. Polling avoids a flaky race.

## Worked examples

**Result-level integration — happy path + not-found**
(`Integration/ManualSample/V1/PurchaseOrderActionsIntegrationTests.cs`):

```csharp
public sealed class PurchaseOrderActionsIntegrationTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    [Fact]
    public async Task Create_ShouldPersistOrder_AndReturnMatchingDto()
    {
        await fixture.ResetDatabaseAsync();
        using var scope = fixture.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var repository = scope.ServiceProvider.GetRequiredService<IRepositorySpec>();

        var result = await bus.Send(new CreatePurchaseOrderRequest
        {
            CustomerName = "Acme Pte Ltd", Amount = 250.00m, ByUser = "integration-test"
        });

        result.IsSuccess.ShouldBeTrue();
        result.Value!.CustomerName.ShouldBe("Acme Pte Ltd");

        var created = await repository.FirstOrDefaultAsync(
            new SpecGetPurchaseOrder(byCustomerName: "Acme Pte Ltd"), CancellationToken.None);
        created!.CreatedBy.ShouldBe("integration-test");
    }

    [Fact]
    public async Task Update_ShouldFail_WhenOrderNotFound()
    {
        await fixture.ResetDatabaseAsync();
        using var scope = fixture.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var result = await bus.Send(new UpdatePurchaseOrderRequest { Id = Guid.NewGuid(), Amount = 50m, ByUser = "integration-test" });

        result.IsFailed.ShouldBeTrue();
    }
}
```

The same class also proves a guarded state transition by calling the handler twice — `Cancel` once
succeeds, a second `Cancel` on the same order fails with the `precondition.purchase-order-already-cancelled`
message (`PreconditionCodes`, `<YourApp>.AppServices/Share/PreconditionCodes.cs`), and every mutating action
fails when `ByUser` is left empty (the acting-user rule the manual mode enforces in the handler itself).

**Precondition branch reachable only over HTTP from xUnit**
(`Integration/AutomatedSample/V1/ProductPreconditionTests.cs`):

```csharp
public sealed class ProductPreconditionTests(AuthOnApiFixture fixture) : IClassFixture<AuthOnApiFixture>
{
    [Fact]
    public async Task DeletingAnUnknownProduct_PassesThePreconditionAndStillAnswers404()
    {
        await fixture.ResetDatabaseAsync();
        var client = fixture.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/v1/products/{Guid.NewGuid()}");
        request.Headers.Add("X-Test-Scopes", "products.write");
        using var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
```

The comment on that class explains why: `DeleteProductRequestValidator` must let an unknown id pass its
precondition check, or the route's own 404 would be hidden behind a 409. BDD covers the "still for sale"
(409) and "discontinued" (204) branches of the same rule end to end; this is the one branch — the target
not existing at all — with no user-facing scenario of its own. Reach for `HttpClient` in xUnit only for a
gap shaped like this one, not as a default way to test a route.

**Pure functional — validators** (`Unit/ManualSample/PurchaseOrderValidatorsTests.cs`): construct the
`AbstractValidator<TRequest>` directly and call `.Validate(request)`, no host:

```csharp
private readonly CreatePurchaseOrderCommandValidator _createValidator = new();

[Fact]
public void CreateValidator_ShouldFail_WhenCustomerNameIsBlank()
{
    var result = _createValidator.Validate(new CreatePurchaseOrderRequest { CustomerName = "", Amount = 10m });
    result.IsValid.ShouldBeFalse();
}
```

That file also guards a real regression (DRK-714): `UpdateValidator_ShouldNotRejectEmptyId` — `Id` comes
from the route, not the body, so the validator must not fail on `Guid.Empty`; an unknown/empty id 404s
from the handler's spec lookup instead. Write the equivalent guard whenever a validator could accidentally
reject a route-bound id.

**Pure functional — specs** (`Unit/ManualSample/SpecGetPurchaseOrderTests.cs`): compile the spec's
`FilterQuery` and run it against in-memory instances, no DB:

```csharp
[Fact]
public void NoFilter_ShouldMatchEveryOrder()
{
    // Regression guard: an unstarted predicate builder compiles to WHERE FALSE (empty list page).
    var predicate = new SpecGetPurchaseOrder().FilterQuery!.Compile();
    predicate(MakeOrder("Acme")).ShouldBeTrue();
}
```

**Pure functional — entity methods, mode=auto** (`Unit/AutomatedSample/ProductTests.cs`):

```csharp
[Fact]
public void ChangePrice_ShouldUpdatePrice()
{
    var product = new Product("Widget", 9.99m);
    product.ChangePrice(12.50m);
    product.Price.ShouldBe(12.50m);
}

[Fact]
public void Discontinue_CalledTwice_ShouldStayDiscontinued_NotThrow()
{
    // The entity method itself stays idempotent — the refusal on a second discontinue is a business
    // rule enforced by DiscontinueProductCommandHandler, one layer up, not by this method.
    var product = new Product("Widget", 9.99m);
    product.Discontinue();
    product.Discontinue();
    product.IsDiscontinued.ShouldBeTrue();
}
```

The same file reflects over the `[CrudCreate]` constructor's parameters to prove the `DataAnnotations`
(`[Required]`, `[StringLength]`, `[Range]`) that the generator forwards onto the generated request are
actually present on the source the generator reads — grounding the claim without asserting on generated
code that has no committed source file.

**Static data seeder** (`Unit/ManualSample/PurchaseOrderStaticDataTests.cs`): a `DataSeedingConfiguration<T>`
seeder's `GetDataAsync` is `protected`, invoked only by DKNet's `UseAutoDataSeeding` pipeline — neither test
fixture wires that pipeline into its InMemory `DbContextOptions`, so it's otherwise never exercised. Invoke
it via reflection to prove the actual seed data instead of leaving the seeder at 0% coverage:

```csharp
var method = typeof(PurchaseOrderStaticData).GetMethod("GetDataAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
var orders = await (ValueTask<ICollection<PurchaseOrder>>)method.Invoke(new PurchaseOrderStaticData(), [CancellationToken.None])!;
orders.ShouldAllBe(o => o.CreatedBy == SharedConsts.SystemAccount);
```

**Architecture rule** (`Architecture/AppServiceTests.cs`), using NetArchTest against the compiled
`<YourApp>.AppServices` assembly:

```csharp
var result = Types.InAssembly(typeof(AppSetup).Assembly)
    .That().AreClasses().And().AreNotAbstract().And().ImplementInterface(typeof(IRequestHandler<,>))
    .Should().NotBePublic().And().BeSealed()
    .GetResult();
result.IsSuccessful.ShouldBeTrue();
```

## Mode differences

- **manual** (`ManualSample/PurchaseOrder`): test the hand-written `CreatePurchaseOrderCommandValidator`
  etc. directly; cover duplicate checks and rejected state transitions (`Cancel` twice) through the
  handler's `Result`; the acting-user rule (`ByUser` empty → fail) is enforced by the handler itself, so
  assert it there.
- **auto** (`AutomatedSample/Product`): no hand-written validator class exists for `[CrudCreate]`/
  `[CrudUpdate]` members — test the entity method directly (`ChangePrice`, `Discontinue`, `Approve`)
  instead. `DataAnnotations` on a `[CrudCreate]` parameter are forwarded onto the generated request but
  enforced only on a literal `Map*(string, Delegate)` route — **never assert 400 from a DataAnnotations
  violation on a generated CRUD route**; it returns 201/200 (`DKNet.AspCore.Extensions`'s generic
  `Map*<TRequest,TDto>` wrapper isn't visible to the validation source generator). A FluentValidation
  validator still runs on every route. Verify a declared event's composed name against the compiled
  assembly before asserting on it — `strings bin/**/<YourApp>.Domains.dll | grep <Entity>`.

## Conventions

- xUnit + Shouldly (`result.IsSuccess.ShouldBeTrue()`, not `Assert.True`). `<YourApp>.App.Tests.csproj`
  disables analyzers and warnings-as-errors — production code style rules do not apply here.
- Implicit usings from `GlobalUsings.cs`: `AutoBogus`, `Shouldly`, `System.Text.Json`, `MapsterMapper`,
  plus csproj-level `System.Net`, `Microsoft.Extensions.DependencyInjection`, `Xunit`. Still add explicit
  `using`s for `SlimMessageBus`, `DKNet.EfCore.Specifications*`, and your feature's `AppServices`/`Domains`
  namespaces.
- Reset the database at the top of every test (`await fixture.ResetDatabaseAsync()`), never in a shared
  constructor — the fixture instance is shared across the whole test class.
- Don't hand-roll a mock of `IRepositorySpec` or `IMapper`; resolve the real ones from
  `fixture.CreateScope()` against the InMemory provider — that is what proves DI wiring, not a substitute.
- Test classes/methods: `{Entity}{Concern}Tests`, `[Fact]` methods named `Method_ShouldOutcome_WhenCondition`.

## Commands

```bash
dotnet test ApiEndpoints/<YourApp>.App.Tests/<YourApp>.App.Tests.csproj --filter "FullyQualifiedName~PurchaseOrder"
dotnet test --settings coverage.runsettings --collect:"XPlat Code Coverage"
```

`coverage.runsettings` includes `[DKNet*]*` and `[<YourApp>*]*`, excludes `*.Tests`/`*Tests` assemblies and
`**/bin/**, **/obj/**, **/*Tests.cs, **/GlobalUsings.cs, **/*.g.cs` by file — don't put real logic in an
excluded path expecting it to be measured.

## Step-by-step

1. Decide the layer: architecture rule, pure functional (entity/validator/spec), or result-level
   integration. Don't write an HTTP test here unless it fills a genuine result-only/precondition gap BDD
   can't reach.
2. Pure functional: construct the validator/entity/spec directly, no fixture.
3. Result-level integration: pick `ApiFixture` (or an auth variant only if the scenario needs
   authentication/authorization), reset the DB, resolve `IMessageBus`/`IRepositorySpec` from
   `CreateScope()`, send the request, assert on `IResult`/`IResultBase`.
4. Cover: happy path, not-found, missing/invalid acting user (manual mode), a guarded state transition
   failing on retry, and (auto mode) the entity method's own behavior plus its forwarded
   `DataAnnotations`.
5. Run the filtered test, then the full suite with coverage before moving on.

## Common mistakes

- **What you might expect**: asserting `400` when a `[Range]` attribute on a `[CrudCreate]` parameter is
  violated on a generated route. **What actually happens**: it succeeds (201). **Why**: DataAnnotations are
  forwarded onto the generated request but only enforced when the route is a literal `Map*` call the .NET
  validation source generator can see in the compiling project; generated CRUD routes go through
  `DKNet.AspCore.Extensions`'s generic wrapper instead.
- **What you might expect**: a domain event handler's log line is present immediately after
  `bus.Send(...)` returns. **What actually happens**: it's sometimes missing. **Why**: the in-memory bus
  publishes with `EnableBlockingPublish = false`; poll with `Eventually.IsTrueAsync(...)` against
  `fixture.LogCapture.Messages` instead.
- **What you might expect**: an HTTP-status assertion in xUnit for a rule already covered by a BDD
  scenario is harmless extra coverage. **What actually happens**: duplicated, drifting coverage. **Why**:
  xUnit owns architecture/pure-functional/result-level assertions; BDD owns request→status→body.
- **What you might expect**: a new fixture mutating `Environment.SetEnvironmentVariable` for its own flag
  works standalone. **What actually happens**: it races other tests intermittently. **Why**: only
  `AssemblyInfo.cs`'s `DisableTestParallelization = true` makes that trick safe assembly-wide.

---

# Workflow: `/dknet-unit-tests`

The procedure an agent follows when invoked with arguments. The reference sections above are the rules it applies.

You are adding integration tests in `<YourApp>.App.Tests` that exercise the AppServices and Domains layers through the real DI container.

### Inputs

`$ARGUMENTS` — feature folder, entity, optional `mode=manual|auto`. If not supplied, detect it: a
`[CrudCreate]` on the entity means `auto`. The mode decides which of the cases below apply.

### Required reading

1. The reference sections above
2. `ApiEndpoints/<YourApp>.App.Tests/` — existing fixtures and test patterns (`Architecture/`, `Integration/`, `Unit/`).

### Steps

1. Use the `dknet-implementer` subagent (or follow the skill directly) to write tests covering.

   Both modes:
   - Happy-path Create / Update / Delete via `IMessageBus.Send(...)`.
   - Not-found in Update + Delete.
   - Domain event firing — assert the in-memory consumer ran. In `auto`, send to the **composed**
     event name (`<Entity><NarrowingProps><Operation>Event`, e.g. `ProductPriceUpdatedEvent`), which
     you verify against the compiled assembly, not by guessing.
   - Mapster smoke test (entity → DTO field-for-field).

   `mode=manual` only:
   - FluentValidation failures (empty / too-long / invalid format) on Create + Update.
   - Duplicate detection in Create handler.
   - Rejected state transitions (`Result.Fail`) on any business action.

   `mode=auto` only:
   - Entity mutation methods tested directly — that is where the behavior lives.
   - **Do NOT** write a test asserting a `400`/validation failure from a forwarded DataAnnotations
     attribute on a generated request. It is never enforced under this template's endpoint
     convention, so such a test either fails or, worse, gets "fixed" by relaxing it into asserting
     the gap is correct. Note the gap in the report instead.
2. Run only the affected tests:
   ```
   dotnet test ApiEndpoints/<YourApp>.App.Tests/<YourApp>.App.Tests.csproj --filter "FullyQualifiedName~<Entity>"
   ```
3. If any test fails, fix the test or product code (per skill guidance) — do not relax assertions.
4. Report: test file path, count, pass/fail, coverage areas hit.

### Constraints

- Tests use the real `ApiFixture` + DI container — no hand-rolled mocks for `IRepositorySpec` or `IMapper`.
- xUnit + Shouldly. Assertions: `result.IsSuccess.ShouldBeTrue()`, `result.Value.X.ShouldBe(...)`, `result.Errors.ShouldContain(e => ...)`.
- Reset DB state between tests (per the skill's fixture pattern). Don't leak state between cases.
