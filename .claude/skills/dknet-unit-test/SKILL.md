---
name: dknet-unit-test
description: >-
  Write integration tests for a DKNet.Templates feature using the ApiFixture + IMessageBus pattern.
  Tests verify DI wiring, command/query handling, FluentValidation, EF Core persistence, and domain
  event execution in a single test class. Use after AppServices actions and endpoint config are ready.
---

# Skill: Integration Tests (ApiFixture + IMessageBus)

Write integration tests that exercise the full vertical slice — from DI registration through the message bus, validation, persistence, and domain events — using a real in-memory database and the `WebApplicationFactory` fixture.

---

## When to Use

- After completing dknet-appservices-actions (and optionally dknet-endpoint-config)
- Adding tests for Create / Update / Delete / Query actions on a new feature
- Verifying that FluentValidation rules catch invalid input
- Verifying that domain events fire and are handled correctly

## What a Single Test Proves

Because tests run through the full DI container and a real EF Core context, each test simultaneously verifies:

| Concern | How it's covered |
|---|---|
| DI registration | `GetRequiredService<IMessageBus>()` throws if anything is missing |
| Command/query handling | `bus.Send(request)` routes to the correct handler |
| FluentValidation | Invalid requests return `IsFailed` with validation error messages |
| EF Core persistence | `repository.FirstOrDefaultAsync(spec)` confirms data (SaveChanges handled by IMessageBus middleware) |
| Domain events | Event handlers run in the same scope; side-effects can be asserted |
| SlimMessageBus middleware | All registered behaviors execute in the pipeline |

Additionally, tests should include at least one DTO consistency check when the feature uses generated DTOs:
- verify key response fields expected from the entity mapping are present and correctly populated
- catch request/response drift introduced by manual record edits

## Inputs Required

1. **Feature name** and entity class (e.g., `ManualSample` / `PurchaseOrder`, `AutomatedSample` / `Product`)
2. **AppServices request types** for the feature (Create / Update / Cancel-or-other-transition / Delete / Query requests) — or none at all, if the entity uses `[CrudCreate]`/`[CrudUpdate]` and the layer is generated (see `dknet-appservices-actions`)
3. **Spec class** for querying the entity (e.g., `SpecGetPurchaseOrder`) — may not exist if reads go through the generic `MapGetById`/`MapGetList`
4. **Domain entity constructor** signature (to seed test data directly)
5. **Business rules** to cover: duplicate checks, not-found paths, validation failures, guarded state transitions (e.g. `PurchaseOrder.Cancel` rejecting an already-cancelled order)

---

## Project Conventions

### Test Project Structure

```
ApiEndpoints/Minimal.App.Tests/
├── GlobalUsings.cs                         ← AutoBogus, Shouldly, JsonSerializer, IMapper
├── Integration/
│   ├── Support/
│   │   ├── ApiFixture.cs                   ← DO NOT MODIFY (shared base fixture)
│   │   └── TestMembershipService.cs        ← DO NOT MODIFY
│   └── {Feature}/
│       └── V{N}/
│           └── {Entity}ActionsIntegrationTests.cs   ← CREATE THIS
└── Unit/                                   ← LazyMapper tests only
```

### ApiFixture (already exists — do not recreate)

`ApiFixture` is `WebApplicationFactory<Minimal.Api.Program>` + `IAsyncLifetime`.

Key methods:
- `fixture.CreateScope()` → returns an `IServiceScope` (always `using`)
- `fixture.ResetDatabaseAsync()` → deletes + recreates in-memory DB
- `fixture.Services` → singleton service provider (for mapper, options, etc.)

Key test configuration applied by the fixture:
- `FeatureManagement:RunDbMigrationWhenAppStart = false`
- `FeatureManagement:EnableSwagger = false`
- `FeatureManagement:EnableAzureAppConfig = false`
- `ConnectionStrings:AppDb = UseInMemory`
- `IMembershipService` replaced by `TestMembershipService` (returns `TEST-MEM-000001`, etc.)

