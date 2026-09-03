using OwaspHeaders.Core;
using OwaspHeaders.Core.Extensions;

namespace Minimal.Api.Configs;

[ExcludeFromCodeCoverage]
internal static class SecurityHeadersConfig
{
    #region Methods

    public static IServiceCollection AddSecurityHeadersConfig(this IServiceCollection services, FeatureOptions features)
    {
        if (!features.EnableSecurityHeaders)
        {
            return services;
        }

        services.MarkConfigAdded(nameof(SecurityHeadersConfig));
        return services;
    }

    /// <summary>
    ///     OwaspHeaders.Core's middleware writes its headers straight onto <c>HttpContext.Response.Headers</c>
    ///     before calling the next delegate. <c>UseExceptionHandler</c>'s <c>IExceptionHandler</c> path calls
    ///     <c>Response.Clear()</c> (which empties Headers) before writing the problem response, so headers added
    ///     the normal way never reach an unhandled-exception response. Deferring the same header-write call to
    ///     <c>HttpResponse.OnStarting</c> runs it right before the response actually sends — after any such
    ///     clear — so success, 404 and unhandled-500 responses all carry the headers (R5).
    /// </summary>
    public static WebApplication UseSecurityHeadersConfig(this WebApplication app)
    {
        if (!app.Services.IsConfigAdded(nameof(SecurityHeadersConfig)))
        {
            return app;
        }

        // HttpsConfig (HttpsConfig.cs) owns Strict-Transport-Security — UseHsts() deliberately not called here
        // to avoid emitting it twice.
        var config = SecureHeadersMiddlewareBuilder
            .CreateBuilder()
            .UseXFrameOptions()
            .UseContentTypeOptions()
            .UseDefaultContentSecurityPolicy()
            .UsePermittedCrossDomainPolicies()
            .UseReferrerPolicy()
            .UseCacheControl()
            .UseXssProtection()
            .UseCrossOriginResourcePolicy()
            .Build();

        var headerWriter = new SecureHeadersMiddleware(_ => Task.CompletedTask, config);

        app.Use(async (context, next) =>
        {
            context.Response.OnStarting(() => headerWriter.InvokeAsync(context));
            await next();
        });

        Console.WriteLine("Security Headers enabled.");
        return app;
    }

    #endregion
}
