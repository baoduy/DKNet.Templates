namespace Minimal.Api.Configs;

[ExcludeFromCodeCoverage]
internal static class VersioningConfig
{
    #region Methods

    public static IServiceCollection AddAppVersioning(this IServiceCollection services)
    {
        // AV0021 nudges toward AddApiExplorer(), which lives in the MVC-only Asp.Versioning.Mvc.ApiExplorer
        // package. This is a minimal-API app documented via Microsoft.AspNetCore.OpenApi, so that package
        // doesn't apply — suppress the diagnostic rather than pull in an unused MVC dependency.
#pragma warning disable AV0021
        services.AddEndpointsApiExplorer()
            .AddApiVersioning(op =>
            {
                op.DefaultApiVersion = new ApiVersion(1, 0);
                op.ReportApiVersions = true;
                op.AssumeDefaultVersionWhenUnspecified = true;
                op.ApiVersionReader = new UrlSegmentApiVersionReader();
            });
#pragma warning restore AV0021
        return services;
    }

    #endregion
}