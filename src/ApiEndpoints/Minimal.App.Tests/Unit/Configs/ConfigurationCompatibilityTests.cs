using Microsoft.Extensions.Configuration;
using Minimal.Api.Configs;
using Minimal.Api.Configs.RateLimits;
using Minimal.Share.Options;

namespace Minimal.App.Tests.Unit.Configs;

/// <summary>
/// DRK-1028 §5 (R6): an existing consumer's own configuration — generated from an earlier template version, so
/// it carries none of the new hardening keys — still binds and is honoured, and every hardening item this
/// change adds takes its secure default anyway (the code-level defaults on <see cref="FeatureOptions" /> and
/// the config classes' <c>?? new(...)</c> fallbacks). No host, no HTTP — a pure configuration-binding check, the
/// same layer as <c>Architecture/SecureDefaultAppSettingsTests</c>.
/// </summary>
public class ConfigurationCompatibilityTests
{
    /// <summary>
    /// Simulates a consumer's own appsettings.json from before this hardening cycle: only the keys a
    /// pre-hardening template ever wrote, plus one custom value the consumer themselves chose.
    /// </summary>
    private static IConfigurationRoot LegacyConsumerConfig() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FeatureManagement:EnableHttps"] = "true",
                ["FeatureManagement:EnableSwagger"] = "false",
                ["FeatureManagement:RequireAuthorization"] = "true",
                ["FeatureManagement:EnableRateLimit"] = "true",
                ["Cors:AllowedOrigins:0"] = "https://consumer.example",
                ["RateLimit:DefaultRequestLimit"] = "250" // the consumer's own custom value
            })
            .Build();

    [Fact]
    public void MissingHardeningKeys_StillBindEveryControlToItsSecureDefault()
    {
        var config = LegacyConsumerConfig();
        var features = config.GetSection(FeatureOptions.Name).Get<FeatureOptions>();

        features.ShouldNotBeNull();
        features.EnableSecurityHeaders.ShouldBeTrue();
        features.EnableForwardedHeaders.ShouldBeTrue();
        features.EnableRequestBounds.ShouldBeTrue();
    }

    [Fact]
    public void MissingTrustedProxies_DefaultsToEmpty()
    {
        var config = LegacyConsumerConfig();

        var trustedProxies = config.GetSection("Security:TrustedProxies").Get<string[]>() ?? [];

        trustedProxies.ShouldBeEmpty();
    }

    [Fact]
    public void MissingRequestBoundsSection_DefaultsToTheStatedSecureBounds()
    {
        var config = LegacyConsumerConfig();

        var bounds = config.GetSection(RequestBoundsOptions.Name).Get<RequestBoundsOptions>() ?? new RequestBoundsOptions();

        bounds.RequestTimeoutSeconds.ShouldBe(30);
        bounds.MaxRequestBodySizeBytes.ShouldBe(1 * 1024 * 1024);
        bounds.RequestHeadersTimeoutSeconds.ShouldBe(10);
    }

    [Fact]
    public void ExistingConsumerValues_StayHonoured()
    {
        var config = LegacyConsumerConfig();

        var features = config.GetSection(FeatureOptions.Name).Get<FeatureOptions>();
        features!.EnableHttps.ShouldBeTrue();
        features.EnableSwagger.ShouldBeFalse();
        features.RequireAuthorization.ShouldBeTrue();

        var origins = config.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        origins.ShouldContain("https://consumer.example");

        var rateLimit = config.GetSection(RateLimitOptions.Name).Get<RateLimitOptions>();
        rateLimit!.DefaultRequestLimit.ShouldBe(250,
            "the consumer's own explicit RateLimit:DefaultRequestLimit must still win over any class default.");
    }
}
