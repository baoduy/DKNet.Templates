using HealthChecks.UI.Client;

namespace SlimBus.Api.Configs.Healthz;

[ExcludeFromCodeCoverage]
internal static class HealthzConfig
{
    #region Fields

    private static bool _configAdded;

    #endregion

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
        _configAdded = true;
        return services;
    }

    /// <summary>
    ///     The health check endpoint will be "/healthz"
    /// </summary>
    /// <param name="endpoints"></param>
    /// <returns></returns>
    public static WebApplication UseHealthzConfig(this WebApplication endpoints)
    {
        if (!_configAdded)
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