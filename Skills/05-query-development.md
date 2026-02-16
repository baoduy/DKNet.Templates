# Query Development with Fluent Pattern

## Overview
Queries represent read operations that don't modify system state. They follow the CQRS pattern using DKNet's Fluent library for optimized data retrieval.

## Query Structure

### 1. Query Definition
Queries are immutable records that implement `IWitResponse<TResult>`:
```csharp
using Fluents.Queries;

namespace SlimBus.AppServices.Profiles.V1.Queries;

public record ProfileQuery : IWitResponse<ProfileResult>
{
    #region Properties

    [FromRoute]
    public required Guid Id { get; init; }

    #endregion
}
```

**Key Points**:
- Use `record` for immutability
- Implement `IWitResponse<TResult>` from Fluents.Queries
- Use `init` instead of `set` for properties
- Decorate route parameters with `[FromRoute]`
- Query parameters with `[FromQuery]`

### 2. Result DTO
Define result DTOs that represent the data shape:
```csharp
[MapsFrom(typeof(CustomerProfile))]
public sealed record ProfileResult
{
    public Guid Id { get; init; }
    public string Name { get; init; } = null!;
    public string Email { get; init; } = null!;
    public string MembershipNo { get; init; } = null!;
    public string? Avatar { get; init; }
    public string? Phone { get; init; }
    public DateTime? BirthDay { get; init; }
    public DateTime CreatedDate { get; init; }
    public string CreatedBy { get; init; } = null!;
}
```

**Best Practices**:
- Use `[MapsFrom(typeof(Entity))]` for automatic mapping
- Mark as `sealed record` for performance
- Use `init` for immutability
- Include only necessary fields (not all entity properties)

### 3. Query Handler
Handlers implement `IHandler<TQuery, TResult>`:
```csharp
internal sealed class SingleProfileQueryHandler(IReadRepository<CustomerProfile> repo)
    : IHandler<ProfileQuery, ProfileResult>
{
    #region Methods

    public async Task<ProfileResult?> OnHandle(ProfileQuery request, CancellationToken cancellationToken)
    {
        return await repo.Query<ProfileResult>(p => p.Id == request.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    #endregion
}
```

## Query Types

### Single Item Query
```csharp
public record ProfileQuery : IWitResponse<ProfileResult>
{
    [FromRoute]
    public required Guid Id { get; init; }
}

internal sealed class SingleProfileQueryHandler(IReadRepository<CustomerProfile> repo)
    : IHandler<ProfileQuery, ProfileResult>
{
    public async Task<ProfileResult?> OnHandle(ProfileQuery request, CancellationToken cancellationToken)
    {
        return await repo.Query<ProfileResult>(p => p.Id == request.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
```

### List Query
```csharp
public record ProfileListQuery : IWitResponse<List<ProfileResult>>
{
    [FromQuery]
    public string? SearchTerm { get; init; }
    
    [FromQuery]
    public bool? IsActive { get; init; }
}

internal sealed class ProfileListQueryHandler(IReadRepository<CustomerProfile> repo)
    : IHandler<ProfileListQuery, List<ProfileResult>>
{
    public async Task<List<ProfileResult>> OnHandle(
        ProfileListQuery request,
        CancellationToken cancellationToken)
    {
        var query = repo.Query<ProfileResult>();

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            query = query.Where(p => 
                p.Name.Contains(request.SearchTerm) || 
                p.Email.Contains(request.SearchTerm));
        }

        if (request.IsActive.HasValue)
        {
            query = query.Where(p => p.IsActive == request.IsActive.Value);
        }

        return await query
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);
    }
}
```

