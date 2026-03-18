using System.Linq.Dynamic.Core;
using DKNet.EfCore.Specifications;
using DKNet.EfCore.Specifications.Extensions;
using SlimBus.Api.Configs.Endpoints;
using SlimBus.Api.Extensions;
using SlimBus.AppServices.Share.Generics;
using SlimBus.Domains.Share;
using X.PagedList;

// ReSharper disable once CheckNamespace
namespace Microsoft.AspNetCore.Builder;

/// <summary>
///     Provides extension methods for mapping fluent endpoints to ASP.NET Core route handlers.
/// </summary>
[ExcludeFromCodeCoverage]
internal static class FluentsEndpointMapperExtensions
{
    #region Methods

    /// <summary>
    ///     Adds common response types to the route handler builder for error handling.
    /// </summary>
    /// <param name="routeBuilder">The route handler builder.</param>
    /// <returns>The route handler builder with common error responses.</returns>
    private static RouteHandlerBuilder ProducesCommons(this RouteHandlerBuilder routeBuilder)
    {
        return routeBuilder
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status429TooManyRequests);
    }

    #endregion

    extension(RouteGroupBuilder app)
    {
        /// <summary>
        ///     Maps a DELETE endpoint for a command with a response.
        /// </summary>
        /// <typeparam name="TCommand">The command type implementing IWitResponse&lt;TResponse&gt;.</typeparam>
        /// <typeparam name="TResponse">The response type.</typeparam>
        /// <param name="endpoint">The endpoint path.</param>
        /// <returns>The route handler builder.</returns>
        public RouteHandlerBuilder MapDelete<TCommand, TResponse>(string endpoint = "/")
            where TCommand : class, Fluents.Requests.IWitResponse<TResponse>
        {
            return app.MapDelete(endpoint, async ([AsParameters] TCommand request, [FromServices] IMessageBus bus) =>
                {
                    var rs = await bus.Send(request).ConfigureAwait(false);
                    return rs.Response();
                }).Produces<TResponse>()
                .ProducesCommons();
        }

        /// <summary>
        ///     Maps a DELETE endpoint for a command without a response.
        /// </summary>
        /// <typeparam name="TCommand">The command type implementing INoResponse.</typeparam>
        /// <param name="endpoint">The endpoint path.</param>
        /// <returns>The route handler builder.</returns>
        public RouteHandlerBuilder MapDelete<TCommand>(string endpoint = "/")
            where TCommand : class, Fluents.Requests.INoResponse
        {
            return app.MapDelete(endpoint, async ([AsParameters] TCommand request, [FromServices] IMessageBus bus) =>
                {
                    var rs = await bus.Send(request).ConfigureAwait(false);
                    return rs.Response();
                }).Produces(StatusCodes.Status200OK)
                .ProducesCommons();
        }

        /// <summary>
        ///     Maps a GET endpoint for a query with a response.
        /// </summary>
        /// <typeparam name="TCommand">The query type implementing IWitResponse&lt;TResponse&gt;.</typeparam>
        /// <typeparam name="TResponse">The response type.</typeparam>
        /// <param name="endpoint">The endpoint path.</param>
        /// <returns>The route handler builder.</returns>
        public RouteHandlerBuilder MapGet<TCommand, TResponse>(string endpoint = "/")
            where TCommand : class, Fluents.Queries.IWitResponse<TResponse>
        {
            return app.MapGet(endpoint,
                    async ([AsParameters] TCommand request, [FromServices] IMessageBus bus) =>
                    {
                        var rs = await bus.Send(request).ConfigureAwait(false);
                        return rs is not null ? Results.Ok(rs) : Results.NotFound();
                    })
                .Produces<TResponse>()
                .ProducesCommons();
        }

        /// <summary>
        ///     Maps a GET endpoint for a paged query with a paged response.
        /// </summary>
        /// <typeparam name="TCommand">The query type implementing IWitPageResponse&lt;TResponse&gt;.</typeparam>
        /// <typeparam name="TResponse">The response type.</typeparam>
        /// <param name="endpoint">The endpoint path.</param>
        /// <returns>The route handler builder.</returns>
        public RouteHandlerBuilder MapGetPage<TCommand, TResponse>(string endpoint = "/")
            where TCommand : class, Fluents.Queries.IWitPageResponse<TResponse>
        {
            return app.MapGet(endpoint,
                    async ([AsParameters] TCommand request, [FromServices] IMessageBus bus) =>
                    {
                        var rs = await bus.Send(request).ConfigureAwait(false);
                        return Results.Ok(new PagedResponse<TResponse>(rs));
                    })
                .Produces<IPagedList<TResponse>>()
                .ProducesCommons();
        }

        /// <summary>
        ///     Maps a PATCH endpoint for a command with a response.
        /// </summary>
        /// <typeparam name="TCommand">The command type implementing IWitResponse&lt;TResponse&gt;.</typeparam>
        /// <typeparam name="TResponse">The response type.</typeparam>
        /// <param name="endpoint">The endpoint path.</param>
        /// <returns>The route handler builder.</returns>
        public RouteHandlerBuilder MapPatch<TCommand, TResponse>(string endpoint = "/")
            where TCommand : class, Fluents.Requests.IWitResponse<TResponse>
        {
            return app.MapPatch(endpoint, async (TCommand request, [FromServices] IMessageBus bus) =>
                {
                    var rs = await bus.Send(request).ConfigureAwait(false);
                    return rs.Response();
                }).Produces<TResponse>()
                .ProducesCommons();
        }

        /// <summary>
        ///     Maps a PATCH endpoint for a command without a response.
        /// </summary>
        /// <typeparam name="TCommand">The command type implementing INoResponse.</typeparam>
        /// <param name="endpoint">The endpoint path.</param>
        /// <returns>The route handler builder.</returns>
        public RouteHandlerBuilder MapPatch<TCommand>(string endpoint = "/")
            where TCommand : class, Fluents.Requests.INoResponse
        {
            return app.MapPatch(endpoint, async (TCommand request, [FromServices] IMessageBus bus) =>
                {
                    var rs = await bus.Send(request).ConfigureAwait(false);
                    return rs.Response();
                })
                .Produces(StatusCodes.Status200OK)
                .ProducesCommons();
        }

        /// <summary>
        ///     Maps a POST endpoint for a command with a response.
        /// </summary>
        /// <typeparam name="TCommand">The command type implementing IWitResponse&lt;TResponse&gt;.</typeparam>
        /// <typeparam name="TResponse">The response type.</typeparam>
        /// <param name="endpoint">The endpoint path.</param>
        /// <returns>The route handler builder.</returns>
        public RouteHandlerBuilder MapPost<TCommand, TResponse>(string endpoint = "/")
            where TCommand : class, Fluents.Requests.IWitResponse<TResponse>
        {
            var isCreating = typeof(TCommand).Name.Contains("Create", StringComparison.OrdinalIgnoreCase);
            return app.MapPost(endpoint, async (TCommand request, [FromServices] IMessageBus bus) =>
                {
                    var rs = await bus.Send(request).ConfigureAwait(false);
                    return rs.Response(isCreating);
                }).Produces<TResponse>(isCreating ? StatusCodes.Status201Created : StatusCodes.Status200OK)
                .ProducesCommons();
        }

        /// <summary>
        ///     Maps a POST endpoint for a command without a response.
        /// </summary>
        /// <typeparam name="TCommand">The command type implementing INoResponse.</typeparam>
        /// <param name="endpoint">The endpoint path.</param>
        /// <returns>The route handler builder.</returns>
        public RouteHandlerBuilder MapPost<TCommand>(string endpoint = "/")
            where TCommand : class, Fluents.Requests.INoResponse
        {
            var isCreating = typeof(TCommand).Name.Contains("Create", StringComparison.OrdinalIgnoreCase);
            return app.MapPost(endpoint, async (TCommand request, [FromServices] IMessageBus bus) =>
                {
                    var rs = await bus.Send(request).ConfigureAwait(false);
                    return rs.Response(isCreating);
                }).Produces(isCreating ? StatusCodes.Status201Created : StatusCodes.Status200OK)
                .ProducesCommons();
        }

        /// <summary>
        ///     Maps a PUT endpoint for a command with a response.
        /// </summary>
        /// <typeparam name="TCommand">The command type implementing IWitResponse&lt;TResponse&gt;.</typeparam>
        /// <typeparam name="TResponse">The response type.</typeparam>
        /// <param name="endpoint">The endpoint path.</param>
        /// <returns>The route handler builder.</returns>
        public RouteHandlerBuilder MapPut<TCommand, TResponse>(string endpoint = "/")
            where TCommand : class, Fluents.Requests.IWitResponse<TResponse>
        {
            return app.MapPut(endpoint, async (TCommand request, [FromServices] IMessageBus bus) =>
                {
                    var rs = await bus.Send(request).ConfigureAwait(false);
                    return rs.Response();
                }).Produces<TResponse>()
                .ProducesCommons();
        }

        /// <summary>
        ///     Maps a PUT endpoint for a command without a response.
        /// </summary>
        /// <typeparam name="TCommand">The command type implementing INoResponse.</typeparam>
        /// <param name="endpoint">The endpoint path.</param>
        /// <returns>The route handler builder.</returns>
        public RouteHandlerBuilder MapPut<TCommand>(string endpoint = "/")
            where TCommand : class, Fluents.Requests.INoResponse
        {
            return app.MapPut(endpoint, async (TCommand request, [FromServices] IMessageBus bus) =>
            {
                var rs = await bus.Send(request).ConfigureAwait(false);
                return rs.Response();
            }).ProducesCommons();
        }

        /// <summary>
        ///     Gets a paged list of entities endpoint.
        /// </summary>
        /// <param name="endpoint"></param>
        /// <typeparam name="TEntity"></typeparam>
        /// <typeparam name="TModel"></typeparam>
        /// <returns></returns>
        public RouteHandlerBuilder MapGetList<TEntity, TModel>(string endpoint = "/")
            where TEntity : DomainEntity where TModel : class
        {
            return app.MapGet(endpoint,
                    async ([AsParameters] GenericListParameters request, [FromServices] IRepositorySpec repo) =>
                    {
                        var item = await repo.ToPagedListAsync(
                                new ModelSpecGenericList<TEntity, TModel>(request),
                                request.PageNumberValue, request.PageSizeValue)
                            .ConfigureAwait(false);
                        return Results.Ok(new PagedResponse<TModel>(item));
                    }).ProducesCommons()
                .Produces<PagedResult<TModel>>()
                .WithDescription($"Retrieve a list {typeof(TEntity).Name}.");
        }

        /// <summary>
        ///     Gets an entity by its ID endpoint.
        /// </summary>
        /// <param name="endpoint"></param>
        /// <typeparam name="TEntity"></typeparam>
        /// <typeparam name="TModel"></typeparam>
        /// <returns></returns>
        public RouteHandlerBuilder MapGetById<TEntity, TModel>(string endpoint = "{id:guid}")
            where TEntity : DomainEntity where TModel : class
        {
            return app.MapGet(endpoint, async (Guid id, [FromServices] IRepositorySpec repo) =>
                {
                    var item = await repo.FirstOrDefaultAsync(new ModelSpecGenericById<TEntity, TModel>(id))
                        .ConfigureAwait(false);
                    return item is null ? Results.NotFound() : Results.Ok(item);
                }).ProducesCommons()
                .Produces<TModel>()
                .WithDescription($"Retrieve a {typeof(TEntity).Name} by its ID.");
        }

        /// <summary>
        ///     Gets status counts endpoint.
        /// </summary>
        /// <param name="properties"></param>
        /// <param name="endpoint"></param>
        /// <typeparam name="TEntity"></typeparam>
        /// <returns></returns>
        public RouteHandlerBuilder MapGetStatusCounts<TEntity>(string endpoint = "status",
            params StatusPropertyInfo[] properties) where TEntity : DomainEntity
        {
            return app.MapGet(endpoint,
                    async ([AsParameters] GenericStatusCountsParameters parameters,
                        [FromServices] IRepositorySpec repo) =>
                    {
                        var results = new List<StatusCountsResult>();
                        foreach (var property in properties)
                        {
                            var counts = await repo.GetStatusCounts<TEntity>(property, parameters).ConfigureAwait(false);
                            results.AddRange(counts);
                        }
                        return Results.Ok(results);
                    })
                .CacheOutput()
                .ProducesCommons()
                .Produces<List<StatusCountsResult>>()
                .WithDescription(
                    $"Retrieve grouped counts of '{string.Join(',', properties.Select(p => p.Name))}' for {typeof(TEntity).Name} within date range.");
        }
    }
}