### Global Usings (already available in test project)

```csharp
global using AutoBogus;
global using Shouldly;
global using System.Text.Json;
global using MapsterMapper;
```

You still need explicit `using` for:
- `SlimMessageBus` (for `IMessageBus`)
- `DKNet.EfCore.Specifications` + `DKNet.EfCore.Specifications.Extensions` (for `IRepositorySpec`)
- Your feature's AppServices namespaces

---

## Step-by-Step

### Step 1: Create the Test Class File

Create `ApiEndpoints/Minimal.App.Tests/Integration/{Feature}/V1/{Entity}ActionsIntegrationTests.cs`:

```csharp
using DKNet.EfCore.Specifications;
using DKNet.EfCore.Specifications.Extensions;
using Minimal.App.Tests.Integration.Support;
using Minimal.AppServices.{Feature}.V1;
using Minimal.AppServices.{Feature}.V1.Actions;
using Minimal.AppServices.{Feature}.V1.Specs;
using Minimal.Domains.Features.{Feature}.Entities;
using SlimMessageBus;

namespace Minimal.App.Tests.Integration.{Feature}.V1;

public sealed class {Entity}ActionsIntegrationTests({Entity}Fixture fixture)
    : IClassFixture<{Entity}Fixture>;
```

> **Fixture choice**: If the feature has no special service overrides, use `ApiFixture` directly instead of a dedicated `{Entity}Fixture`. Create a per-feature fixture only when you need additional service replacements.

### Step 2: Create a Per-Feature Fixture (optional)

Only needed when you must replace extra domain services beyond what `ApiFixture` already handles.

```csharp
namespace Minimal.App.Tests.Integration.{Feature}.V1;

/// <summary>
/// Fixture for {Entity} integration tests.
/// Inherits all ApiFixture behaviour; add feature-specific overrides here.
/// </summary>
public sealed class {Entity}Fixture : ApiFixture
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);   // ← always call base first

        builder.ConfigureServices(services =>
        {
            // Replace additional domain services if needed:
            // services.RemoveAll<I{DomainService}>();
            // services.AddSingleton<I{DomainService}, Test{DomainService}>();
        });
    }
}
```

If no extra overrides are needed, skip this step and use `ApiFixture` directly.

### Step 3: Write the Happy-Path Create Test

```csharp
[Fact]
public async Task Create{Entity}ShouldPersistSuccessfully()
{
    await fixture.ResetDatabaseAsync();                              // isolate DB state

    using var scope = fixture.CreateScope();
    var bus        = scope.ServiceProvider.GetRequiredService<IMessageBus>();
    var repository = scope.ServiceProvider.GetRequiredService<IRepositorySpec>();

    var request = new Create{Entity}Request
    {
        {RequiredField1} = "{test-value-1}",
        {RequiredField2} = "{test-value-2}",
        ByUser = "integration-test"
    };

    var result = await bus.Send(request);

    // Assert handler succeeded
    result.IsSuccess.ShouldBeTrue();

    var created = await repository.FirstOrDefaultAsync(
        new SpecGet{Entity}(by{UniqueField}: request.{UniqueField}),
        CancellationToken.None);

    created.ShouldNotBeNull();
    created.{Field1}.ShouldBe(request.{Field1});
    created.{Field2}.ShouldBe(request.{Field2});

    // Optional but recommended when using [GenerateDto]:
    // var dto = result.Value;
    // dto.ShouldNotBeNull();
    // dto.{Field1}.ShouldBe(created.{Field1});
}
```

### Step 4: Write the Duplicate / Business-Rule Failure Test

