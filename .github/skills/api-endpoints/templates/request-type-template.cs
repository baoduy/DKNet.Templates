// ==================================================
// REQUEST TYPE TEMPLATE (Custom CRUD & Action Commands)
// ==================================================
// Location: SlimBus.AppServices/Features/<FEATURE>/V1/Actions/<Action>.cs
//
// INSTRUCTIONS:
// 1. Each CRUD operation needs one request record
// 2. Inherit from RequestBase (provides user context: ByUser, IsIdentity, etc.)
// 3. Implement IWitResponse<TResponse> for commands that return data
// 4. Implement INoResponse for commands that return no data (e.g., Delete)
// 5. Use [MapsFrom(typeof(Entity))] for Mapster mapping
// 6. Use [Required], [StringLength], [Phone], etc. for validation hints
// 7. Each request type must have corresponding Handler and Validator

using Fluents.Requests;
using Mapster;

namespace SlimBus.AppServices.Features.<FEATURE>.V1.Actions;

// ========== CREATE REQUEST ==========

/// <summary>
/// Command to create a new <FEATURE>.
/// Implements IWitResponse<T> because it returns a response DTO.
/// </summary>
[MapsFrom(typeof(<FEATURE>))]
public sealed record Create<FEATURE>Request : RequestBase, IWitResponse<<FEATURE>Dto>
{
    /// <summary>Required field example: email must be unique.</summary>
    [Required]
    public string Email { get; set; } = null!;

    /// <summary>Full name, limited to 150 characters.</summary>
    [StringLength(150)]
    [Required]
    public string Name { get; set; } = null!;

    /// <summary>Optional phone number if provided.</summary>
    [Phone]
    public string? Phone { get; set; }
}

// ========== UPDATE REQUEST ==========

/// <summary>
/// Command to update an existing <FEATURE>.
/// Supports partial updates: only non-null fields will be updated.
/// Implements IWitResponse<T> because it returns updated DTO.
/// </summary>
[MapsFrom(typeof(<FEATURE>))]
public sealed record Update<FEATURE>Request : RequestBase, IWitResponse<<FEATURE>Dto>
{
    /// <summary>ID of the <FEATURE> to update (from route parameter).</summary>
    public required Guid Id { get; init; }

    /// <summary>Optional: new email. If null, current value preserved.</summary>
    public string? Email { get; init; }

    /// <summary>Optional: new name. If null, current value preserved.</summary>
    public string? Name { get; init; }

    /// <summary>Optional: new phone. If null, current value preserved.</summary>
    public string? Phone { get; init; }
}

// ========== DELETE REQUEST ==========

/// <summary>
/// Command to delete an existing <FEATURE>.
/// Implements INoResponse because delete returns 204 NoContent (no body).
/// </summary>
public sealed record Delete<FEATURE>Request : RequestBase, INoResponse
{
    /// <summary>ID of the <FEATURE> to delete (from route parameter).</summary>
    public required Guid Id { get; init; }
}

// ========== CUSTOM ACTION: APPROVE ==========

/// <summary>
/// Custom domain action: approve a pending <FEATURE>.
/// Use in endpoint: group.MapPatch<Approve<FEATURE>Request, <FEATURE>Dto>("{id:guid}/approve")
/// </summary>
public sealed record Approve<FEATURE>Request : RequestBase, IWitResponse<<FEATURE>Dto>
{
    /// <summary>ID of the <FEATURE> to approve (from route parameter).</summary>
    public required Guid Id { get; init; }

    /// <summary>Optional reason or comment for audit trail.</summary>
    public string? Reason { get; init; }
}

// ========== CUSTOM ACTION: REJECT ==========

/// <summary>
/// Custom domain action: reject a pending <FEATURE> with required reason.
/// Use in endpoint: group.MapPatch<Reject<FEATURE>Request, <FEATURE>Dto>("{id:guid}/reject")
/// </summary>
public sealed record Reject<FEATURE>Request : RequestBase, IWitResponse<<FEATURE>Dto>
{
    /// <summary>ID of the <FEATURE> to reject (from route parameter).</summary>
    public required Guid Id { get; init; }

    /// <summary>Required reason for rejection (audit trail).</summary>
    [Required]
    public string Reason { get; init; } = null!;
}

// ========== ADDITIONAL CUSTOM ACTIONS (Examples) ==========

/// <summary>
/// Custom action example: publish a <FEATURE>.
/// </summary>
public sealed record Publish<FEATURE>Request : RequestBase, IWitResponse<<FEATURE>Dto>
{
    public required Guid Id { get; init; }
    
    /// <summary>Optional: publish date (if scheduling is supported).</summary>
    public DateTime? PublishDate { get; init; }
}

/// <summary>
/// Custom action example: archive a <FEATURE>.
/// </summary>
public sealed record Archive<FEATURE>Request : RequestBase, IWitResponse<<FEATURE>Dto>
{
    public required Guid Id { get; init; }
}

/// <summary>
/// Custom action example: restore/unarchive a <FEATURE>.
/// </summary>
public sealed record Restore<FEATURE>Request : RequestBase, IWitResponse<<FEATURE>Dto>
{
    public required Guid Id { get; init; }
}
