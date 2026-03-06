# Copilot Instructions for DKNet Templates

## Project Overview

DKNet Templates is an ASP.NET Core project template that implements **Clean Architecture** with **Domain-Driven Design (DDD)** patterns. It provides a structured approach to building enterprise-grade APIs using the DKNet family of NuGet packages and CQRS via SlimMessageBus.

The template is used as a starting point for new projects and demonstrates best practices for layered architecture, domain modeling, and API development.

## Tech Stack

- **.NET 9** with C# (nullable reference types enabled, implicit usings enabled)
- **ASP.NET Core Minimal APIs** for HTTP endpoints
- **Entity Framework Core 9** for data persistence (SQL Server, PostgreSQL)
- **SlimMessageBus** for CQRS messaging and in-memory command/query dispatch
- **Mapster** for object-to-object mapping (`[MapsTo]` / `[MapsFrom]` attributes)
- **FluentValidation 12** for request validation
- **FluentResults** for the result/error pattern
- **.NET Aspire** for local orchestration (optional)
- **xUnit + Shouldly** for testing; **Testcontainers** for integration tests

### DKNet Packages
| Package | Purpose |
|---------|---------|
| `DKNet.EfCore.Abstractions` | `AggregateRoot`, `AuditedEntity<T>` base classes |
| `DKNet.EfCore.Repos` | Generic `IReadRepository<T>` / `IWriteRepository<T>` |
| `DKNet.EfCore.Events` | Domain event publishing via EF Core save hooks |
| `DKNet.EfCore.Relational.Helpers` | `DefaultEntityTypeConfiguration<T>`, auto-model wiring |
| `DKNet.SlimBus.Extensions` | `IHandler<TRequest, TResult>`, `IWitResponse<T>` |
| `DKNet.AspCore.SlimBus` | Minimal API integration, `IEndpointConfig`, versioning |

## Project Structure

```
src/
  {ProjectName}.Domains/          # Domain layer — entities, repository interfaces
    Features/{Feature}/
      Entities/                   # AggregateRoot-based domain entities
      Repos/                      # Repository interfaces (IWriteRepository / IReadRepository)
    Share/                        # Domain constants, schemas (DomainSchemas)

  {ProjectName}.Infra/            # Infrastructure layer — EF Core, repositories
    Features/{Feature}/
      Mappers/                    # DefaultEntityTypeConfiguration<T> subclasses
      Repos/                      # Repository implementations (internal sealed)
      StaticData/                 # Seed data classes
      ExternalEvents/             # External domain event handlers
    Data/                         # DbContext and EF Core migrations

  {ProjectName}.AppServices/      # Application layer — CQRS commands, queries, handlers
    {Feature}/V{n}/
      Actions/                    # Commands: Create, Update, Delete
      Queries/                    # Query records and result DTOs
      Events/                     # Domain event handlers
      Validators/                 # FluentValidation validators

  {ProjectName}.Api/              # Presentation layer — Minimal API endpoints
    ApiEndpoints/                 # IEndpointConfig implementations
    Configs/                      # Middleware, filters, API configuration

  {ProjectName}.Share/            # Shared constants, utilities, cross-cutting concerns
  {ProjectName}.AppHost/          # .NET Aspire app host (startup & DI composition)
```

**Dependency flow**: `Api → AppServices → Domains ← Infra`  
`Share` is referenced by all layers. `Domains` has no dependencies on other project layers.

## Build, Test & Migration Commands

```bash
# Build the solution
dotnet build

# Run all tests
dotnet test

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"

# Add a new EF Core migration
dotnet ef migrations add <MigrationName> \
  --project {ProjectName}.Infra \
  --startup-project {ProjectName}.Api

# Apply migrations to the database
dotnet ef database update \
  --project {ProjectName}.Infra \
  --startup-project {ProjectName}.Api

# Remove the last migration
dotnet ef migrations remove \
  --project {ProjectName}.Infra \
  --startup-project {ProjectName}.Api
```

## Code Patterns and Conventions