```csharp
[Fact]
public async Task Create{Entity}ShouldFailWhen{UniqueField}AlreadyExists()
{
    await fixture.ResetDatabaseAsync();

    using var scope = fixture.CreateScope();
    var bus        = scope.ServiceProvider.GetRequiredService<IMessageBus>();
    var repository = scope.ServiceProvider.GetRequiredService<IRepositorySpec>();

    // Seed the duplicate directly via repository (bypasses bus)
    await repository.AddAsync(
        new {Entity}("{seed-field1}", "{duplicate-unique-value}", "{seed-user}"),
        CancellationToken.None);
    await repository.SaveChangesAsync(CancellationToken.None);

    var request = new Create{Entity}Request
    {
        {UniqueField} = "{duplicate-unique-value}",   // same as seeded record
        {OtherField}  = "{other-value}",
        ByUser = "integration-test"
    };

    var result = await bus.Send(request);

    result.IsFailed.ShouldBeTrue();
    result.Errors.Select(x => x.Message)
        .ShouldContain("{UniqueField} {duplicate-unique-value} already exists.");
}
```

### Step 5: Write the Happy-Path Update Test

```csharp
[Fact]
public async Task Update{Entity}ShouldPersistChanges()
{
    await fixture.ResetDatabaseAsync();

    using var scope = fixture.CreateScope();
    var bus        = scope.ServiceProvider.GetRequiredService<IMessageBus>();
    var repository = scope.ServiceProvider.GetRequiredService<IRepositorySpec>();

    // Seed the entity to update
    var entity = new {Entity}("{initial-field1}", "{initial-unique}", "{initial-other}", "seed");
    await repository.AddAsync(entity, CancellationToken.None);
    await repository.SaveChangesAsync(CancellationToken.None);

    var request = new Update{Entity}Request
    {
        Id     = entity.Id,
        {MutableField1} = "{new-value-1}",
        {MutableField2} = "{new-value-2}",
        ByUser = "integration-test"
    };

    var result = await bus.Send(request);

    result.IsSuccess.ShouldBeTrue();

    var updated = await repository.FirstOrDefaultAsync(
        new SpecGet{Entity}(entity.Id),
        CancellationToken.None);

    updated.ShouldNotBeNull();
    updated.{MutableField1}.ShouldBe("{new-value-1}");
    updated.{MutableField2}.ShouldBe("{new-value-2}");
}
```

### Step 6: Write the Not-Found Update Test

```csharp
[Fact]
public async Task Update{Entity}ShouldFailWhenEntityNotFound()
{
    await fixture.ResetDatabaseAsync();

    using var scope = fixture.CreateScope();
    var bus       = scope.ServiceProvider.GetRequiredService<IMessageBus>();
    var missingId = Guid.NewGuid();

    var result = await bus.Send(new Update{Entity}Request
    {
        Id     = missingId,
        {MutableField1} = "{any-value}",
        ByUser = "integration-test"
    });

    result.IsFailed.ShouldBeTrue();
    result.Errors.Select(x => x.Message)
        .ShouldContain($"The {Entity} {missingId} is not found.");
}
```

### Step 7: Write the Happy-Path Delete Test

```csharp
[Fact]
public async Task Delete{Entity}ShouldRemoveEntity()
{
    await fixture.ResetDatabaseAsync();

    using var scope = fixture.CreateScope();
    var bus        = scope.ServiceProvider.GetRequiredService<IMessageBus>();
    var repository = scope.ServiceProvider.GetRequiredService<IRepositorySpec>();

    // Seed entity to delete
    var entity = new {Entity}("{field1}", "{unique}", "{other}", "seed");
    await repository.AddAsync(entity, CancellationToken.None);
    await repository.SaveChangesAsync(CancellationToken.None);

    var result = await bus.Send(new Delete{Entity}Request { Id = entity.Id });

    result.IsSuccess.ShouldBeTrue();

    var deleted = await repository.FirstOrDefaultAsync(
        new SpecGet{Entity}(entity.Id),
        CancellationToken.None);

    deleted.ShouldBeNull();
}
```

### Step 8: Write the Invalid-Id Delete Test

```csharp
[Fact]
public async Task Delete{Entity}ShouldFailWhenIdIsEmpty()
{
    await fixture.ResetDatabaseAsync();

    using var scope = fixture.CreateScope();
    var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

    var result = await bus.Send(new Delete{Entity}Request { Id = Guid.Empty });

    result.IsFailed.ShouldBeTrue();
    result.Errors.Select(x => x.Message).ShouldContain("The Id is in valid.");
}
```

