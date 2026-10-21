using Microsoft.Extensions.Configuration;
using Minimal.Api.Configs.AzureAppConfig;

namespace DKNet.Templates.ScaffoldTests;

/// <summary>
/// DRK-2350 reproduction: the label selector (<c>op.Select(KeyFilter.Any, label)</c>) also fetches
/// <c>.appconfig.featureflag/*</c> keys carrying the service label, so a flag is loaded even when
/// <c>LoadFeatureFlags</c> is false or its name is outside <c>FeatureFlagPrefix</c>. The setup keeps a fetched
/// setting only when <c>AzureAppConfigOptions.ShouldLoadSetting</c> says so; these tests drive that decision,
/// never the SDK call (it connects to a live store).
/// </summary>
public class AzureAppConfigFeatureFlagFilterTests
{
    #region Methods

    /// <summary>D1 (R2): a flag the label selector fetches is dropped when flags are off.</summary>
    [Fact]
    public void FlagKey_LoadFeatureFlagsFalse_IsDropped()
    {
        var options = new AzureAppConfigOptions { LoadFeatureFlags = false, FeatureFlagPrefix = "" };

        options.ShouldLoadSetting(".appconfig.featureflag/Beta").ShouldBeFalse();
    }

    /// <summary>D2 (R2): flags off wins over a matching prefix.</summary>
    [Fact]
    public void FlagKeyMatchingPrefix_LoadFeatureFlagsFalse_IsDropped()
    {
        var options = new AzureAppConfigOptions { LoadFeatureFlags = false, FeatureFlagPrefix = "Minimal." };

        options.ShouldLoadSetting(".appconfig.featureflag/Minimal.Beta").ShouldBeFalse();
    }

    /// <summary>D3 (R4): a flag whose name is outside the prefix is dropped.</summary>
    [Fact]
    public void FlagKeyOutsidePrefix_LoadFeatureFlagsTrue_IsDropped()
    {
        var options = new AzureAppConfigOptions { LoadFeatureFlags = true, FeatureFlagPrefix = "Minimal." };

        options.ShouldLoadSetting(".appconfig.featureflag/Other.Beta").ShouldBeFalse();
    }

    /// <summary>D4 (R4): the prefix compares case-sensitively, like the SDK filter.</summary>
    [Fact]
    public void FlagKeyPrefixDiffersOnlyInCase_IsDropped()
    {
        var options = new AzureAppConfigOptions { LoadFeatureFlags = true, FeatureFlagPrefix = "minimal." };

        options.ShouldLoadSetting(".appconfig.featureflag/Minimal.Beta").ShouldBeFalse();
    }

    /// <summary>D5 (R4): a flag whose name starts with the prefix is kept when flags are on.</summary>
    [Fact]
    public void FlagKeyMatchingPrefix_LoadFeatureFlagsTrue_IsKept()
    {
        var options = new AzureAppConfigOptions { LoadFeatureFlags = true, FeatureFlagPrefix = "Minimal." };

        options.ShouldLoadSetting(".appconfig.featureflag/Minimal.Beta").ShouldBeTrue();
    }

    /// <summary>D6 (R3): an empty or whitespace prefix keeps every flag when flags are on.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void FlagKey_BlankPrefix_LoadFeatureFlagsTrue_IsKept(string prefix)
    {
        var options = new AzureAppConfigOptions { LoadFeatureFlags = true, FeatureFlagPrefix = prefix };

        options.ShouldLoadSetting(".appconfig.featureflag/Beta").ShouldBeTrue();
    }

    /// <summary>D7 (R3): options bound from an empty configuration keep every flag.</summary>
    [Fact]
    public void FlagKey_DefaultOptionsFromEmptyConfiguration_IsKept()
    {
        var configuration = new ConfigurationBuilder().Build();
        var options = configuration.GetSection(AzureAppConfigOptions.Name).Get<AzureAppConfigOptions>() ??
                      new AzureAppConfigOptions();

        options.ShouldLoadSetting(".appconfig.featureflag/Beta").ShouldBeTrue();
    }

    /// <summary>D8 (R1): a plain key is kept, whatever the flag options.</summary>
    [Theory]
    [InlineData(true, "")]
    [InlineData(true, "Minimal.")]
    [InlineData(true, "a,b*")]
    [InlineData(false, "")]
    [InlineData(false, "Minimal.")]
    [InlineData(false, "a,b*")]
    public void PlainKey_IsKept(bool loadFeatureFlags, string prefix)
    {
        var options = new AzureAppConfigOptions { LoadFeatureFlags = loadFeatureFlags, FeatureFlagPrefix = prefix };

        options.ShouldLoadSetting("Logging:LogLevel:Default").ShouldBeTrue();
    }

    /// <summary>D9 (R1): a key holding the flag prefix anywhere but at its start is a plain key.</summary>
    [Fact]
    public void KeyContainingFlagPrefixNotAtStart_LoadFeatureFlagsFalse_IsKept()
    {
        var options = new AzureAppConfigOptions { LoadFeatureFlags = false, FeatureFlagPrefix = "" };

        options.ShouldLoadSetting("Settings/.appconfig.featureflag/X").ShouldBeTrue();
    }

    /// <summary>D10 (R4): the raw prefix is compared, not the escaped SDK filter.</summary>
    [Fact]
    public void FlagKey_PrefixWithReservedCharacters_IsKept()
    {
        var options = new AzureAppConfigOptions { LoadFeatureFlags = true, FeatureFlagPrefix = "a,b*" };

        options.ShouldLoadSetting(".appconfig.featureflag/a,b*X").ShouldBeTrue();
    }

    #endregion
}
