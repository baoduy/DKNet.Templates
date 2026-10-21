using System.Reflection;
using Microsoft.Extensions.Configuration;
using Minimal.Api.Configs.AzureAppConfig;

namespace DKNet.Templates.ScaffoldTests;

/// <summary>
/// DRK-2334: the API host reads its Azure App Configuration options from the <c>AzureAppConfiguration</c>
/// section and honours <c>Label</c>, <c>LoadFeatureFlags</c>, <c>FeatureFlagPrefix</c> and
/// <c>CacheExpirationInSeconds</c>. <c>AzureAppConfigSetup.AddAzureAppConfig</c> connects to a live store, so
/// these tests drive the options it binds and the decisions it reads from them, never the SDK call itself.
/// </summary>
public class AzureAppConfigOptionsTests
{
    #region Methods

    /// <summary>Binds the options the way <c>AzureAppConfigSetup.AddAzureAppConfig</c> does.</summary>
    private static AzureAppConfigOptions Bind(IConfiguration configuration) =>
        configuration.GetSection(AzureAppConfigOptions.Name).Get<AzureAppConfigOptions>() ??
        new AzureAppConfigOptions();

    private static AzureAppConfigOptions Bind(Dictionary<string, string?> data) =>
        Bind(new ConfigurationBuilder().AddInMemoryCollection(data).Build());

    private static AzureAppConfigOptions BindShippedAppSettings() =>
        Bind(new ConfigurationBuilder().AddJsonFile(AppSettingsScanner.BaseAppSettingsPath()).Build());

    /// <summary>D9: values under <c>AzureAppConfiguration</c> are read.</summary>
    [Fact]
    public void OptionsUnderAzureAppConfigurationSection_AreRead()
    {
        var options = Bind(new Dictionary<string, string?>
        {
            ["AzureAppConfiguration:Label"] = "prod",
            ["AzureAppConfiguration:LoadFeatureFlags"] = "false",
            ["AzureAppConfiguration:FeatureFlagPrefix"] = "Minimal.",
            ["AzureAppConfiguration:CacheExpirationInSeconds"] = "60"
        });

        options.Label.ShouldBe("prod");
        options.LoadFeatureFlags.ShouldBeFalse();
        options.FeatureFlagPrefix.ShouldBe("Minimal.");
        options.CacheExpirationInSeconds.ShouldBe(60);
    }

    /// <summary>D9: values under the old <c>AzureAppConfig</c> section are not read.</summary>
    [Fact]
    public void OptionsUnderOldAzureAppConfigSection_AreNotRead()
    {
        var options = Bind(new Dictionary<string, string?>
        {
            ["AzureAppConfig:Label"] = "prod"
        });

        options.Label.ShouldBeNull();
    }

    /// <summary>D2: the shipped <c>"Label": ""</c> binds, and still selects the service name.</summary>
    [Fact]
    public void ShippedAppSettings_EmptyLabel_SelectsServiceName()
    {
        var options = BindShippedAppSettings();

        options.Label.ShouldBe(string.Empty);
        options.ResolvedLabel.ShouldBe("Minimal.Api");
    }

    /// <summary>R6: the shipped <c>CacheExpirationInSeconds: 300</c> is the refresh interval, in seconds.</summary>
    [Fact]
    public void ShippedAppSettings_RefreshIntervalIs300Seconds()
    {
        BindShippedAppSettings().RefreshInterval.ShouldBe(TimeSpan.FromSeconds(300));
    }

    /// <summary>D1–D4 (R3): null, empty or whitespace selects the service name; any other value is kept.</summary>
    [Theory]
    [InlineData(null, "Minimal.Api")]
    [InlineData("", "Minimal.Api")]
    [InlineData("  ", "Minimal.Api")]
    [InlineData("prod", "prod")]
    public void ResolvedLabel_FollowsLabel(string? label, string expected)
    {
        new AzureAppConfigOptions { Label = label }.ResolvedLabel.ShouldBe(expected);
    }