### Step 9: Write FluentValidation Tests

Test that invalid requests are rejected **before** hitting the handler. Each validation rule should have a dedicated test.

```csharp
[Fact]
public async Task Create{Entity}ShouldFailValidationWhen{Field}IsEmpty()
{
    await fixture.ResetDatabaseAsync();

    using var scope = fixture.CreateScope();
    var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

    var request = new Create{Entity}Request
    {
        {RequiredField} = string.Empty,   // deliberately invalid
        {OtherRequiredField} = "{valid-value}",
        ByUser = "integration-test"
    };

    var result = await bus.Send(request);

    result.IsFailed.ShouldBeTrue();
    // FluentValidation errors surface in result.Errors
    result.Errors.Select(x => x.Message).ShouldNotBeEmpty();
}
```

### Step 10: (Optional) Add a Mapping Smoke Test

Confirm that Mapster is correctly configured for the entity → DTO mapping:

```csharp
[Fact]
public void {Entity}MappingShouldProduceValidDto()
{
    var entity = new AutoFaker<{Entity}>()
        .CustomInstantiator(f => new {Entity}(
            f.{Field1Generator}(),
            f.{Field2Generator}(),
            // ... match the constructor signature
            f.Internet.UserName()))
        .Generate();

    var mapper = fixture.Services.GetRequiredService<IMapper>();
    var dto = mapper.Map<{Entity}Dto>(entity);

    dto.ShouldNotBeNull();
    dto.Id.ShouldBe(entity.Id);
}
```

---

## Reference: the committed suite for PurchaseOrder / Product

Both samples already have tests. **Read them before writing new ones** — mirror their shape rather
than inventing a parallel structure:

| File | Tests |
|---|---:|
| `Minimal.App.Tests/Integration/ManualSample/V1/PurchaseOrderActionsIntegrationTests.cs` | 13 |
| `Minimal.App.Tests/Integration/ManualSample/V1/PurchaseOrderSecurityTests.cs` | 3 |
| `Minimal.App.Tests/Integration/ManualSample/V1/PurchaseOrderListPagingTests.cs` | 3 |
| `Minimal.App.Tests/Unit/ManualSample/` (entity, validators, spec, static data) | 16 |
| `Minimal.App.Tests/Integration/AutomatedSample/V1/ProductSecurityTests.cs` | 4 |
| `Minimal.App.Tests/Integration/AutomatedSample/V1/ProductOwnershipIsolationTests.cs` | 3 |
| `Minimal.App.Tests/Unit/AutomatedSample/` (entity, notification handler) | 8 |

`PurchaseOrderActionsIntegrationTests` is the canonical exemplar — a
`<Entity>ActionsIntegrationTests(ApiFixture fixture) : IClassFixture<ApiFixture>` class resolving
`IMessageBus` and `IRepositorySpec` from the same `fixture.CreateScope()`. What it covers:

| Test method | What it proves |
|------|---------------|
| `Create_ShouldPersistOrder_AndReturnMatchingDto` | `bus.Send(new CreatePurchaseOrderRequest{...})` persists via `repository.AddAsync`; returns the mapped DTO |
| `Create_ShouldFail_WhenByUserIsMissing` | The handler's `string.IsNullOrEmpty(request.ByUser)` guard fails the request |
| `Update_ShouldChangeAmount_WhenOrderExists` | `ChangeAmount` persists; `Result.Ok` returns the mapped DTO |
| `Update_ShouldFail_WhenOrderNotFound` | Fails with `NotFoundError` for an unknown `Id` |
| `Cancel_ShouldSucceedOnce_ThenFail_WhenAlreadyCancelled` | The `order.Status == PurchaseOrderStatus.Cancelled` guard — the concrete business-rule test this sample exists to demonstrate |
| `Cancel_ShouldFail_WhenOrderNotFound` / `Delete_ShouldFail_WhenOrderNotFound` | Same not-found shape on every action |
| `Delete_ShouldRemoveOrder` | Delete returns `IResultBase`; assert `IsSuccess`, not `Value` |
| `*_ShouldFail_WhenByUserIsMissing` (Update / Cancel / Delete) | All four hand-written handlers carry the same `ByUser` guard |
| `GetById_ShouldReturnNull_WhenNotFound`, `List_ShouldFilterByCustomerName` | Query paths through the spec |

