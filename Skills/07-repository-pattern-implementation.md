# Repository Pattern Implementation

## Overview
Repositories provide an abstraction over data access. DKNet provides generic repository implementations that reduce boilerplate while maintaining flexibility.

## Repository Interfaces (Domains Layer)

### Base Interfaces from DKNet

#### IReadRepository<TEntity>
Read-only operations:
```csharp
public interface IReadRepository<TEntity> where TEntity : class
{
    Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<TEntity>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);
    Task<int> CountAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);
    IQueryable<TEntity> GetAll();
    IQueryable<TResult> Query<TResult>(Expression<Func<TEntity, bool>>? predicate = null);
}
```

#### IWriteRepository<TEntity>
Includes read + write operations:
```csharp
public interface IWriteRepository<TEntity> : IReadRepository<TEntity> where TEntity : class
{
    Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);
    Task UpdateAsync(TEntity entity, CancellationToken cancellationToken = default);
    Task UpdateRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);
    Task DeleteAsync(TEntity entity, CancellationToken cancellationToken = default);
    Task DeleteRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);
}
```

### Custom Repository Interface
Define custom interfaces for domain-specific operations:
```csharp
namespace SlimBus.Domains.Features.Profiles.Repos;

public interface ICustomerProfileRepo : IWriteRepository<CustomerProfile>
{
    Task<bool> IsEmailExistAsync(string email);
    Task<CustomerProfile?> GetByEmailAsync(string email);
    Task<CustomerProfile?> GetByMembershipNoAsync(string membershipNo);
    Task<List<CustomerProfile>> GetActiveProfilesAsync(CancellationToken cancellationToken = default);
}
```

**Guidelines**:
- Extend `IWriteRepository<TEntity>` for full CRUD
- Extend `IReadRepository<TEntity>` for read-only repositories
- Add domain-specific query methods
- Keep interfaces in Domains layer
- Use descriptive method names

## Repository Implementation (Infra Layer)

### Basic Implementation
```csharp
using Microsoft.EntityFrameworkCore;
using SlimBus.Domains.Features.Profiles.Entities;
using SlimBus.Domains.Features.Profiles.Repos;

namespace SlimBus.Infra.Features.Profiles.Repos;

internal sealed class CustomerProfileRepo(CoreDbContext context)
    : ICustomerProfileRepo
{
    #region Fields

    private readonly DbSet<CustomerProfile> _dbSet = context.Set<CustomerProfile>();

    #endregion

    #region Methods

    public async Task<bool> IsEmailExistAsync(string email)
    {
        return await this._dbSet.AnyAsync(p => p.Email == email);
    }

    public async Task<CustomerProfile?> GetByEmailAsync(string email)
    {
        return await this._dbSet.FirstOrDefaultAsync(p => p.Email == email);
    }

    public async Task<CustomerProfile?> GetByMembershipNoAsync(string membershipNo)
    {
        return await this._dbSet.FirstOrDefaultAsync(p => p.MembershipNo == membershipNo);
    }

    public async Task<List<CustomerProfile>> GetActiveProfilesAsync(
        CancellationToken cancellationToken = default)
    {
        return await this._dbSet
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);
    }

    #endregion
}
```

**Key Points**:
- Mark as `internal sealed`
- Inject `DbContext` via primary constructor
- Store `DbSet<T>` as a field for reusability
- Implement only custom methods (generic CRUD comes from DKNet)
- Use async/await consistently
- Support CancellationToken

## Service Registration

### Automatic Registration
DKNet provides automatic repository registration:
```csharp
// In Program.cs or Startup
services.AddGenericRepositories<CoreDbContext>();
```

This automatically:
- Scans assemblies for repository implementations
- Registers them with appropriate lifetime (Scoped)
- Wires up generic repository base functionality

### Manual Registration
For specific cases:
```csharp
services.AddScoped<ICustomerProfileRepo, CustomerProfileRepo>();
```

## Using Repositories

### In Command Handlers
```csharp
internal sealed class CreateProfileCommandHandler(
    ICustomerProfileRepo repository,
    IMapper mapper)
    : IHandler<CreateProfileCommand, ProfileResult>
{
    public async Task<IResult<ProfileResult>> OnHandle(
        CreateProfileCommand request,
        CancellationToken cancellationToken)
    {
        // Check duplicate
        if (await repository.IsEmailExistAsync(request.Email))
        {
            return Result.Fail<ProfileResult>("Email already exists");
        }

        // Create entity
        var profile = mapper.Map<CustomerProfile>(request);

        // Add to repository
        await repository.AddAsync(profile, cancellationToken);

        // Return result
        return mapper.ResultOf<ProfileResult>(profile);
    }
}
```

### In Query Handlers
```csharp
internal sealed class SingleProfileQueryHandler(
    IReadRepository<CustomerProfile> repo)
    : IHandler<ProfileQuery, ProfileResult>
{
    public async Task<ProfileResult?> OnHandle(
        ProfileQuery request,
        CancellationToken cancellationToken)
    {
        // Use generic query with projection
        return await repo.Query<ProfileResult>(p => p.Id == request.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
```

**Note**: Query handlers can use `IReadRepository<T>` for read-only access.

## Common Repository Patterns

### Existence Check
```csharp
public async Task<bool> IsEmailExistAsync(string email)
{
    return await this._dbSet.AnyAsync(p => p.Email == email);
}
```

### Find by Unique Key
```csharp
public async Task<CustomerProfile?> GetByEmailAsync(string email)
{
    return await this._dbSet
        .AsNoTracking()
        .FirstOrDefaultAsync(p => p.Email == email);
}
```

