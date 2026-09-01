namespace Minimal.Api.Configs;

[ExcludeFromCodeCoverage]
internal static class CrosConfig
{
    #region Methods

    public static IServiceCollection AddCrosConfig(this IServiceCollection services, IConfiguration configuration)
    {
        var origins = (configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
            .Where(o => !string.IsNullOrWhiteSpace(o))
            .ToArray();

        if (origins.Length == 0)
        {
            return services;
        }

        services.AddCors(c => c.AddDefaultPolicy(o => o.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()));
        services.MarkConfigAdded(nameof(CrosConfig));
        return services;
    }

    public static WebApplication UseCrosConfig(this WebApplication app)
    {
        if (app.Services.IsConfigAdded(nameof(CrosConfig)))
        {
            app.UseCors();
            Console.WriteLine("CROS enabled.");
        }

        return app;
    }

    #endregion
}