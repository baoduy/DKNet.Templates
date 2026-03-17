// ==================================================
// COMMAND HANDLER TEMPLATE (IHandler Implementation)
// ==================================================
// Location: SlimBus.AppServices/Features/<FEATURE>/V1/Actions/<Action>.cs
//   (Same file as request type, or separate internal class)
//
// INSTRUCTIONS:
// 1. Create internal sealed class for each request type
// 2. Implement IHandler<TRequest, TResponse> or IHandler<TRequest>
// 3. Receive IRepositorySpec for queries, IMapper for mapping, IEventPublisher for events
// 4. Class must be discoverable by message bus (internal or public, auto-scanned)
// 5. Return Result<T> or Result for success/failure handling
// 6. Use domain methods for state mutations (not direct property sets)
// 7. Query entities using Specs pattern

using DKNet.EfCore.Specifications;
using Fluents.Requests;
using Mapster;

namespace SlimBus.AppServices.Features.<FEATURE>.V1.Actions;

// ========== CREATE HANDLER ==========

/// <summary>
/// Handler for Create<FEATURE>Request.
/// Creates new <FEATURE>, checks duplicates via Spec, publishes domain event.
/// </summary>
internal sealed class Create<FEATURE>CommandHandler :
    IHandler<Create<FEATURE>Request, <FEATURE>Dto>
{
    private readonly IMapper _mapper;
    private readonly IRepositorySpec _repo;
    private readonly IEventPublisher _eventPublisher;

    public Create<FEATURE>CommandHandler(
        IMapper mapper,
        IRepositorySpec repo,
        IEventPublisher eventPublisher)
    {
        _mapper = mapper;
        _repo = repo;
        _eventPublisher = eventPublisher;
    }

    public async Task<Result<<FEATURE>Dto>> Handle(
        Create<FEATURE>Request request,
        CancellationToken cancellationToken)
    {
        // ✓ Check for duplicates using Spec
        var spec = new Spec<FEATURE>GetByEmail(request.Email);
        var existing = await _repo.FirstOrDefaultAsync(spec, cancellationToken);
        if (existing != null)
            return Result<<FEATURE>Dto>.Invalid("Email already exists");

        // ✓ Create entity via factory method
        var entity = <FEATURE>.Create(
            userId: request.UserId,  // ByUser from RequestBase
            name: request.Name,
            email: request.Email,
            phone: request.Phone);

        // ✓ Persist to database
        await _repo.AddAsync(entity, cancellationToken);
        await _repo.SaveChangesAsync(cancellationToken);

        // ✓ Publish domain event
        await _eventPublisher.PublishAsync(
            new <FEATURE>CreatedEvent(
                entity.Id,
                entity.UserId,
                entity.Email,
                entity.CreatedAt),
            cancellationToken);

        // ✓ Return mapped response
        var dto = _mapper.Map<<FEATURE>Dto>(entity);
        return Result<<FEATURE>Dto>.Success(dto);
    }
}

// ========== UPDATE HANDLER ==========

/// <summary>
/// Handler for Update<FEATURE>Request.
/// Updates only non-null fields (partial update).
/// Returns NotFound if <FEATURE> doesn't exist.
/// </summary>
internal sealed class Update<FEATURE>CommandHandler :
    IHandler<Update<FEATURE>Request, <FEATURE>Dto>
{
    private readonly IMapper _mapper;
    private readonly IRepositorySpec _repo;

    public Update<FEATURE>CommandHandler(IMapper mapper, IRepositorySpec repo)
    {
        _mapper = mapper;
        _repo = repo;
    }

    public async Task<Result<<FEATURE>Dto>> Handle(
        Update<FEATURE>Request request,
        CancellationToken cancellationToken)
    {
        // ✓ Get existing entity using Spec
        var entity = await _repo.FirstOrDefaultAsync(
            new Spec<FEATURE>GetById(request.Id),
            cancellationToken);

        if (entity == null)
            return Result<<FEATURE>Dto>.NotFound("<FEATURE> not found");

        // ✓ Update only non-null fields
        if (!string.IsNullOrEmpty(request.Email))
            entity.Email = request.Email;

        if (!string.IsNullOrEmpty(request.Name))
            entity.SetName(request.Name);  // Use domain method, don't just set property

        if (!string.IsNullOrEmpty(request.Phone))
            entity.Phone = request.Phone;

        // ✓ Persist changes
        await _repo.UpdateAsync(entity, cancellationToken);
        await _repo.SaveChangesAsync(cancellationToken);

        // ✓ Return mapped response
        var dto = _mapper.Map<<FEATURE>Dto>(entity);
        return Result<<FEATURE>Dto>.Success(dto);
    }
}

// ========== DELETE HANDLER ==========

