# Querying and Specifications

This page covers the read side of the template: how a query travels from an HTTP request to a
filtered, paged, projected result, and why it goes through `DKNet.EfCore.Specifications` instead
of a raw `IQueryable`.

## Why specifications, not raw `IQueryable`

Handlers never build a LINQ query against `CoreDbContext` directly. Instead they ask
`IRepositorySpec` for `FirstOrDefaultAsync(spec, ...)`, `AnyAsync(spec, ...)`, or
`ToPagedListAsync(spec, ...)`, passing a `Specification<TEntity>` from `DKNet.EfCore.Specifications`.

A spec is a reusable, named, testable filter. The same predicate isn't hand-rolled differently in
every handler that needs it — for example, "purchase orders for this customer". A spec can also be
unit-tested without spinning up EF Core or a database; see `Unit/*` and the testing section in
`docs/ddd-implementation-guide.md`.

For the package's full API, see DKNet's own `docs/EfCore/DKNet.EfCore.Specifications.md`.

> **Note.** This page covers the *hand-written* read path, where each list query declares its own
> parameters (see `ListPurchaseOrdersQuery` below). Generator-driven CRUD slices instead get a
> uniform filter/search/order/page list route for free — that separate contract is documented in
> [Generic List Endpoint](generic-list-endpoint.md).

### Worked spec: `SpecGetPurchaseOrder`

`Minimal.AppServices/ManualSample/V1/Specs/SpecGetPurchaseOrder.cs` builds one predicate that serves
both a single-record lookup and a filtered list, depending on which optional constructor argument is
supplied:

```csharp
internal sealed class SpecGetPurchaseOrder : Specification<PurchaseOrder>
{
    public SpecGetPurchaseOrder(Guid? byId = null, string? byCustomerName = null)
    {
        var predicator = CreatePredicate();

        if (byId is not null)
            predicator = predicator.And(a => a.Id == byId);

        if (!string.IsNullOrEmpty(byCustomerName))
            predicator = predicator.And(a => a.CustomerName == byCustomerName);

        if (byId is null && string.IsNullOrEmpty(byCustomerName))
            // An unstarted predicate builder compiles to WHERE FALSE — without this, "no filter"
            // would silently match nothing instead of listing every order.
            predicator = predicator.And(_ => true);

        WithFilter(predicator);
    }
}
```

The `byId is null && byCustomerName is empty` branch is not defensive filler. `CreatePredicate()`
with no `.And(...)` ever called compiles to `WHERE FALSE`, so an unfiltered "list everything" call
would silently return zero rows without it. Any spec you write that supports a no-filter call shape
needs the same guard.

### Generic spec: `ModelSpecStatusCounts<TEntity>`

`Minimal.AppServices/Share/Generics/ModelSpecGenericStatusCounts.cs` shows a spec parameterized over
any `DomainEntity`, filtering only on the audited `CreatedOn` column:

```csharp
public class ModelSpecStatusCounts<TEntity> : Specification<TEntity> where TEntity : DomainEntity
{
    public ModelSpecStatusCounts(GenericStatusCountsParameters parameters)
    {
        var predicate = CreatePredicate(x => true);
        if (parameters.From is { } from) predicate = predicate.And(x => x.CreatedOn >= from);
        if (parameters.To is { } to) predicate = predicate.And(x => x.CreatedOn <= to);
        WithFilter(predicate);
    }
}
```

Unlike `SpecGetPurchaseOrder`, this one seeds `CreatePredicate(x => true)` up front, so it needs no
separate "no filter given" branch. Both `From` and `To` are optional, and the predicate is already
non-empty either way.

## Queries as bus requests

A query is one of two record shapes dispatched through `IMessageBus.Send(...)` from the endpoint:
`Fluents.Queries.IWitResponse<TDto>` for a single result, or `Fluents.Queries.IWitPageResponse<TDto>`
for a paged result. See `docs/slimbus-messaging.md` for how that dispatch and its MediatR-equivalent
shapes work. Two worked examples live in `Minimal.AppServices/ManualSample/V1/Queries/`:

**`GetPurchaseOrderById.cs`** — single-record lookup:

```csharp
public sealed record GetPurchaseOrderByIdQuery : Fluents.Queries.IWitResponse<PurchaseOrderDto>
{
    public required Guid Id { get; init; }
}

internal sealed class GetPurchaseOrderByIdQueryHandler(IRepositorySpec repository, IMapper mapper)
    : Fluents.Queries.IHandler<GetPurchaseOrderByIdQuery, PurchaseOrderDto>
{
    public async Task<PurchaseOrderDto?> OnHandle(GetPurchaseOrderByIdQuery request, CancellationToken cancellationToken)
    {
        var order = await repository.FirstOrDefaultAsync(new SpecGetPurchaseOrder(request.Id), cancellationToken);
        return order is null ? null : mapper.Map<PurchaseOrderDto>(order);
    }
}
```

