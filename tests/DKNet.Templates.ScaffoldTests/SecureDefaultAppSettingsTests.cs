using Microsoft.Extensions.Configuration;
using Minimal.Share.Options;

namespace DKNet.Templates.ScaffoldTests;

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
        var srcDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src"));
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
    /// DRK-1579 (R5): the base <c>appsettings.json</c> must never enable the built-in demonstration
    /// authentication provider — it authenticates every caller as a fixed, non-real identity and is a
    /// development/demonstration-only switch, never a Production default.
    /// </summary>
    [Fact]
    public void BaseAppSettings_DoesNotEnableDemoAuthentication()
    {
        var config = LoadBaseConfig();
        var features = config.GetSection(FeatureOptions.Name).Get<FeatureOptions>();

        features.ShouldNotBeNull();
        features.EnableDemoAuthentication.ShouldBeFalse(
            $"{nameof(FeatureOptions.EnableDemoAuthentication)}=true in the template's base appsettings.json " +
            "would ship an anonymous-as-demo-identity default to every Production-shaped host (DRK-1579 R5).");
    }

    /// <summary>
    /// The base file's explicit <c>RateLimit</c> section must survive: without it a host with
    /// <c>EnableRateLimit: true</c> silently falls back to <c>RateLimitOptions</c>' class defaults
    /// (DefaultRequestLimit 2), running a limiter far stricter than intended. Asserted against the JSON
    /// rather than the options type, which is internal to Minimal.Api and must stay that way.
    /// </summary>
    [Fact]
    public void BaseAppSettings_RateLimitSection_DeclaresExplicitValues_NotClassDefaults()
    {
        var config = LoadBaseConfig().GetSection("RateLimit");

        config.Exists().ShouldBeTrue("the base appsettings.json must declare an explicit RateLimit section.");
        config["DefaultRequestLimit"].ShouldBe("100");
        config["DefaultConcurrentLimit"].ShouldBe("20");
        config["TimeWindowInSeconds"].ShouldBe("1");
    }

    /// <summary>
    /// DRK-1570 (S6): the base <c>appsettings.json</c> must declare <c>Security:TrustedNetworks</c> as an
    /// array, alongside the existing single-IP <c>Security:TrustedProxies</c>, so a CIDR range can be trusted
    /// without an operator inventing the key from documentation alone. Asserted on the raw JSON, not
    /// <see cref="IConfiguration" />: the shipped default is an empty array, and
    /// <c>Microsoft.Extensions.Configuration.Json</c> emits no key for an empty array, so an
    /// <see cref="IConfiguration" />-based <c>Exists()</c>/<c>Get&lt;string[]&gt;()</c> assertion could never
    /// go green (the same reason <c>ForwardedHeadersConfig.cs:21</c> needs <c>?? []</c> against the
    /// already-shipped empty <c>TrustedProxies</c>) — same convention as
    /// <see cref="BaseAppSettings_RateLimitSection_DeclaresExplicitValues_NotClassDefaults" />.
    /// </summary>
    [Fact]
    public void BaseAppSettings_DeclaresTrustedNetworksKey_AsArray()
    {
        using var json = JsonDocument.Parse(File.ReadAllText(AppsettingsPath()));

        json.RootElement.TryGetProperty("Security", out var security).ShouldBeTrue(
            "the base appsettings.json must declare a Security section (DRK-1570).");
        security.TryGetProperty("TrustedNetworks", out var trustedNetworks).ShouldBeTrue(
            "the base appsettings.json must declare Security:TrustedNetworks (DRK-1570).");
        trustedNetworks.ValueKind.ShouldBe(JsonValueKind.Array,
            "Security:TrustedNetworks must be declared as a JSON array (DRK-1570).");
    }

    #endregion
}