    /// <summary>D5 (R4): <c>LoadFeatureFlags</c> true / false / absent → load / none / load.</summary>
    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData(null, true)]
    public void LoadFeatureFlags_IsReadFromAzureAppConfigurationSection(string? value, bool expected)
    {
        var data = new Dictionary<string, string?> { ["AzureAppConfiguration:Label"] = "prod" };
        if (value is not null) data["AzureAppConfiguration:LoadFeatureFlags"] = value;

        Bind(data).LoadFeatureFlags.ShouldBe(expected);
    }

    /// <summary>
    /// D6 (R5): absent or empty prefix selects all flags (no filter); a prefix selects only flags whose name
    /// starts with it. The SDK reserves <c>*</c>, <c>,</c> and <c>\</c> in a filter, so a prefix carrying
    /// them is escaped to match literally.
    /// </summary>
    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("  ", null)]
    [InlineData("Minimal.", "Minimal.*")]
    [InlineData(@"a,b*c\d", @"a\,b\*c\\d*")]
    public void FeatureFlagFilter_FollowsFeatureFlagPrefix(string? prefix, string? expected)
    {
        var data = new Dictionary<string, string?> { ["AzureAppConfiguration:Label"] = "prod" };
        if (prefix is not null) data["AzureAppConfiguration:FeatureFlagPrefix"] = prefix;

        Bind(data).FeatureFlagFilter.ShouldBe(expected);
    }

    /// <summary>D7/D8 (R6, Q2): absent / 300 / 60 / 0 / negative → 300 s / 300 s / 60 s / 300 s / 300 s.</summary>
    [Theory]
    [InlineData(null, 300)]
    [InlineData("300", 300)]
    [InlineData("60", 60)]
    [InlineData("0", 300)]
    [InlineData("-5", 300)]
    public void RefreshInterval_IsCacheExpirationInSeconds(string? value, int expectedSeconds)
    {
        var data = new Dictionary<string, string?> { ["AzureAppConfiguration:Label"] = "prod" };
        if (value is not null) data["AzureAppConfiguration:CacheExpirationInSeconds"] = value;

        Bind(data).RefreshInterval.ShouldBe(TimeSpan.FromSeconds(expectedSeconds));
    }

    /// <summary>Row 5: <c>CacheExpirationInSeconds</c> defaults to 300.</summary>
    [Fact]
    public void CacheExpirationInSeconds_DefaultsTo300()
    {
        new AzureAppConfigOptions().CacheExpirationInSeconds.ShouldBe(300);
    }

    /// <summary>D10: the connection-string name stays <c>AzureAppConfig</c>.</summary>
    [Fact]
    public void ConnectionStringName_DefaultsToAzureAppConfig()
    {
        new AzureAppConfigOptions().ConnectionStringName.ShouldBe("AzureAppConfig");
    }

    /// <summary>Row 7: every key in the base <c>AzureAppConfiguration</c> section binds to an options property.</summary>
    [Fact]
    public void BaseAppSettings_AzureAppConfigurationSection_HasOnlyBoundKeys()
    {
        var properties = typeof(AzureAppConfigOptions)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite)
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        using var doc = JsonDocument.Parse(File.ReadAllText(AppSettingsScanner.BaseAppSettingsPath()));
        doc.RootElement.TryGetProperty("AzureAppConfiguration", out var section)
            .ShouldBeTrue("the base appsettings.json must declare an AzureAppConfiguration section.");

        var unbound = section.EnumerateObject()
            .Select(k => k.Name)
            .Where(name => !properties.Contains(name))
            .ToArray();

        unbound.ShouldBeEmpty(
            "Every key under AzureAppConfiguration in the base appsettings.json must bind to a public settable " +
            $"AzureAppConfigOptions property. Unbound key(s): {string.Join(", ", unbound)}");
    }

    #endregion
}