**`ListPurchaseOrders.cs`** — filtered, paged list, with its own validator:

```csharp
public sealed record ListPurchaseOrdersQuery : Fluents.Queries.IWitPageResponse<PurchaseOrderDto>
{
    public const int DefaultPageIndex = 1;
    public const int DefaultPageSize = 20;

    public int? PageIndex { get; init; }
    public int? PageSize { get; init; }
    public string? CustomerName { get; init; }
}

internal sealed class ListPurchaseOrdersQueryValidator : AbstractValidator<ListPurchaseOrdersQuery>
{
    public ListPurchaseOrdersQueryValidator()
    {
        RuleFor(a => a.PageSize).InclusiveBetween(1, 100).When(a => a.PageSize.HasValue);
        RuleFor(a => a.PageIndex).GreaterThan(0).When(a => a.PageIndex.HasValue);
    }
}

internal sealed class ListPurchaseOrdersQueryHandler(IRepositorySpec repository, IMapper mapper)
    : Fluents.Queries.IPageHandler<ListPurchaseOrdersQuery, PurchaseOrderDto>
{
    public async Task<IPagedList<PurchaseOrderDto>> OnHandle(ListPurchaseOrdersQuery request, CancellationToken cancellationToken)
    {
        var spec = new SpecGetPurchaseOrder(byCustomerName: request.CustomerName);
        var pageIndex = request.PageIndex ?? ListPurchaseOrdersQuery.DefaultPageIndex;
        var pageSize = request.PageSize ?? ListPurchaseOrdersQuery.DefaultPageSize;
        var page = await repository.ToPagedListAsync(spec, pageIndex, pageSize, cancellationToken);
        return new StaticPagedList<PurchaseOrderDto>(page.Select(mapper.Map<PurchaseOrderDto>), page);
    }
}
```

`PageIndex`/`PageSize` are declared `int?`, not `int`. This lets `[AsParameters]` binding distinguish
two cases: the caller omitted the query parameter, which falls back to
`DefaultPageIndex`/`DefaultPageSize`, and an explicit out-of-range value like `pageSize=0`, which must
still fail validation instead of silently falling back to the default.

### Paging and projection to DTOs

Paging itself comes from `DKNet.EfCore.Specifications.Extensions`: calling
`ToPagedListAsync(spec, pageIndex, pageSize, cancellationToken)` returns an `X.PagedList` page over
the entity. The handler never returns entities. It projects each page item through Mapster
(`mapper.Map<PurchaseOrderDto>`) and rewraps the projected items in a `StaticPagedList<TDto>`, so the
page metadata — total count, page index, page size — survives the entity-to-DTO conversion.

## Status-counts endpoint helper

`Minimal.Api/Configs/Endpoints/StatusCountsEndpointMapperExtensions.cs` provides
`MapGetStatusCounts<TEntity>`. It maps a `GET` route that returns grouped status counts for any
`DomainEntity`. The method is template-local, deliberately not part of the published
`DKNet.AspCore.Extensions` package — see the note in its own XML doc comment.

```csharp
public RouteHandlerBuilder MapGetStatusCounts<TEntity>(string endpoint = "status", params StatusPropertyInfo[] properties)
    where TEntity : DomainEntity
{
    return app.MapGet(endpoint, async ([AsParameters] GenericStatusCountsParameters parameters, [FromServices] IRepositorySpec repo) =>
    {
        var results = new List<StatusCountsResult>();
        foreach (var property in properties)
            results.AddRange(await repo.GetStatusCounts<TEntity>(property, parameters));
        return Results.Ok(results);
    }).CacheOutput().ProducesCommons().Produces<List<StatusCountsResult>>();
}
```

No current `Minimal.Api.ApiEndpoints` config actually calls `MapGetStatusCounts`, so there is no live
HTTP route for it in the shipped template today. It is exercised directly against
`IRepositorySpec.GetStatusCounts<TEntity>` in
`Minimal.App.Tests/Integration/StatusCounts/StatusCountsEndpointMapperExtensionsTests.cs`, which
proves the query itself against the real EF Core/DI stack. Wire it into an endpoint's
`Map(RouteGroupBuilder group)` the same way the other routes in `PurchaseOrderV1Endpoint` are wired,
if you want it reachable over HTTP.

`GetStatusCounts<TEntity>` runs the spec above and groups dynamically by the given property name,
using `System.Linq.Dynamic.Core`. Importantly, it backfills every enum member of `property.EnumType`
with a zero count when the database has no rows for that value, so a caller always sees the full set
of statuses rather than only the ones with data.

**Unbounded window by default.** `GenericStatusCountsParameters.From`/`To` are both optional, and
`ModelSpecStatusCounts<TEntity>` only narrows the query when one is supplied. A call with neither
bound reports counts over the entire history, not a rolling window. This was a deliberate breaking
change — see the "Breaking change — status-counts default window" note in the repo's `README.md`.
Pass explicit `From`/`To` if you need a bounded window, such as the last 30 days.
