// ==================================================
// WRITE REPOSITORY INTERFACE TEMPLATE
// ==================================================
// Location: SlimBus.AppServices/Features/<FEATURE>/Repositories/I<FEATURE>Repository.cs
//
// INSTRUCTIONS:
// 1. Replace <FEATURE> with your entity name (e.g., "CustomerProfile")
// 2. Replace <ENTITY> with your domain entity class
// 3. Keep ONLY write operations (Add, Update, Delete, SaveChanges)
// 4. Read operations use Specs instead (see spec-template.cs)
// 5. Add this to your AppServices project

using System;
using System.Threading.Tasks;
using <DOMAIN_NAMESPACE>.<ENTITY>;

namespace SlimBus.AppServices.Features.<FEATURE>.Repositories;

/// <summary>
/// Write-only repository for <ENTITY>
/// Read operations use Query Specs (see AppServices/Specs/)
/// </summary>
public interface I<FEATURE>Repository
{
    // Write operations only
    Task AddAsync(ENTITY entity);
    Task UpdateAsync(ENTITY entity);
    Task DeleteAsync(Guid id);
    Task SaveChangesAsync();
    
    // ❌ DO NOT add read methods here
    // Instead create Specs in AppServices/Specs/ folder
    // Usage: var spec = new SpecGetCustomerProfileById(id);
    //        var entity = await _repository.FirstOrDefaultAsync(spec);
}

