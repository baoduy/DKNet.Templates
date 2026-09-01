using Microsoft.Extensions.Configuration;
using Minimal.Api.Configs.RateLimits;
using Minimal.Share.Options;

namespace Minimal.App.Tests.Architecture;

/// <summary>
/// DRK-901 (SEC-007): the base <c>appsettings.json</c> is what ships to Production for every host that does
/// not layer a Development/Testing overlay on top of it — a <see langword="false"/> security flag there is an
/// anonymous, plaintext, unthrottled service by default. This reads the base file directly (no host, no
/// environment overlay) so it cannot be masked by <c>Minimal.App.TestSupport.TestApiFactoryBase</c> forcing the
/// "Testing" environment on every other Integration test in this suite.
/// </summary>
public class SecureDefaultAppSettingsTests
{
    #region Methods

    private static string AppsettingsPath()
    {
        var srcDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
        return Path.Combine(srcDir, "ApiEndpoints/Minimal.Api/appsettings.json");
    }

    private static IConfigurationRoot LoadBaseConfig()
    {
        var path = AppsettingsPath();
        File.Exists(path).ShouldBeTrue($"base config not found at {path}");

        return new ConfigurationBuilder().AddJsonFile(path, optional: false).Build();
    }

    /// <summary>
    /// Only the flags this finding is about. <c>EnableAntiforgery</c> is deliberately excluded: it guards
    /// cookie-based form posts, and this template's <c>Authentication</c> section is Bearer-scheme only — a
    /// <see langword="false"/> default there is not the "anonymous plaintext service" regression SEC-007
    /// targets, so pinning it here would just be padding, not a security assertion.
    /// </summary>
    [Theory]
    [InlineData(nameof(FeatureOptions.RequireAuthorization))]
    [InlineData(nameof(FeatureOptions.EnableHttps))]
    [InlineData(nameof(FeatureOptions.EnableRateLimit))]
    public void BaseAppSettings_SecurityFeatureFlag_MustNotBeFalse(string flagName)
    {
        var config = LoadBaseConfig();
        var features = config.GetSection(FeatureOptions.Name).Get<FeatureOptions>();

        features.ShouldNotBeNull();
        var value = flagName switch
        {
            nameof(FeatureOptions.RequireAuthorization) => features.RequireAuthorization,
            nameof(FeatureOptions.EnableHttps) => features.EnableHttps,
            nameof(FeatureOptions.EnableRateLimit) => features.EnableRateLimit,
            _ => throw new ArgumentOutOfRangeException(nameof(flagName))
        };

        value.ShouldBeTrue(
            $"{flagName}=false in the template's base appsettings.json ships an insecure default to every " +
            "Production-shaped host that boots without a Development/Testing overlay (SEC-007).");
    }

    /// <summary>
    /// <see cref="RateLimitOptions"/> defaults <c>DefaultRequestLimit</c> to 2 — a value chosen for the
    /// class, not the product. The base file's explicit <c>RateLimit</c> section must be what actually binds,
    /// or a host with <c>EnableRateLimit: true</c> would silently run a limiter far stricter than intended.
    /// </summary>
    [Fact]
    public void BaseAppSettings_RateLimitSection_BindsExplicitValues_NotClassDefaults()
    {
        var config = LoadBaseConfig();
        var rateLimit = config.GetSection(RateLimitOptions.Name).Get<RateLimitOptions>();

        rateLimit.ShouldNotBeNull();
        rateLimit.DefaultRequestLimit.ShouldBe(100,
            "the base file's explicit RateLimit:DefaultRequestLimit must bind — 2 would mean the section " +
            "was renamed/removed and the host silently fell back to RateLimitOptions' class default.");
        rateLimit.DefaultConcurrentLimit.ShouldBe(20);
        rateLimit.TimeWindowInSeconds.ShouldBe(1);
    }

    #endregion
}