### Domain Entities
- Always inherit from `AggregateRoot` (provides `Id`, `CreatedBy/Date`, `UpdatedBy/Date`, `IsActive`, `RowVersion`, domain events).
- Decorate with `[Table("TableName", Schema = DomainSchemas.{Schema})]`.
- **Two constructors required**: public (create new) and `internal` (EF Core rehydration).
- All properties use **private setters** to enforce encapsulation.
- Mutations happen through explicit public methods that call `SetUpdatedBy(userId)`.
- Domain events are added with `entity.AddEvent(new SomeDomainEvent(...))`.

```csharp
[Table("Entities", Schema = DomainSchemas.Feature)]
public class MyEntity : AggregateRoot
{
    public MyEntity(string name, string byUser) : this(Guid.Empty, name, byUser) { }

    internal MyEntity(Guid id, string name, string createdBy) : base(id, createdBy)
    {
        Name = name;
    }

    public string Name { get; private set; }

    public void Update(string? name, string userId)
    {
        if (!string.IsNullOrEmpty(name)) Name = name;
        SetUpdatedBy(userId);
    }
}
```

### Commands (Write Operations)
- Sealed records implementing `IWitResponse<TResult>` from `Fluents.Requests`.
- Inherit from `BaseCommand` (provides `UserId`, `CorrelationId`).
- Annotate with `[MapsTo(typeof(Entity))]` for Mapster auto-mapping.
- Handlers are `internal sealed` classes implementing `IHandler<TCommand, TResult>`.
- Return `mapper.ResultOf<TResult>(entity)` (not `mapper.Map<TResult>(entity)`).

```csharp
[MapsTo(typeof(MyEntity))]
public sealed record CreateMyEntityCommand : BaseCommand, IWitResponse<MyEntityResult>
{
    [Required] public string Name { get; set; } = null!;
}

internal sealed class CreateMyEntityCommandHandler(IMyEntityRepo repo, IMapper mapper)
    : IHandler<CreateMyEntityCommand, MyEntityResult>
{
    public async Task<IResult<MyEntityResult>> OnHandle(
        CreateMyEntityCommand request, CancellationToken cancellationToken)
    {
        if (await repo.ExistsAsync(request.Name))
            return Result.Fail<MyEntityResult>("Already exists");

        var entity = mapper.Map<MyEntity>(request);
        await repo.AddAsync(entity, cancellationToken);
        entity.AddEvent(new MyEntityCreatedEvent(entity.Id));
        return mapper.ResultOf<MyEntityResult>(entity);
    }
}
```

### Queries (Read Operations)
- Records implementing `IWitResponse<TResult>` from `Fluents.Queries`.
- Bind parameters with `[FromRoute]` or `[FromQuery]`.
- Result DTOs are annotated with `[MapsFrom(typeof(Entity))]`.
- Handlers use `IReadRepository<TEntity>` and `repo.Query<TResult>()` for projection.

```csharp
public record GetMyEntityQuery : IWitResponse<MyEntityResult>
{
    [FromRoute] public required Guid Id { get; init; }
}

[MapsFrom(typeof(MyEntity))]
public sealed record MyEntityResult
{
    public Guid Id { get; init; }
    public string Name { get; init; } = null!;
}

internal sealed class GetMyEntityQueryHandler(IReadRepository<MyEntity> repo)
    : IHandler<GetMyEntityQuery, MyEntityResult>
{
    public async Task<MyEntityResult?> OnHandle(
        GetMyEntityQuery request, CancellationToken cancellationToken)
        => await repo.Query<MyEntityResult>(e => e.Id == request.Id)
            .FirstOrDefaultAsync(cancellationToken);
}
```

### API Endpoints
- `internal sealed` classes implementing `IEndpointConfig`.
- Set `Version` (int) and `GroupEndpoint` (route prefix) properties.
- Use `MapGet`, `MapPost`, `MapPut`, `MapDelete`, `MapGetPage` extension methods.
- Add `.AddIdempotencyFilter()` to POST endpoints.

