using Minimal.Client.Contracts;

namespace Minimal.Client;

/// <summary>Typed client for the Products endpoints (automated sample) of the API.</summary>
/// <remarks>DRK-1789 acceptance-test stub: the Refit route attributes are the Build stage's §3 row 4.</remarks>
public interface IProductsClient
{
    Task<ProductResponse> GetProductAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PagedResponse<ProductResponse>> ListProductsAsync(
        ProductListQuery? query = null,
        CancellationToken cancellationToken = default);

    Task<ProductResponse> CreateProductAsync(
        CreateProductRequest request,
        CancellationToken cancellationToken = default);

    Task<ProductResponse> ChangeProductPriceAsync(
        Guid id,
        ChangeProductPriceRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteProductAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ProductResponse> ApproveProductAsync(
        Guid id,
        ApproveProductRequest request,
        CancellationToken cancellationToken = default);

    Task<ProductResponse> AssignSupplierReferenceAsync(
        Guid id,
        AssignSupplierReferenceRequest request,
        CancellationToken cancellationToken = default);

    Task<ProductResponse> DiscontinueProductAsync(
        Guid id,
        DiscontinueProductRequest request,
        CancellationToken cancellationToken = default);

    Task<ProductPriceSummaryResponse> GetProductSummaryAsync(CancellationToken cancellationToken = default);
}
