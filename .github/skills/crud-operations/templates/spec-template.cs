// ==================================================
// QUERY SPEC TEMPLATE
// ==================================================
// Location: SlimBus.AppServices/Specs/<FEATURE>Specs.cs
//
// INSTRUCTIONS:
// 1. Replace <FEATURE> with your entity name (e.g., "CustomerProfile")
// 2. Replace <ENTITY> with your domain entity class
// 3. Create one spec class per query pattern
// 4. Each spec inherits from Specification<Entity>
// 5. Use Query.Where, .Include, .OrderBy for filtering/ordering
// 6. Add this to your AppServices project

using Ardalis.Specification;
using <DOMAIN_NAMESPACE>.<ENTITY>;

namespace SlimBus.AppServices.Specs;

// ==== Example 1: Get by ID ====
public sealed class SpecGet<FEATURE>ById : Specification<<ENTITY>>
{
    public SpecGet<FEATURE>ById(Guid id)
    {
        Query.Where(x => x.Id == id);
    }
}

// ==== Example 2: Get by Email (or other unique property) ====
// TODO: Replace Email with your property
public sealed class SpecGet<FEATURE>ByEmail : Specification<<ENTITY>>
{
    public SpecGet<FEATURE>ByEmail(string email)
    {
        Query.Where(x => x.Email == email);  // TODO: Replace property
    }
}

// ==== Example 3: Get all by User ID (with ordering) ====
public sealed class SpecGet<FEATURE>sByUserId : Specification<<ENTITY>>
{
    public SpecGet<FEATURE>sByUserId(Guid userId)
    {
        Query
            .Where(x => x.UserId == userId)  // TODO: Adjust filter
            .OrderBy(x => x.CreatedAt);      // TODO: Adjust ordering
    }
}

// ==== Example 4: Get with related data (Include) ====
// TODO: Add Include() for any navigation properties
public sealed class SpecGet<FEATURE>WithDetails : Specification<<ENTITY>>
{
    public SpecGet<FEATURE>WithDetails(Guid id)
    {
        Query
            .Where(x => x.Id == id)
            // .Include(x => x.Orders)           // TODO: Add includes
            // .Include(x => x.Preferences)
            ;  // TODO: Remove extra semicolon after last include
    }
}

// ==== Usage in Service ====
// public async Task<ENTITY?> GetByIdAsync(Guid id)
// {
//     var spec = new SpecGet<FEATURE>ById(id);
//     return await _repository.FirstOrDefaultAsync(spec);
// }

// public async Task<List<ENTITY>> GetByUserIdAsync(Guid userId)
// {
//     var spec = new SpecGet<FEATURE>sByUserId(userId);
//     return await _repository.ListAsync(spec);
// }
