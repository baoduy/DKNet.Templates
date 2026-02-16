# Quick Reference Guide - DKNet Templates

## Table of Contents
- [Project Setup](#project-setup)
- [Domain Entity Checklist](#domain-entity-checklist)
- [Command Checklist](#command-checklist)
- [Query Checklist](#query-checklist)
- [API Endpoint Checklist](#api-endpoint-checklist)
- [Common Code Snippets](#common-code-snippets)
- [Troubleshooting](#troubleshooting)

## Project Setup

### Initial Setup Commands
```bash
# Clone/create project
dotnet new sln -n MyProject
dotnet new webapi -n MyProject.Api
dotnet new classlib -n MyProject.Domains
dotnet new classlib -n MyProject.Infra
dotnet new classlib -n MyProject.AppServices
dotnet new classlib -n MyProject.Share

# Add projects to solution
dotnet sln add **/*.csproj

# Add DKNet packages
dotnet add MyProject.Domains package DKNet.EfCore.Abstractions
dotnet add MyProject.Domains package DKNet.EfCore.Repos.Abstractions
dotnet add MyProject.Infra package DKNet.EfCore.Repos
dotnet add MyProject.Infra package DKNet.EfCore.Events
dotnet add MyProject.AppServices package DKNet.SlimBus.Extensions
dotnet add MyProject.Api package DKNet.AspCore.SlimBus
```

### Service Registration (Program.cs)
```csharp
// DbContext with auto-configuration
services.AddDbContextWithHook<CoreDbContext>(
    (sp, builder) => builder.UseSqlServer(connectionString))
    .UseAutoConfigModel([
        typeof(ProfileMapper).Assembly,
        typeof(CustomerProfile).Assembly
    ])
    .UseAutoDataSeeding([typeof(ProfileData).Assembly]);

// Generic repositories
services.AddGenericRepositories<CoreDbContext>();

// API versioning and endpoints
app.MapApiVersioning([typeof(ProfileV1Endpoint).Assembly]);
```

## Domain Entity Checklist

- [ ] Inherit from `AggregateRoot`
- [ ] Add `[Table("TableName", Schema = DomainSchemas.{Schema})]` attribute
- [ ] Create public constructor for new entities
- [ ] Create internal constructor for EF Core rehydration
- [ ] Use private setters for all properties
- [ ] Implement Update method for modifications
- [ ] Add domain events where appropriate
- [ ] Create repository interface in same feature folder

**Template**:
```csharp
[Table("EntityName", Schema = DomainSchemas.Feature)]
public class EntityName : AggregateRoot
{
    // Public constructor
    public EntityName(string requiredField, string byUser)
        : this(Guid.Empty, requiredField, byUser) { }

    // Internal constructor
    internal EntityName(Guid id, string requiredField, string createdBy)
        : base(id, createdBy)
    {
        this.RequiredField = requiredField;
    }

    // Properties with private setters
    public string RequiredField { get; private set; }
    public string? OptionalField { get; private set; }

    // Update method
    public void Update(string? newField, string userId)
    {
        if (!string.IsNullOrEmpty(newField))
            this.OptionalField = newField;
        
        this.SetUpdatedBy(userId);
    }
}
```

## Command Checklist

- [ ] Create command record implementing `IWitResponse<TResult>`
- [ ] Add `[MapsTo(typeof(Entity))]` attribute
- [ ] Inherit from `BaseCommand`
- [ ] Add validation attributes
- [ ] Create FluentValidation validator
- [ ] Create handler implementing `IHandler<TCommand, TResult>`
- [ ] Inject required repositories and services
- [ ] Perform database validation
- [ ] Map command to entity
- [ ] Add domain events
- [ ] Return `mapper.ResultOf<TResult>(entity)`

**Template**:
```csharp
// Command
[MapsTo(typeof(Entity))]
public sealed record CreateEntityCommand : BaseCommand, IWitResponse<EntityResult>
{
    [Required] public string Name { get; set; } = null!;
}

// Validator
internal sealed class CreateEntityCommandValidator : AbstractValidator<CreateEntityCommand>
{
    public CreateEntityCommandValidator()
    {
        RuleFor(a => a.Name).NotEmpty().Length(1, 100);
    }
}

// Handler
internal sealed class CreateEntityCommandHandler(
    IEntityRepo repository,
    IMapper mapper)
    : IHandler<CreateEntityCommand, EntityResult>
{
    public async Task<IResult<EntityResult>> OnHandle(
        CreateEntityCommand request,
        CancellationToken cancellationToken)
    {
        // Validate
        if (await repository.ExistsAsync(request.Name))
            return Result.Fail<EntityResult>("Already exists");

        // Map and save
        var entity = mapper.Map<Entity>(request);
        await repository.AddAsync(entity, cancellationToken);

        // Event
        entity.AddEvent(new EntityCreatedEvent(entity.Id));

        return mapper.ResultOf<EntityResult>(entity);
    }
}
```

## Query Checklist

- [ ] Create query record implementing `IWitResponse<TResult>`
- [ ] Add `[FromRoute]` or `[FromQuery]` attributes
- [ ] Create result DTO with `[MapsFrom(typeof(Entity))]`
- [ ] Create handler implementing `IHandler<TQuery, TResult>`
- [ ] Inject `IReadRepository<TEntity>`
- [ ] Use `repo.Query<TResult>()` for projection
- [ ] Apply filters and ordering
- [ ] Return result (nullable for single items)

**Template**:
```csharp
// Query
public record EntityQuery : IWitResponse<EntityResult>
{
    [FromRoute] public required Guid Id { get; init; }
}

// Result DTO
[MapsFrom(typeof(Entity))]
public sealed record EntityResult
{
    public Guid Id { get; init; }
    public string Name { get; init; } = null!;
}

// Handler
internal sealed class EntityQueryHandler(IReadRepository<Entity> repo)
    : IHandler<EntityQuery, EntityResult>
{
    public async Task<EntityResult?> OnHandle(
        EntityQuery request,
        CancellationToken cancellationToken)
    {
        return await repo.Query<EntityResult>(e => e.Id == request.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
```

## API Endpoint Checklist

- [ ] Create endpoint config implementing `IEndpointConfig`
- [ ] Set `Version` property
- [ ] Set `GroupEndpoint` property
- [ ] Implement `Map` method
- [ ] Use appropriate Map methods (MapGet, MapPost, MapPut, MapDelete)
- [ ] Add descriptions and tags
- [ ] Add idempotency filter for POST operations
- [ ] Mark as `internal sealed`

**Template**:
```csharp
internal sealed class EntityV1Endpoint : IEndpointConfig
{
    public int Version => 1;
    public string GroupEndpoint => "/entities";

    public void Map(RouteGroupBuilder group)
    {
        group.MapGetPage<PageEntityQuery, EntityResult>("")
            .WithDescription("Get all entities");

        group.MapGet<EntityQuery, EntityResult?>("{id:guid}")
            .WithDescription("Get entity by id");

        group.MapPost<CreateEntityCommand, EntityResult>("")
            .AddIdempotencyFilter()
            .WithDescription("Create entity");

        group.MapPut<UpdateEntityCommand, EntityResult>("{id:guid}")
            .WithDescription("Update entity");

        group.MapDelete<DeleteEntityCommand>("{id:guid}")
            .WithDescription("Delete entity");
    }
}
```

## Common Code Snippets

### EF Core Mapper
```csharp
internal sealed class EntityMapper : DefaultEntityTypeConfiguration<Entity>
{
    public override void Configure(EntityTypeBuilder<Entity> builder)
    {
        base.Configure(builder);

        builder.HasIndex(e => e.Name).IsUnique();
        builder.Property(e => e.Name).HasMaxLength(100).IsRequired();
        builder.ToTable("Entities", DomainSchemas.Feature);
    }
}
```

### Custom Repository
```csharp
// Interface (Domains)
public interface IEntityRepo : IWriteRepository<Entity>
{
    Task<bool> ExistsAsync(string name);
}

// Implementation (Infra)
internal sealed class EntityRepo(CoreDbContext context) : IEntityRepo
{
    private readonly DbSet<Entity> _dbSet = context.Set<Entity>();

    public async Task<bool> ExistsAsync(string name)
    {
        return await _dbSet.AnyAsync(e => e.Name == name);
    }
}
```

### Paginated Query
```csharp
public sealed record PageEntityQuery : BasePageQuery, IWitResponse<PageResults<EntityResult>>
{
    [FromQuery] public string? Search { get; init; }
}

internal sealed class PageEntityQueryHandler(IReadRepository<Entity> repo)
    : IHandler<PageEntityQuery, PageResults<EntityResult>>
{
    public async Task<PageResults<EntityResult>> OnHandle(
        PageEntityQuery request,
        CancellationToken cancellationToken)
    {
        var query = repo.Query<EntityResult>();

        if (!string.IsNullOrWhiteSpace(request.Search))
            query = query.Where(e => e.Name.Contains(request.Search));

        return await query
            .OrderByDescending(e => e.CreatedDate)
            .ToPageResultsAsync(request, cancellationToken);
    }
}
```

### Update Command
```csharp
public sealed record UpdateEntityCommand : BaseCommand, IWitResponse<EntityResult>
{
    [FromRoute] public Guid Id { get; set; }
    [StringLength(100)] public string? Name { get; set; }
}

internal sealed class UpdateEntityCommandHandler(
    IEntityRepo repository,
    IMapper mapper)
    : IHandler<UpdateEntityCommand, EntityResult>
{
    public async Task<IResult<EntityResult>> OnHandle(
        UpdateEntityCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await repository.GetByIdAsync(request.Id, cancellationToken);
        if (entity == null)
            return Result.Fail<EntityResult>("Not found");

        entity.Update(request.Name, request.UserId);
        await repository.UpdateAsync(entity, cancellationToken);

        return mapper.ResultOf<EntityResult>(entity);
    }
}
```

## Troubleshooting

### Issue: "Entity type not configured"
**Solution**: Ensure mapper inherits from `DefaultEntityTypeConfiguration` and assembly is registered in `UseAutoConfigModel`.

### Issue: "Repository not found"
**Solution**: Verify `AddGenericRepositories<CoreDbContext>()` is called in Program.cs.

### Issue: "Validation not running"
**Solution**: Ensure validator follows naming convention: `{Command}Validator` and is in the same assembly.

### Issue: "Mapping not working"
**Solution**: Check `[MapsTo]` and `[MapsFrom]` attributes are applied correctly.

### Issue: "API endpoint not found"
**Solution**: Ensure endpoint config implements `IEndpointConfig` and `MapApiVersioning` is called.

### Issue: "Domain events not firing"
**Solution**: Verify event publisher is registered: `AddEventPublisher<CoreDbContext, EventPublisher>()`.

### Issue: "Idempotency filter error"
**Solution**: Ensure client sends `X-Idempotency-Key` header with POST requests.

### Issue: "Lazy mapping fails"
**Solution**: Use `mapper.ResultOf<T>()` instead of `mapper.Map<T>()` for entities without IDs yet.

## Migration Commands

```bash
# Add migration
dotnet ef migrations add MigrationName --project ProjectName.Infra --startup-project ProjectName.Api

# Update database
dotnet ef database update --project ProjectName.Infra --startup-project ProjectName.Api

# Remove last migration
dotnet ef migrations remove --project ProjectName.Infra --startup-project ProjectName.Api

# Script migration
dotnet ef migrations script --project ProjectName.Infra --startup-project ProjectName.Api --output migration.sql
```

## Testing Endpoints

```http
### Variables
@baseUrl = https://localhost:7001/api
@version = v1

### Get all
GET {{baseUrl}}/{{version}}/entities?pageIndex=1&pageSize=10

### Get by ID
@entityId = 00000000-0000-0000-0000-000000000000
GET {{baseUrl}}/{{version}}/entities/{{entityId}}

### Create
POST {{baseUrl}}/{{version}}/entities
Content-Type: application/json
X-Idempotency-Key: {{$guid}}

{
  "name": "Test Entity"
}

### Update
PUT {{baseUrl}}/{{version}}/entities/{{entityId}}
Content-Type: application/json

{
  "name": "Updated Entity"
}

### Delete
DELETE {{baseUrl}}/{{version}}/entities/{{entityId}}
```

---

**Tip**: Keep this file open as a quick reference while developing. For detailed explanations, refer to the specific skill files in the README.
