using Minimal.AppServices.Crud;

namespace Minimal.Api.ApiEndpoints.AutomatedSample;

/// <summary>
/// Maps the fully generated CRUD slice for <c>Product</c> — nothing hand-mapped, the generator's own
/// <c>MapProductCrud</c> extension registers GetById/GetList/Create/Update/Delete.
/// </summary>
internal sealed class ProductV1Endpoint : IEndpointConfig
{
    #region Properties

    public int Version => 1;

    public string GroupEndpoint => "/products";

    #endregion

    #region Methods

    public void Map(RouteGroupBuilder group)
    {
        group.WithDescription("Automated sample — Product CRUD generated from [CrudCreate]/[CrudUpdate]/[RaisesEvent].");
        group.MapProductCrud();
        
        
    }

    #endregion
}
