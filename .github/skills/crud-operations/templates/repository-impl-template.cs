// ==================================================
// WRITE REPOSITORY IMPLEMENTATION TEMPLATE
// ==================================================
// Location: Minimal.Infra/Features/<FEATURE>/Repos/<FEATURE>Repository.cs
//
// INSTRUCTIONS:
// 1. Replace <FEATURE> with your entity name
// 2. Replace <ENTITY> with your domain entity class
// 3. Create sealed class (auto-discovered by Scrutor)
// 4. Keep ONLY write methods (Add, Update, Delete, SaveChanges)
// 5. Inject IRepository<Entity> for reads via Specs
// 6. Add this to your Infra project

using System;
using System.Threading.Tasks;
using <DOMAIN_NAMESPACE>.<ENTITY>;
using <APPSERVICES_NAMESPACE>.Repositories;
using <INFRA_NAMESPACE>.Contexts;

namespace <INFRA_NAMESPACE>.Features.<FEATURE>.Repos;

/// <summary>
/// Write-only repository for <ENTITY>
/// Reads use Query Specs injected as IRepository<Entity>
/// </summary>
public sealed class <FEATURE>Repository : I<FEATURE>Repository
{
    private readonly ApplicationDbContext _context;

    public <FEATURE>Repository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(ENTITY entity)
        => _context.<FEATURE_DBSET>.Add(entity);

    public async Task UpdateAsync(ENTITY entity)
        => _context.<FEATURE_DBSET>.Update(entity);

    public async Task DeleteAsync(Guid id)
    {
        var entity = await _context.<FEATURE_DBSET>.FindAsync(id);
        if (entity != null)
            _context.<FEATURE_DBSET>.Remove(entity);
    }

    public async Task SaveChangesAsync()
        => await _context.SaveChangesAsync();

    // ❌ NO read methods here
    // Inject IRepository<ENTITY> from Ardalis.Specification
    // and use Query Specs defined in AppServices/Specs/
}