/// <summary>
/// Handler for Delete<FEATURE>Request.
/// Soft-deletes by marking IsDeleted = true.
/// Returns NotFound if <FEATURE> doesn't exist.
/// </summary>
internal sealed class Delete<FEATURE>CommandHandler :
    IHandler<Delete<FEATURE>Request>
{
    private readonly IRepositorySpec _repo;

    public Delete<FEATURE>CommandHandler(IRepositorySpec repo)
    {
        _repo = repo;
    }

    public async Task<Result> Handle(
        Delete<FEATURE>Request request,
        CancellationToken cancellationToken)
    {
        // ✓ Get existing entity
        var entity = await _repo.FirstOrDefaultAsync(
            new Spec<FEATURE>GetById(request.Id),
            cancellationToken);

        if (entity == null)
            return Result.NotFound("<FEATURE> not found");

        // ✓ Soft delete (mark as deleted)
        entity.MarkAsDeleted();

        // ✓ Persist
        await _repo.UpdateAsync(entity, cancellationToken);
        await _repo.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

// ========== CUSTOM ACTION: APPROVE HANDLER ==========

/// <summary>
/// Handler for Approve<FEATURE>Request.
/// Updates <FEATURE> status to Approved.
/// </summary>
internal sealed class Approve<FEATURE>CommandHandler :
    IHandler<Approve<FEATURE>Request, <FEATURE>Dto>
{
    private readonly IMapper _mapper;
    private readonly IRepositorySpec _repo;

    public Approve<FEATURE>CommandHandler(IMapper mapper, IRepositorySpec repo)
    {
        _mapper = mapper;
        _repo = repo;
    }

    public async Task<Result<<FEATURE>Dto>> Handle(
        Approve<FEATURE>Request request,
        CancellationToken cancellationToken)
    {
        // ✓ Get entity
        var entity = await _repo.FirstOrDefaultAsync(
            new Spec<FEATURE>GetById(request.Id),
            cancellationToken);

        if (entity == null)
            return Result<<FEATURE>Dto>.NotFound("<FEATURE> not found");

        // ✓ Call domain method to change state
        entity.Approve(request.Reason);

        // ✓ Persist
        await _repo.UpdateAsync(entity, cancellationToken);
        await _repo.SaveChangesAsync(cancellationToken);

        var dto = _mapper.Map<<FEATURE>Dto>(entity);
        return Result<<FEATURE>Dto>.Success(dto);
    }
}

// ========== CUSTOM ACTION: REJECT HANDLER ==========

/// <summary>
/// Handler for Reject<FEATURE>Request.
/// Updates <FEATURE> status to Rejected with required reason.
/// </summary>
internal sealed class Reject<FEATURE>CommandHandler :
    IHandler<Reject<FEATURE>Request, <FEATURE>Dto>
{
    private readonly IMapper _mapper;
    private readonly IRepositorySpec _repo;

    public Reject<FEATURE>CommandHandler(IMapper mapper, IRepositorySpec repo)
    {
        _mapper = mapper;
        _repo = repo;
    }

    public async Task<Result<<FEATURE>Dto>> Handle(
        Reject<FEATURE>Request request,
        CancellationToken cancellationToken)
    {
        // ✓ Get entity
        var entity = await _repo.FirstOrDefaultAsync(
            new Spec<FEATURE>GetById(request.Id),
            cancellationToken);

        if (entity == null)
            return Result<<FEATURE>Dto>.NotFound("<FEATURE> not found");

        // ✓ Call domain method with reason
        entity.Reject(request.Reason);

        // ✓ Persist
        await _repo.UpdateAsync(entity, cancellationToken);
        await _repo.SaveChangesAsync(cancellationToken);

        var dto = _mapper.Map<<FEATURE>Dto>(entity);
        return Result<<FEATURE>Dto>.Success(dto);
    }
}

// ========== KEY PATTERNS ==========

/*
IMPORTANT PATTERNS:

1. QUERY via Spec:
   var entity = await _repo.FirstOrDefaultAsync(new SpecEntityGetById(id), cancellationToken);
   var list = await _repo.ToPagedListAsync(new SpecEntityList(filters), page, pageSize, cancellationToken);

2. PERSIST:
   await _repo.AddAsync(entity, cancellationToken);          // For new
   await _repo.UpdateAsync(entity, cancellationToken);       // For existing
   await _repo.SaveChangesAsync(cancellationToken);          // Always after add/update/delete

3. DOMAIN METHODS (not direct property sets):
   entity.Approve(reason);              // NOT: entity.Status = Status.Approved
   entity.SetName(name);                // NOT: entity.Name = name
   entity.MarkAsDeleted();              // NOT: entity.IsDeleted = true

4. RETURN RESULTS:
   Result<T>.Success(dto)               // 200 OK or 201 Created
   Result<T>.NotFound(message)          // 404 Not Found
   Result<T>.Invalid(message)           // 400 Bad Request
   Result.Success()                     // 204 NoContent (for delete)

5. ERROR HANDLING:
   - Check NotNull before proceeding
   - Return Result.Invalid for validation errors
   - Return Result.NotFound for missing entities
   - Let exceptions propagate (framework catches and logs)

6. EVENT PUBLISHING:
   await _eventPublisher.PublishAsync(new DomainEvent(...), cancellationToken);
   - Only on successful creates or state changes
   - After SaveChangesAsync
   - Let handlers subscribe to events
*/