```csharp
internal sealed class MyEntityV1Endpoint : IEndpointConfig
{
    public int Version => 1;
    public string GroupEndpoint => "/my-entities";

    public void Map(RouteGroupBuilder group)
    {
        group.MapGetPage<PageMyEntityQuery, MyEntityResult>("").WithDescription("List entities");
        group.MapGet<GetMyEntityQuery, MyEntityResult?>("{id:guid}").WithDescription("Get by id");
        group.MapPost<CreateMyEntityCommand, MyEntityResult>("").AddIdempotencyFilter().WithDescription("Create");
        group.MapPut<UpdateMyEntityCommand, MyEntityResult>("{id:guid}").WithDescription("Update");
        group.MapDelete<DeleteMyEntityCommand>("{id:guid}").WithDescription("Delete");
    }
}
```

### EF Core Mappers
- Inherit from `DefaultEntityTypeConfiguration<TEntity>`.
- Call `base.Configure(builder)`.
- Register assemblies via `UseAutoConfigModel([...assemblies...])` in Program.cs.

### Repository Interfaces (Domains Layer)
- Extend `IWriteRepository<TEntity>` or `IReadRepository<TEntity>`.
- Declare only custom query methods beyond CRUD.

### Service Registration (Program.cs / AppHost)
```csharp
services.AddDbContextWithHook<CoreDbContext>(
        (sp, b) => b.UseSqlServer(connectionString))
    .UseAutoConfigModel([typeof(MyEntityMapper).Assembly, typeof(MyEntity).Assembly])
    .UseAutoDataSeeding([typeof(SeedData).Assembly]);

services.AddGenericRepositories<CoreDbContext>();

app.MapApiVersioning([typeof(MyEntityV1Endpoint).Assembly]);
```

## Validation Conventions
- Use **Data Annotations** (`[Required]`, `[StringLength]`) on command/query properties for simple rules.
- Use **FluentValidation** for complex validation; validators are `internal sealed` and named `{Command}Validator`.
- Perform **database-level validation** inside the handler (e.g., duplicate checks) and return `Result.Fail<T>(message)`.

## Anti-Patterns to Avoid
- ❌ Public setters on domain entity properties — use private setters and mutation methods.
- ❌ `mapper.Map<TResult>(entity)` on newly created entities — use `mapper.ResultOf<TResult>(entity)` instead.
- ❌ Direct EF Core / DbContext calls in the Api layer — go through AppServices and repositories.
- ❌ Business logic in API endpoint handlers — keep endpoints thin and delegate to CQRS handlers.
- ❌ Mutable command/query records — use `init`-only setters for query properties.
- ❌ Skipping `base.Configure(builder)` in EF Core mappers — the base sets up audit fields and concurrency.

## Skills Documentation

For detailed, example-driven guides see the **`Skills/`** folder:

| File | Topic |
|------|-------|
| [`01-project-structure-and-architecture.md`](../Skills/01-project-structure-and-architecture.md) | Layers, dependencies, naming |
| [`02-efcore-domain-entity-development.md`](../Skills/02-efcore-domain-entity-development.md) | Entity patterns, domain events |
| [`03-efcore-configuration-development.md`](../Skills/03-efcore-configuration-development.md) | EF Core mappers, migrations |
| [`04-action-command-development.md`](../Skills/04-action-command-development.md) | Create / Update / Delete commands |
| [`05-query-development.md`](../Skills/05-query-development.md) | Single, list, paginated queries |
| [`06-api-configuration-endpoint-development.md`](../Skills/06-api-configuration-endpoint-development.md) | Minimal API endpoints, versioning |
| [`07-repository-pattern-implementation.md`](../Skills/07-repository-pattern-implementation.md) | Custom repositories, read/write interfaces |
| [`08-validation-and-mapping-patterns.md`](../Skills/08-validation-and-mapping-patterns.md) | FluentValidation, Mapster |
| [`QUICK-REFERENCE.md`](../Skills/QUICK-REFERENCE.md) | Checklists and code snippets |
