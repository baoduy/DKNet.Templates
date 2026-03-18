// ==================================================
// SERVICE TEMPLATE (with Specs)
// ==================================================
// Location: SlimBus.AppServices/Features/<FEATURE>/Services/<FEATURE>Service.cs
//
// INSTRUCTIONS:
// 1. Replace <FEATURE> with your entity name (e.g., "CustomerProfile")
// 2. Replace <ENTITY> with your domain entity class
// 3. Inject IRepository<Entity> for reads (uses Specs)
// 4. Inject I<FEATURE>Repository for writes
// 5. Inject EventPublisher for domain events
// 6. Implement read/write methods using specs and repository
// 7. Add this to your AppServices project

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ardalis.Specification;
using <DOMAIN_NAMESPACE>.<ENTITY>;
using <APPSERVICES_NAMESPACE>.Features.<FEATURE>.V1;
using <APPSERVICES_NAMESPACE>.Features.<FEATURE>.Events;
using <APPSERVICES_NAMESPACE>.Features.<FEATURE>.Repositories;
using <APPSERVICES_NAMESPACE>.Specs;
using <INFRA_NAMESPACE>.Services;
using Mapster;

namespace <APPSERVICES_NAMESPACE>.Features.<FEATURE>.Services;

public sealed class <FEATURE>Service
{
    // Injections:
    // - IRepository<Entity>: For read operations (Specs)
    // - I<FEATURE>Repository: For write operations
    // - EventPublisher: For domain events
    
    private readonly IRepository<<ENTITY>> _repository;
    private readonly I<FEATURE>Repository _writeRepo;
    private readonly EventPublisher _eventPublisher;

    public <FEATURE>Service(
        IRepository<<ENTITY>> repository,
        I<FEATURE>Repository writeRepo,
        EventPublisher eventPublisher)
    {
        _repository = repository;
        _writeRepo = writeRepo;
        _eventPublisher = eventPublisher;
    }

    // ==== READ OPERATIONS (using Specs) ====
    
    public async Task<<ENTITY>?> GetByIdAsync(Guid id)
    {
        var spec = new SpecGet<FEATURE>ById(id);
        return await _repository.FirstOrDefaultAsync(spec);
    }

    // TODO: Add other read methods
    // public async Task<<ENTITY>?> GetByEmailAsync(string email)
    // {
    //     var spec = new SpecGet<FEATURE>ByEmail(email);
    //     return await _repository.FirstOrDefaultAsync(spec);
    // }

    // public async Task<List<<ENTITY>>> GetByUserIdAsync(Guid userId)
    // {
    //     var spec = new SpecGet<FEATURE>sByUserId(userId);
    //     return await _repository.ListAsync(spec);
    // }

    // ==== WRITE OPERATIONS ====
    
    public async Task<Guid> CreateAsync(
        Create<FEATURE>Request request,
        Guid userId)
    {
        // TODO: Add validation (e.g., check for duplicates using spec)
        
        // Create entity
        var entity = <ENTITY>.Create(
            userId,
            request.FullName);  // TODO: Add request properties

        // Write to repository
        await _writeRepo.AddAsync(entity);
        await _writeRepo.SaveChangesAsync();

        // Publish event
        await _eventPublisher.Publish(
            new <FEATURE>CreatedEvent(
                entity.Id,
                entity.UserId,
                DateTime.UtcNow));

        return entity.Id;
    }

    public async Task UpdateAsync(Guid id, Update<FEATURE>Request request)
    {
        var spec = new SpecGet<FEATURE>ById(id);
        var entity = await _repository.FirstOrDefaultAsync(spec);
        
        if (entity == null)
            throw new InvalidOperationException($"{nameof(<ENTITY>)} {id} not found");

        entity.Update(request.FullName);  // TODO: Add request properties

        await _writeRepo.UpdateAsync(entity);
        await _writeRepo.SaveChangesAsync();

        await _eventPublisher.Publish(
            new <FEATURE>UpdatedEvent(entity.Id, DateTime.UtcNow));
    }

    public async Task DeleteAsync(Guid id)
    {
        await _writeRepo.DeleteAsync(id);
        await _writeRepo.SaveChangesAsync();

        await _eventPublisher.Publish(
            new <FEATURE>DeletedEvent(id, DateTime.UtcNow));
    }
}