### Paginated Query
```csharp
public sealed record PageProfilePageQuery : BasePageQuery, IWitResponse<PageResults<ProfileResult>>
{
    #region Properties

    [FromQuery] public string? Search { get; init; }

    #endregion
}

internal sealed class PageProfilePageQueryHandler(IReadRepository<CustomerProfile> repo)
    : IHandler<PageProfilePageQuery, PageResults<ProfileResult>>
{
    #region Methods

    public async Task<PageResults<ProfileResult>> OnHandle(
        PageProfilePageQuery request,
        CancellationToken cancellationToken)
    {
        var query = repo.Query<ProfileResult>();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            query = query.Where(p =>
                p.Name.Contains(request.Search) ||
                p.Email.Contains(request.Search) ||
                p.MembershipNo.Contains(request.Search));
        }

        return await query
            .OrderByDescending(p => p.CreatedDate)
            .ToPageResultsAsync(request, cancellationToken);
    }

    #endregion
}
```

**BasePageQuery Properties**:
- `PageIndex` - Current page (1-based)
- `PageSize` - Items per page
- `OrderBy` - Sort field
- `OrderDirection` - "asc" or "desc"

## Repository Query Methods

### Generic Query
```csharp
// Query with projection
repo.Query<TResult>(predicate)
    .Where(...)
    .OrderBy(...)
    .ToListAsync(cancellationToken);
```

### Direct Query
```csharp
// Query entities directly
repo.GetAll()
    .Where(p => p.IsActive)
    .Select(p => new ProfileResult { ... })
    .ToListAsync(cancellationToken);
```

### Specific Methods
```csharp
// Single item
var item = await repo.GetByIdAsync(id, cancellationToken);

// Check existence
var exists = await repo.AnyAsync(p => p.Email == email, cancellationToken);

// Count
var count = await repo.CountAsync(p => p.IsActive, cancellationToken);
```

## Projection and Mapping

### Automatic Projection
Using `Query<TResult>` with `[MapsFrom]` attribute:
```csharp
[MapsFrom(typeof(CustomerProfile))]
public sealed record ProfileResult
{
    public Guid Id { get; init; }
    public string Name { get; init; } = null!;
    public string Email { get; init; } = null!;
}

// Handler
return await repo.Query<ProfileResult>(p => p.Id == request.Id)
    .FirstOrDefaultAsync(cancellationToken);
```

### Manual Projection
For complex scenarios:
```csharp
return await repo.GetAll()
    .Where(p => p.Id == request.Id)
    .Select(p => new ProfileResult
    {
        Id = p.Id,
        Name = p.Name,
        Email = p.Email,
        FullName = p.Name + " (" + p.MembershipNo + ")",
        IsNew = p.CreatedDate > DateTime.UtcNow.AddDays(-30)
    })
    .FirstOrDefaultAsync(cancellationToken);
```

## Query Optimization

### 1. Select Only Needed Fields
```csharp
// Good - projects to DTO with only needed fields
repo.Query<ProfileBasicResult>(p => p.IsActive)
    .ToListAsync(cancellationToken);

// Bad - loads entire entity
repo.GetAll()
    .Where(p => p.IsActive)
    .ToListAsync(cancellationToken);
```

### 2. Use AsNoTracking
For read-only queries (already default with IReadRepository):
```csharp
repo.GetAll()
    .AsNoTracking()
    .Where(...)
    .ToListAsync(cancellationToken);
```

### 3. Avoid N+1 Queries
```csharp
// Good - eager loading
repo.GetAll()
    .Include(p => p.Addresses)
    .Where(p => p.IsActive)
    .ToListAsync(cancellationToken);

// Bad - lazy loading causes N+1
var profiles = await repo.GetAll().ToListAsync();
foreach (var profile in profiles)
{
    var addresses = profile.Addresses; // N+1 query
}
```

### 4. Pagination
Always paginate large result sets:
```csharp
return await query
    .OrderBy(p => p.CreatedDate)
    .ToPageResultsAsync(request, cancellationToken);
```

## Advanced Query Patterns

