using X.PagedList;

namespace Minimal.Api.Configs.Endpoints;

internal sealed record PagedResponse<TResult>
{
    public PagedResponse()
    {
        Items = [];
    }

    public PagedResponse(IPagedList<TResult> list)
    {
        PageNumber = list.PageNumber;
        PageSize = list.PageSize;
        PageCount = list.PageCount;
        TotalItemCount = list.TotalItemCount;
        Items = [.. list];
    }

    public IList<TResult> Items { get; init; }
    public int PageCount { get; init; }

    public int PageNumber { get; init; }
    public int PageSize { get; init; }
    public int TotalItemCount { get; init; }
}