using Microsoft.Extensions.DependencyInjection;

namespace Minimal.Client;

/// <summary>Registers the typed API client in a downstream system's service collection.</summary>
public static class ApiClientServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IProductsClient" /> and <see cref="IPurchaseOrdersClient" /> against
    /// <paramref name="baseAddress" />. <paramref name="handlerFactory" />, when given, supplies the caller's
    /// own <see cref="DelegatingHandler" /> (for example one that attaches the caller's bearer token); the
    /// client attaches no credential itself.
    /// </summary>
    /// <returns>The <see cref="IHttpClientBuilder" /> shared by both clients, for further configuration.</returns>
    /// <remarks>DRK-1789 acceptance-test stub — implemented in the Build stage (§3 rows 5 and 6).</remarks>
    public static IHttpClientBuilder AddApiClient(
        this IServiceCollection services,
        Uri baseAddress,
        Func<DelegatingHandler>? handlerFactory = null) =>
        throw new NotImplementedException();
}