For `Product` (`AutomatedSample`), there is no hand-written handler to unit-test at all for
create/update — the generated `CreateProductHandler`/`ChangePriceProductHandler` are produced code,
and this template's own convention doesn't unit-test generated handlers directly. Coverage for
`Product` instead centers on the entity's declared behavior (`[RaisesEvent]` firing on save — an
integration-level assertion through `ApiFixture`, not a handler-level one), row-level ownership
isolation, and the hand-written `ProductCreatedEventHandler`/`ProductCreatedNotificationHandler`
consumers.

**Do not write a validation test against a generated route.** Per
`docs/samples/manual-vs-automated.md`, a negative `Price` is expected to **succeed** (`201`), not
fail — the forwarded `[Range]` is never enforced under this template's generated-route convention. If
you assert that gap, assert what actually happens; never "fix" such a test by relaxing it into
claiming the validation runs.

The same scope provides both `IMessageBus` and `IRepositorySpec` — they share the same `DbContext` instance, so `SaveChangesAsync` on the repository commits what the bus handler staged.

---

## Validation Checklist

- [ ] Test class is `public sealed` and implements `IClassFixture<TFixture>`
- [ ] Constructor receives only the fixture (no other parameters)
- [ ] Every test calls `await fixture.ResetDatabaseAsync()` as the first line
- [ ] `using var scope = fixture.CreateScope()` is used (disposed at end of test)
- [ ] `IMessageBus` is resolved from scope, not from `fixture.Services`
- [ ] `IRepositorySpec` is resolved from the **same** scope as `IMessageBus`
- [ ] Happy-path tests assert `result.IsSuccess.ShouldBeTrue()`
- [ ] Failure-path tests assert `result.IsFailed.ShouldBeTrue()` + check error messages
- [ ] Seeding test data goes through `repository.AddAsync` + `SaveChangesAsync` (not `bus.Send`)
- [ ] `ByUser` is always set on requests (e.g., `"integration-test"`)
- [ ] Per-feature fixture calls `base.ConfigureWebHost(builder)` before adding services
- [ ] `dotnet build -c Release` passes
- [ ] `dotnet test` passes with all new tests green

---

## Common Mistakes

| Mistake | Fix |
|---------|-----|
| Resolving `IMessageBus` from `fixture.Services` (singleton scope) | Always use `fixture.CreateScope()` — bus handlers require a scoped `DbContext` |
| Seeding via `bus.Send(createRequest)` then testing the same path again | Seed directly via `repository.AddAsync` + `SaveChangesAsync` to isolate the scenario under test |
| Not calling `ResetDatabaseAsync()` at the start | Tests sharing state produce false positives; always reset |
| Asserting `result.Value` on a delete (returns `IResultBase`, not `IResult<T>`) | Use `result.IsSuccess.ShouldBeTrue()` — delete handlers have no value |
| Missing `ByUser` on requests | Every hand-written handler checks `string.IsNullOrEmpty(request.ByUser)` and fails the request — set it explicitly in tests (there's no request base class auto-filling it outside the running app's claim-population pipeline) |
| Creating a per-feature fixture without calling `base.ConfigureWebHost` | The in-memory DB and service overrides in `ApiFixture` will be skipped |

---

## Next Steps

After writing integration tests, run:

```bash
dotnet test --settings coverage.runsettings --collect:"XPlat Code Coverage"
```

To add a new migration after testing revealed schema gaps:

```bash
cd ApiEndpoints && dotnet ef migrations add <MigrationName> -c CoreDbContext -p Minimal.Infra/Minimal.Infra.csproj
```