### Filter Builder Pattern
```csharp
internal sealed class ProfileQueryHandler(IReadRepository<CustomerProfile> repo)
    : IHandler<ProfileFilterQuery, List<ProfileResult>>
{
    public async Task<List<ProfileResult>> OnHandle(
        ProfileFilterQuery request,
        CancellationToken cancellationToken)
    {
        var query = repo.Query<ProfileResult>();

        query = ApplyFilters(query, request);
        query = ApplyOrdering(query, request);

        return await query.ToListAsync(cancellationToken);
    }

    private static IQueryable<ProfileResult> ApplyFilters(
        IQueryable<ProfileResult> query,
        ProfileFilterQuery request)
    {
        if (!string.IsNullOrWhiteSpace(request.Name))
            query = query.Where(p => p.Name.Contains(request.Name));

        if (!string.IsNullOrWhiteSpace(request.Email))
            query = query.Where(p => p.Email.Contains(request.Email));

        if (request.CreatedAfter.HasValue)
            query = query.Where(p => p.CreatedDate >= request.CreatedAfter.Value);

        return query;
    }

    private static IQueryable<ProfileResult> ApplyOrdering(
        IQueryable<ProfileResult> query,
        ProfileFilterQuery request)
    {
        return request.OrderBy?.ToLower() switch
        {
            "name" => query.OrderBy(p => p.Name),
            "email" => query.OrderBy(p => p.Email),
            "created" => query.OrderBy(p => p.CreatedDate),
            _ => query.OrderByDescending(p => p.CreatedDate)
        };
    }
}
```

### Specification Pattern
For complex, reusable query logic:
```csharp
public interface ISpecification<T>
{
    Expression<Func<T, bool>> Criteria { get; }
}

public class ActiveProfilesSpecification : ISpecification<CustomerProfile>
{
    public Expression<Func<CustomerProfile, bool>> Criteria =>
        p => p.IsActive && p.CreatedDate > DateTime.UtcNow.AddYears(-1);
}

// Usage
var spec = new ActiveProfilesSpecification();
var profiles = await repo.Query<ProfileResult>()
    .Where(spec.Criteria)
    .ToListAsync(cancellationToken);
```

## Best Practices

1. **Immutability**: Use `record` with `init` properties
2. **Sealed Types**: Mark handlers and results as `sealed`
3. **Internal Handlers**: Keep handlers internal
4. **Nullable Results**: Return `TResult?` for single item queries
5. **Projection**: Always project to DTOs, never return entities
6. **Pagination**: Use `BasePageQuery` and `ToPageResultsAsync()` for lists
7. **Filtering**: Apply filters before ordering and pagination
8. **Ordering**: Provide sensible defaults for ordering
9. **Cancellation**: Always pass `CancellationToken` through
10. **Async/Await**: Use async methods consistently
11. **Query Naming**: Name queries by their intent (e.g., `GetActiveProfiles`, not `GetProfiles`)
12. **DTO Reuse**: Create different DTOs for different use cases (ProfileResult, ProfileBasicResult, ProfileDetailResult)

## File Organization
```
{ProjectName}.AppServices/
  {FeatureName}/
    V{Version}/
      Queries/
        SingleProfileQueryHandler.cs
        PageProfilesQueryHandler.cs
        ProfileResult.cs
```

## Performance Tips

1. **Index Usage**: Ensure queries use database indexes
2. **Projection**: Select only needed columns
3. **Avoid Cartesian Products**: Use proper joins
4. **Limit Results**: Always use pagination for large datasets
5. **Caching**: Consider caching frequently accessed, rarely changed data
6. **Async**: Use async methods to avoid thread blocking

## Common Query Patterns

### Existence Check
```csharp
public async Task<bool> OnHandle(CheckEmailQuery request, CancellationToken cancellationToken)
{
    return await repo.AnyAsync(p => p.Email == request.Email, cancellationToken);
}
```

### Count Query
```csharp
public async Task<int> OnHandle(CountProfilesQuery request, CancellationToken cancellationToken)
{
    return await repo.CountAsync(p => p.IsActive, cancellationToken);
}
```

### Aggregation
```csharp
public async Task<decimal> OnHandle(AverageAgeQuery request, CancellationToken cancellationToken)
{
    return await repo.GetAll()
        .Where(p => p.BirthDay.HasValue)
        .AverageAsync(p => (DateTime.UtcNow - p.BirthDay!.Value).TotalDays / 365, cancellationToken);
}
```
