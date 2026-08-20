using DKNet.AspCore.Idempotency;
using DKNet.AspCore.Idempotency.RedisStore;
using Minimal.Api.Configs.Auth;
using Minimal.Api.Configs.GlobalExceptions;
using Minimal.Api.Configs.RateLimits;
using Minimal.Api.Configs.Swagger;

namespace Minimal.Api.Configs;

[ExcludeFromCodeCoverage]
internal static class AppConfig
{
    #region Methods

    public static IServiceCollection AddAppConfig(
        this IServiceCollection services,
        FeatureOptions features,
        IConfiguration configuration)
    {
        if (features.EnableAntiforgery)
        {
            services.AddAntiforgeryConfig();
        }

        if (features.RequireAuthorization)
        {
            services.AddAuthConfig();
        }

        if (features.EnableSwagger)
        {
            services.AddOpenApiDoc(features);
        }

        if (features.EnableHttps)
        {
            services.AddHttpsConfig();
        }

        if (features.EnableRateLimit)
        {
            services.AddRateLimitConfig(configuration);
        }

        if (features.EnableVersioning)
        {
            services.AddAppVersioning();
        }

        services.AddHttpContextAccessor()
            .AddFeatureManagement();

        services.CacheConfig(configuration);

        var redisConnectionString = configuration.GetConnectionString(SharedConsts.RedisConnectionString);
        if (!string.IsNullOrWhiteSpace(redisConnectionString))
        {
            services.AddIdempotencyWithRedisStore(
                redisConnectionString,
                o => o.ConflictHandling = IdempotentConflictHandling.CachedResult);
        }
        else
        {
#pragma warning disable CS0618 // AddIdempotentKey() (non-generic) is obsolete; accepted fallback when Redis is not configured.
            services.AddIdempotentKey(o => o.ConflictHandling = IdempotentConflictHandling.CachedResult);
#pragma warning restore CS0618
        }

        return services
            .AddCrosConfig()
            .AddGlobalException()
            .AddAllAppServices(configuration, features)
            .AddHealthzConfig(features);
    }

    public static Task UseAppConfig(this WebApplication app, Action<WebApplication>? extra = null)
    {
        app.UseAntiforgeryConfig()
            .UseCrosConfig()
            .UseHttpsConfig()
            .UseHealthzConfig();

        app.UseRouting();
        app.UseRateLimitConfig();

        //This must be after UseRouting
        app.UseAuthConfig();

        //This is UseEndpoints
        extra?.Invoke(app);

        //These have to be after UseEndpoints.
        app.UseOpenApiDoc()
            .UseGlobalException();

        return app.RunAsync();
    }

    #endregion
}