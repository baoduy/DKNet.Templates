using Microsoft.Extensions.Configuration;
using Minimal.Share.Options;

namespace DKNet.Templates.ScaffoldTests;

/// <summary>
/// DRK-1903 (SEC-007): <see cref="FeatureOptions.RequireAuthorization" /> and
/// <see cref="FeatureOptions.EnableHttps" /> must default to <see langword="true" /> in code, so a host whose
/// configuration omits either key fails closed (auth required, HTTPS redirection) instead of silently running
/// anonymous over plain HTTP. <c>Program.cs</c> binds with <c>Get&lt;FeatureOptions&gt;()</c>, which falls back
/// to the property initialiser for any absent key. <see cref="SecureDefaultAppSettingsTests" /> pins the shipped
/// JSON; this pins the class default the JSON can no longer mask. Each case asserts one flag, so a failure names
/// the flag.
/// </summary>
public class FeatureOptionsClassDefaultTests
{
    #region Methods

    private static bool ReadFlag(FeatureOptions features, string flagName) => flagName switch
    {
        nameof(FeatureOptions.RequireAuthorization) => features.RequireAuthorization,
        nameof(FeatureOptions.EnableHttps) => features.EnableHttps,
        _ => throw new ArgumentOutOfRangeException(nameof(flagName))
    };

    private static FeatureOptions BindFeatureSection(Dictionary<string, string?> data)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(data).Build();
        var features = configuration.GetSection(FeatureOptions.Name).Get<FeatureOptions>();
        features.ShouldNotBeNull();
        return features;
    }

    /// <summary>S1 (@new, R1/R2): a freshly constructed <see cref="FeatureOptions" /> is secure by default.</summary>
    [Theory]
    [InlineData(nameof(FeatureOptions.RequireAuthorization))]
    [InlineData(nameof(FeatureOptions.EnableHttps))]
    public void NewFeatureOptions_SecurityFlag_DefaultsToTrue(string flagName)
    {
        var features = new FeatureOptions();

        ReadFlag(features, flagName).ShouldBeTrue(
            $"new FeatureOptions().{flagName} must default to true (DRK-1903, SEC-007).");
    }

    /// <summary>
    /// S2 (@new, R1/R2): binding a <c>FeatureManagement</c> section that omits both keys — the
    /// <c>Program.cs:9</c> path — yields <see langword="true" /> for each.
    /// </summary>
    [Theory]
    [InlineData(nameof(FeatureOptions.RequireAuthorization))]
    [InlineData(nameof(FeatureOptions.EnableHttps))]
    public void BoundSection_KeyAbsent_SecurityFlag_IsTrue(string flagName)
    {
        var features = BindFeatureSection(new Dictionary<string, string?>
        {
            ["FeatureManagement:EnableSwagger"] = "false"
        });

        ReadFlag(features, flagName).ShouldBeTrue(
            $"FeatureManagement:{flagName} absent from configuration must bind to true (DRK-1903, SEC-007).");
    }

    /// <summary>
    /// S3 (@new, R3): an explicit <see langword="false" /> in configuration still wins over the class default,
    /// so the Development/Testing overlays keep working.
    /// </summary>
    [Theory]
    [InlineData(nameof(FeatureOptions.RequireAuthorization))]
    [InlineData(nameof(FeatureOptions.EnableHttps))]
    public void BoundSection_KeyExplicitlyFalse_SecurityFlag_IsFalse(string flagName)
    {
        var features = BindFeatureSection(new Dictionary<string, string?>
        {
            ["FeatureManagement:RequireAuthorization"] = "false",
            ["FeatureManagement:EnableHttps"] = "false"
        });

        ReadFlag(features, flagName).ShouldBeFalse(
            $"FeatureManagement:{flagName}=false must bind to false (DRK-1903, R3).");
    }

    #endregion
}
