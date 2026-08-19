namespace Minimal.Api.Configs;

[ExcludeFromCodeCoverage]
internal static class HttpsConfig
{
    #region Methods

    public static IServiceCollection AddHttpsConfig(
        this IServiceCollection services,
        Action<HstsOptions>? configureOptions = null)
    {
        if (configureOptions is not null)
        {
            services.AddHsts(configureOptions);
        }
        else
        {
            services.AddHsts(c =>
            {
                c.Preload = true;
                c.IncludeSubDomains = true;
                c.MaxAge = TimeSpan.FromDays(30);
            });
        }

        services.MarkConfigAdded(nameof(HttpsConfig));
        return services;
    }

    public static WebApplication UseHttpsConfig(this WebApplication app)
    {
        if (app.Services.IsConfigAdded(nameof(HttpsConfig)))
        {
            app.UseHsts()
                .UseHttpsRedirection();
            Console.WriteLine("Hsts enabled.");
        }

        return app;
    }

    #endregion
}