### Filtered List
```csharp
public async Task<List<CustomerProfile>> GetActiveProfilesAsync(
    CancellationToken cancellationToken = default)
{
    return await this._dbSet
        .Where(p => p.IsActive)
        .OrderBy(p => p.Name)
        .ToListAsync(cancellationToken);
}
```

### Eager Loading
```csharp
public async Task<CustomerProfile?> GetWithAddressesAsync(Guid id)
{
    return await this._dbSet
        .Include(p => p.Addresses)
        .FirstOrDefaultAsync(p => p.Id == id);
}
```

### Pagination
```csharp
public async Task<PageResults<CustomerProfile>> GetPagedAsync(
    int pageIndex,
    int pageSize,
    CancellationToken cancellationToken = default)
{
    var query = this._dbSet
        .Where(p => p.IsActive)
        .OrderByDescending(p => p.CreatedDate);

    var totalCount = await query.CountAsync(cancellationToken);
    var items = await query
        .Skip((pageIndex - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync(cancellationToken);

    return new PageResults<CustomerProfile>
    {
        Items = items,
        TotalCount = totalCount,
        PageIndex = pageIndex,
        PageSize = pageSize
    };
}
```

### Complex Query
```csharp
public async Task<List<CustomerProfile>> SearchAsync(
    string? name,
    string? email,
    DateTime? createdAfter,
    CancellationToken cancellationToken = default)
{
    var query = this._dbSet.AsQueryable();

    if (!string.IsNullOrWhiteSpace(name))
        query = query.Where(p => p.Name.Contains(name));

    if (!string.IsNullOrWhiteSpace(email))
        query = query.Where(p => p.Email.Contains(email));

    if (createdAfter.HasValue)
        query = query.Where(p => p.CreatedDate >= createdAfter.Value);

    return await query
        .OrderBy(p => p.Name)
        .ToListAsync(cancellationToken);
}
```

## Generic Repository Usage

When custom methods aren't needed, inject generic repositories directly:
```csharp
internal sealed class DeleteProfileCommandHandler(
    IWriteRepository<CustomerProfile> repository)
    : IHandler<DeleteProfileCommand>
{
    public async Task<IResult> OnHandle(
        DeleteProfileCommand request,
        CancellationToken cancellationToken)
    {
        var profile = await repository.GetByIdAsync(request.Id, cancellationToken);
        
        if (profile == null)
            return Result.Fail("Profile not found");

        await repository.DeleteAsync(profile, cancellationToken);
        
        return Result.Ok();
    }
}
```

## Query Projection

### Direct Projection
```csharp
public async Task<List<ProfileBasicResult>> GetBasicProfilesAsync(
    CancellationToken cancellationToken = default)
{
    return await this._dbSet
        .Select(p => new ProfileBasicResult
        {
            Id = p.Id,
            Name = p.Name,
            Email = p.Email
        })
        .ToListAsync(cancellationToken);
}
```

### Using Query<TResult>
With DKNet's lazy mapping:
```csharp
public async Task<List<ProfileResult>> GetProfileResultsAsync(
    CancellationToken cancellationToken = default)
{
    return await this._dbSet
        .Query<ProfileResult>()
        .ToListAsync(cancellationToken);
}
```

## Best Practices

1. **Interface Segregation**: Define separate interfaces for read/write if needed
2. **Naming Convention**: `{EntityName}Repo` (e.g., CustomerProfileRepo)
3. **Sealed Classes**: Mark implementations as `sealed`
4. **Internal Visibility**: Keep implementations `internal`
5. **DbSet Field**: Store DbSet as a field for reusability
6. **Async Operations**: Always use async methods
7. **CancellationToken**: Support cancellation in all async methods
8. **NoTracking**: Use `AsNoTracking()` for read-only queries
9. **Projection**: Project to DTOs in queries when possible
10. **Error Handling**: Let exceptions bubble up, handle in handlers
11. **Transaction Management**: Done automatically by DbContext
12. **Lazy Loading**: Avoid lazy loading, use explicit loading or Include
13. **Generic First**: Use generic repositories when custom methods aren't needed
14. **Query Optimization**: Use indexes, projection, and appropriate filtering

## File Location
```
Interfaces:
{ProjectName}.Domains/Features/{FeatureName}/Repos/I{EntityName}Repo.cs

Implementations:
{ProjectName}.Infra/Features/{FeatureName}/Repos/{EntityName}Repo.cs
```

## Testing Repositories

### Unit Testing (with In-Memory Database)
```csharp
public class CustomerProfileRepoTests
{
    private readonly DbContextOptions<CoreDbContext> _options;

    public CustomerProfileRepoTests()
    {
        _options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase(databaseName: "TestDb")
            .Options;
    }

    [Fact]
    public async Task IsEmailExistAsync_ReturnsTrue_WhenEmailExists()
    {
        // Arrange
        await using var context = new CoreDbContext(_options);
        var repo = new CustomerProfileRepo(context);
        var profile = new CustomerProfile("Test", "MEM001", "test@example.com", "123", "system");
        await repo.AddAsync(profile);
        await context.SaveChangesAsync();

        // Act
        var exists = await repo.IsEmailExistAsync("test@example.com");

        // Assert
        Assert.True(exists);
    }
}
```

## Performance Considerations

1. **Select Only Needed Fields**: Use projection to reduce data transfer
2. **Use Appropriate Indexes**: Ensure queries use database indexes
3. **Avoid N+1 Queries**: Use `Include()` for related entities
4. **Pagination**: Always paginate large result sets
5. **Compiled Queries**: For frequently executed queries
6. **Batch Operations**: Use `AddRangeAsync` and `UpdateRangeAsync` for bulk operations
7. **Query Splitting**: Use `AsSplitQuery()` for complex includes
8. **Connection Pooling**: Enabled by default in EF Core
