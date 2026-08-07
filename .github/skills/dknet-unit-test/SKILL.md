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

1. **Feature name** and entity class (e.g., `CustomerProfiles` / `CustomerProfile`)
2. **AppServices request types** for the feature (Create / Update / Delete / Query requests)
3. **Spec class** for querying the entity (e.g., `SpecGetCustomerProfile`)
4. **Domain entity constructor** signature (to seed test data directly)
5. **Business rules** to cover: duplicate checks, not-found paths, validation failures

---

## Project Conventions

### Test Project Structure

```
src/ApiEndpoints/Minimal.App.Tests/
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

Create `src/ApiEndpoints/Minimal.App.Tests/Integration/{Feature}/V1/{Entity}ActionsIntegrationTests.cs`:

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

## Reference: CustomerProfile Integration Tests (actual production code)

Test class: `CustomerProfileActionsIntegrationTests(ApiFixture fixture) : IClassFixture<ApiFixture>`

| Test | What it proves |
|------|---------------|
| `Test_CustomerProvide_Mapping` | Mapster config is correct; `IMapper` resolves from DI |
| `CreateActionShouldResolveFromDiAndPersistProfile` | Full create flow + `TEST-MEM-` membership generation + EF persistence |
| `CreateActionShouldFailWhenEmailAlreadyExists` | Duplicate-check business rule returns `IsFailed` with specific message |
| `UpdateActionShouldResolveFromDiAndUpdateEntity` | Fetch → mutate → verify persisted changes |
| `UpdateActionShouldFailWhenProfileIsMissing` | Not-found returns `IsFailed` with `$"The Profile {id} is not found."` |
| `DeleteActionShouldResolveFromDiAndDeleteEntity` | Soft-delete removes entity from spec query results |
| `DeleteActionShouldFailWhenIdIsEmpty` | `Guid.Empty` guard returns `IsFailed` with `"The Id is in valid."` |

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
- [ ] `dotnet build src/DKNet.Templates.sln -c Release` passes
- [ ] `dotnet test src/DKNet.Templates.sln` passes with all new tests green

---

## Common Mistakes

| Mistake | Fix |
|---------|-----|
| Resolving `IMessageBus` from `fixture.Services` (singleton scope) | Always use `fixture.CreateScope()` — bus handlers require a scoped `DbContext` |
| Seeding via `bus.Send(createRequest)` then testing the same path again | Seed directly via `repository.AddAsync` + `SaveChangesAsync` to isolate the scenario under test |
| Not calling `ResetDatabaseAsync()` at the start | Tests sharing state produce false positives; always reset |
| Asserting `result.Value` on a delete (returns `IResultBase`, not `IResult<T>`) | Use `result.IsSuccess.ShouldBeTrue()` — delete handlers have no value |
| Missing `ByUser` on requests | `RequestBase.ByUser` is used by audit and domain mutation methods; omitting it causes `null` reference errors or incorrect audit data |
| Creating a per-feature fixture without calling `base.ConfigureWebHost` | The in-memory DB and service overrides in `ApiFixture` will be skipped |

---

## Next Steps

After writing integration tests, run:

```bash
dotnet test src/DKNet.Templates.sln --settings src/coverage.runsettings --collect:"XPlat Code Coverage"
```

To add a new migration after testing revealed schema gaps:

```bash
cd src/Minimal.ApiEndpoints && ./add-migration.sh <MigrationName>
```
