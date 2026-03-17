// ==================================================
// RESPONSE DTO TEMPLATE (Auto-Generated with [GenerateDto])
// ==================================================
// Location: SlimBus.AppServices/Features/<FEATURE>/V1/<FEATURE>Dto.cs
//
// INSTRUCTIONS:
// 1. Replace <FEATURE> with your entity name (e.g., "CustomerProfile")
// 2. Replace <ENTITY> with your domain entity class (e.g., "CustomerProfile")
// 3. The [GenerateDto] attribute auto-generates all public properties from entity
// 4. Keep record as "partial" - code generator fills in properties during compile
// 5. Use Exclude parameter to hide sensitive fields (passwords, hashes, internal notes)
// 6. Add to your AppServices project; used in all endpoint responses

using DKNet.EfCore.DtoGenerator;
using Mapster;
using SlimBus.Domains.Features.<FEATURE>.Entities;

namespace SlimBus.AppServices.Features.<FEATURE>.V1;

/// <summary>
/// Auto-generated response DTO for <FEATURE>.
/// 
/// The [GenerateDto] attribute generates all public properties from <ENTITY>.
/// Keep this partial - code generator provides implementation at compile-time.
/// 
/// Used in GET responses, POST 201, PUT 200, PATCH 200 responses.
/// 
/// EXAMPLES:
/// - No exclusions (expose all):         [GenerateDto(typeof(<ENTITY>), Exclude = [])]
/// - Exclude password:                   [GenerateDto(typeof(<ENTITY>), Exclude = [nameof(<ENTITY>.Password)])]
/// - Exclude multiple:                   [GenerateDto(typeof(<ENTITY>), Exclude = [nameof(<ENTITY>.Password), nameof(<ENTITY>.InternalNotes)])]
/// </summary>
[GenerateDto(typeof(<ENTITY>), Exclude = [])]  // TODO: Add excluded properties if needed
[MapsFrom(typeof(<ENTITY>))]
public sealed partial record <FEATURE>Dto;

// GENERATED CONTENT (added by code generator):
// 
// public sealed partial record <FEATURE>Dto
// {
//     public Guid Id { get; init; }
//     public string Name { get; init; }
//     public string Email { get; init; }
//     public string? Phone { get; init; }
//     public DateTime CreatedAt { get; init; }
//     public DateTime UpdatedAt { get; init; }
//     // ... all other mapped properties from <FEATURE> entity
// }
