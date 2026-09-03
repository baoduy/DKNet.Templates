namespace Minimal.Share.Options;

/// <summary>
///     Configuration options for application feature toggles.
/// </summary>
public class FeatureOptions
{
    #region Properties

    /// <summary>
    ///     Gets or sets a value indicating whether anti-forgery token validation is enabled.
    /// </summary>
    public bool EnableAntiforgery { get; set; }

    /// <summary>
    ///     Enable Azure App Configuration for remote configuration and feature management
    /// </summary>
    public bool EnableAzureAppConfig { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether health checks are enabled. Default is true.
    /// </summary>
    public bool EnableHealthCheck { get; set; } = true;

    /// <summary>
    ///     Gets or sets a value indicating whether HTTPS redirection is enabled.
    /// </summary>
    public bool EnableHttps { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether OpenTelemetry instrumentation is enabled.
    /// </summary>
    public bool EnableOpenTelemetry { get; set; }

    /// <summary>
    ///     Enable Rate Limiting
    /// </summary>
    public bool EnableRateLimit { get; set; } = true;

    /// <summary>
    ///     Gets or sets a value indicating whether the service bus is enabled.
    /// </summary>
    public bool EnableServiceBus { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether Swagger/OpenAPI documentation is enabled.
    /// </summary>
    public bool EnableSwagger { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether API versioning is enabled. Default is true.
    /// </summary>
    public bool EnableVersioning { get; set; } = true;

    /// <summary>
    ///     Gets or sets a value indicating whether authorization is required for API endpoints.
    /// </summary>
    public bool RequireAuthorization { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether database migrations should run when the application starts.
    /// </summary>
    public bool RunDbMigrationWhenAppStart { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether OWASP-recommended security response headers are added to every
    ///     response. Default is true.
    /// </summary>
    public bool EnableSecurityHeaders { get; set; } = true;

    /// <summary>
    ///     Gets or sets a value indicating whether <c>X-Forwarded-For</c>/<c>X-Forwarded-Proto</c> are honoured
    ///     from the proxies listed in <c>Security:TrustedProxies</c>. Default is true; with an empty trusted-proxy
    ///     list, forwarded values are ignored regardless of this flag.
    /// </summary>
    public bool EnableForwardedHeaders { get; set; } = true;

    /// <summary>
    ///     Gets or sets a value indicating whether request lifetime, max body size and header-read timeout are
    ///     enforced instead of inherited from Kestrel's own defaults. Default is true.
    /// </summary>
    public bool EnableRequestBounds { get; set; } = true;

    /// <summary>
    ///     Gets the configuration section name for feature management.
    /// </summary>
    public static string Name => "FeatureManagement";

    #endregion
}