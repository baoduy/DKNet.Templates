using DKNet.EfCore.Specifications.Extensions;
using DKNet.EfCore.Specifications.Repositories;
using Minimal.AppServices.ManualSample.V1.Specs;
using X.PagedList;

namespace Minimal.AppServices.ManualSample.V1.Queries;

public sealed record ListPurchaseOrdersQuery : Fluents.Queries.IWitPageResponse<PurchaseOrderDto>
{
    public int PageIndex { get; init; } = 1;

    public int PageSize { get; init; } = 20;

    public string? CustomerName { get; init; }
}

internal sealed class ListPurchaseOrdersQueryHandler(IRepositorySpec repository, IMapper mapper)
    : Fluents.Queries.IPageHandler<ListPurchaseOrdersQuery, PurchaseOrderDto>
{
    public async Task<IPagedList<PurchaseOrderDto>> OnHandle(
        ListPurchaseOrdersQuery request,
        CancellationToken cancellationToken)
    {
        var spec = new SpecGetPurchaseOrder(byCustomerName: request.CustomerName);

        var page = await repository.ToPagedListAsync(spec, request.PageIndex, request.PageSize, cancellationToken);

        return new StaticPagedList<PurchaseOrderDto>(page.Select(mapper.Map<PurchaseOrderDto>), page);
    }
}
