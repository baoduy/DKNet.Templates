using HealthChecks.UI.Client;

namespace Minimal.Api.Configs.Healthz;

[ExcludeFromCodeCoverage]
internal static class HealthzConfig
{
    #region Methods

    public static IServiceCollection AddHealthzConfig(this IServiceCollection services, FeatureOptions features)
    {
        if (!features.EnableHealthCheck)
        {
            return services;
        }

        services.AddHealthChecks()
            .AddDbContextCheck<DbContext>()
            .AddCheck<HealthCheckHandler>(SharedConsts.ApiName);
        services.MarkConfigAdded(nameof(HealthzConfig));
        return services;
    }

    /// <summary>
    ///     The health check endpoint will be "/healthz"
    /// </summary>
    /// <param name="endpoints"></param>
    /// <returns></returns>
    public static WebApplication UseHealthzConfig(this WebApplication endpoints)
    {
        if (!endpoints.Services.IsConfigAdded(nameof(HealthzConfig)))
        {
            return endpoints;
        }

        var options = new HealthCheckOptions
        {
            AllowCachingResponses = false,
            Predicate = _ => true,
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
        };
        endpoints.MapHealthChecks("/healthz", options);
        endpoints.MapHealthChecks("/", options);
        Console.WriteLine("Healthz enabled.");

        return endpoints;
    }

    #endregion
}