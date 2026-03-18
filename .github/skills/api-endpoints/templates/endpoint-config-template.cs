// ==================================================
// ENDPOINT CONFIGURATION TEMPLATE (IEndpointConfig)
// ==================================================
// Location: Minimal.Api/ApiEndpoints/<FEATURE>V1Endpoints.cs
//
// INSTRUCTIONS:
// 1. Replace <FEATURE> with your entity name (e.g., "Profile")
// 2. Update GroupEndpoint and API version
// 3. Use MapGetList/MapGetById for reads (generic)
// 4. Use MapPost/MapPut/MapDelete for CRUD writes
// 5. Use MapPatch for custom actions (approve, reject, etc.)
// 6. This class is auto-discovered and registered

using Fluents.Builder;

namespace Minimal.Api.ApiEndpoints;

/// <summary>
/// Endpoint configuration for <FEATURE> API.
/// Auto-registered via IEndpointConfig interface discovery.
/// </summary>
internal sealed class <FEATURE>V1Endpoint : IEndpointConfig
{
    #region IEndpointConfig Implementation

    public int Version => 1;
    
    public string GroupEndpoint => "/<features>";  // TODO: Change route (e.g., "/customer-profiles")

    public void Map(RouteGroupBuilder group)
    {
        // ========== READ OPERATIONS (Generic Fluent Mappers) ==========
        // These are auto-handled by the framework from Spec pattern
        
        /// <summary>
        /// GET /api/v1/<features>
        /// Returns paginated list with optional filtering and sorting.
        /// </summary>
        group.MapGetList<<FEATURE>, <FEATURE>Dto>("")
            .WithDescription("Get paginated list of <features>");

        /// <summary>
        /// GET /api/v1/<features>/{id}
        /// Returns single <feature> by ID.
        /// </summary>
        group.MapGetById<<FEATURE>, <FEATURE>Dto>("{id:guid}")
            .WithDescription("Get <feature> by ID");

        // ========== WRITE OPERATIONS (Custom Request Commands) ==========
        // Each maps to a custom request record + handler
        
        /// <summary>
        /// POST /api/v1/<features>
        /// Creates new <feature>.
        /// Request: Create<FEATURE>Request
        /// Response: <FEATURE>Dto (201 Created)
        /// </summary>
        group.MapPost<Create<FEATURE>Request, <FEATURE>Dto>("")
            .WithDescription("Create new <feature>");

        /// <summary>
        /// PUT /api/v1/<features>/{id}
        /// Updates existing <feature> (partial update; null fields ignored).
        /// Request: Update<FEATURE>Request
        /// Response: <FEATURE>Dto (200 OK)
        /// </summary>
        group.MapPut<Update<FEATURE>Request, <FEATURE>Dto>("{id:guid}")
            .WithDescription("Update <feature> by ID");

        /// <summary>
        /// DELETE /api/v1/<features>/{id}
        /// Soft-deletes <feature>.
        /// Response: 204 NoContent
        /// </summary>
        group.MapDelete<Delete<FEATURE>Request>("{id:guid}")
            .WithDescription("Delete <feature> by ID");

        // ========== CUSTOM ACTIONS (MapPatch) ==========
        // Example: Approve, Reject, Publish, Archive, etc.
        
        /// <summary>
        /// PATCH /api/v1/<features>/{id}/approve
        /// Custom domain action: approve pending <feature>.
        /// Request: Approve<FEATURE>Request
        /// Response: <FEATURE>Dto (200 OK)
        /// </summary>
        group.MapPatch<Approve<FEATURE>Request, <FEATURE>Dto>("{id:guid}/approve")
            .WithDescription("Approve <feature>");

        /// <summary>
        /// PATCH /api/v1/<features>/{id}/reject
        /// Custom domain action: reject pending <feature> with reason.
        /// Request: Reject<FEATURE>Request
        /// Response: <FEATURE>Dto (200 OK)
        /// </summary>
        group.MapPatch<Reject<FEATURE>Request, <FEATURE>Dto>("{id:guid}/reject")
            .WithDescription("Reject <feature> with reason");
    }

    #endregion
}
