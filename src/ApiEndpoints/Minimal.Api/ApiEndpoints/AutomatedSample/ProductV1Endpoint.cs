using DKNet.AspCore.Extensions.Responses;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Minimal.AppServices.AutomatedSample.V1;
using Minimal.AppServices.AutomatedSample.V1.Actions;
using Minimal.AppServices.AutomatedSample.V1.Queries;
using Minimal.AppServices.Crud;

namespace Minimal.Api.ApiEndpoints.AutomatedSample;

/// <summary>
/// Composite-first: the generated CRUD slice for <c>Product</c> is mapped first, each generated route
/// carrying its own authorization scope, then the two hand-written business routes below it — one
/// replacing a generated route the composite rule outgrew, one with no generated shape at all.
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
        // R2: a route's authorization metadata resolves against policies AddAuthConfig registers. With
        // RequireAuthorization off, the host never calls AddAuthConfig, so RequireAuthorization(...) on a
        // route would throw at request time — every scope call below is gated on this same flag.
        var requireAuthorization = ((IEndpointRouteBuilder)group).ServiceProvider
            .GetRequiredService<IOptions<FeatureOptions>>().Value.RequireAuthorization;

        group.WithDescription(
            "Automated sample — Product CRUD generated from [CrudCreate]/[CrudUpdate]/[RaisesEvent], " +
            "plus two hand-written business routes below it.");

        group.MapProductCrud(o =>
        {
            // Dropped by name and replaced below — see that route's comment for why (R1).
            o.Exclude("Discontinue");

            if (!requireAuthorization)
            {
                return;
            }

            o.Configure(CrudOp.GetById, rb => rb.RequireAuthorization(ProductScopes.Read));
            o.Configure(CrudOp.GetList, rb => rb.RequireAuthorization(ProductScopes.Read));
            o.Configure(CrudOp.Create, rb => rb.RequireAuthorization(ProductScopes.Write));
            o.Configure(CrudOp.Update, rb => rb.RequireAuthorization(ProductScopes.Write));
            o.Configure(CrudOp.Delete, rb => rb.RequireAuthorization(ProductScopes.Write));
            o.Configure("Approve", rb => rb.RequireAuthorization(ProductScopes.Write));
            // Its own scope, not Write — holding only products.write must not be enough to assign it.
            o.Configure("AssignSupplierReference", rb => rb.RequireAuthorization(ProductScopes.Supplier));
        });

        // An operation that writes more than one aggregate in one transaction cannot be generated:
        // discontinuing a product also creates its named replacement, so this route is hand-written and
        // dropped from the generated block above by name rather than left to the generator.
        var discontinue = group.MapPut("{id:guid}/discontinue", async (
                Guid id,
                DiscontinueProductCommand req,
                IMessageBus bus,
                CancellationToken ct) =>
            {
                var result = await bus.Send(req with { Id = id }, cancellationToken: ct);
                return result.Response();
            })
            .Produces<ProductDto>()
            .WithDescription("Discontinue a product and create its named replacement in the same transaction.");

        if (requireAuthorization)
        {
            discontinue.RequireAuthorization(ProductScopes.Discontinue);
        }

        var summary = group.MapGet("summary", async (
                IMessageBus bus,
                CancellationToken ct) =>
            {
                var result = await bus.Send(new ProductPriceSummaryQuery(), cancellationToken: ct);
                return Results.Ok(result);
            })
            .Produces<ProductPriceSummaryDto>()
            .WithDescription("Product count and average price across every product the caller can see.");

        if (requireAuthorization)
        {
            summary.RequireAuthorization(ProductScopes.Read);
        }
    }

    #endregion
}
