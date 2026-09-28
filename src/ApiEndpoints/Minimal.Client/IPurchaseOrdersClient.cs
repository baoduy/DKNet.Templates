using Minimal.Client.Contracts;

namespace Minimal.Client;

/// <summary>Typed client for the Purchase Orders endpoints (manual sample) of the API.</summary>
/// <remarks>DRK-1789 acceptance-test stub: the Refit route attributes are the Build stage's §3 row 4.</remarks>
public interface IPurchaseOrdersClient
{
    /// <summary>Creates a purchase order. <paramref name="idempotencyKey" /> is required and is sent as
    /// the <c>X-Idempotency-Key</c> header; a null or blank key is refused before any request is sent.</summary>
    Task<PurchaseOrderResponse> CreatePurchaseOrderAsync(
        CreatePurchaseOrderRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<PagedResponse<PurchaseOrderResponse>> ListPurchaseOrdersAsync(
        PurchaseOrderListQuery? query = null,
        CancellationToken cancellationToken = default);

    Task<PurchaseOrderResponse> GetPurchaseOrderAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PurchaseOrderResponse> UpdatePurchaseOrderAsync(
        Guid id,
        UpdatePurchaseOrderRequest request,
        CancellationToken cancellationToken = default);

    Task<PurchaseOrderResponse> CancelPurchaseOrderAsync(Guid id, CancellationToken cancellationToken = default);

    Task DeletePurchaseOrderAsync(Guid id, CancellationToken cancellationToken = default);
}
