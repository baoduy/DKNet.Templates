namespace Minimal.Api.Configs;

[ExcludeFromCodeCoverage]
internal static class CrosConfig
{
    #region Methods

    public static IServiceCollection AddCrosConfig(this IServiceCollection services)
    {
        services.AddCors(c => c.AddDefaultPolicy(o => o.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
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