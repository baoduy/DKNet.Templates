namespace DKNet.Templates.ScaffoldTests;

/// <summary>
/// DRK-2364: the template takes the generic hosting items from DKNet 13.3.0 (DKNet.AspCore.Extensions and
/// DKNet.Fw.Extensions) and keeps no copy of its own — "None of the 4 repos keeps its own copy of an item it now
/// gets from the packages". Mirrors <see cref="PackageAdoptionTests.PackagedPlumbing_ShouldNotHaveATemplateCopy" />.
/// </summary>
public class SharedHostingAdoptionTests
{
    #region Methods

    [Theory]
    [InlineData("Minimal.Api/Configs/HostConfigMarker.cs")]
    [InlineData("Minimal.Api/Configs/RateLimits/RateLimitConfig.cs")]
    [InlineData("Minimal.Api/Configs/RateLimits/RateLimitKeyProvider.cs")]
    [InlineData("Minimal.Api/Configs/RateLimits/RateLimitOptions.cs")]
    [InlineData("Minimal.Api/Configs/RateLimits/RateLimitOptionsProvider.cs")]
    [InlineData("Minimal.Api/Configs/Antiforgery/AntiforgeryConfig.cs")]
    [InlineData("Minimal.Api/Configs/Antiforgery/AntiforgeryCookieMiddleware.cs")]
    [InlineData("Minimal.Api/Configs/RequestBoundsConfig.cs")]
    [InlineData("Minimal.Api/Configs/HttpsConfig.cs")]
    [InlineData("Minimal.Api/Configs/ForwardedHeadersConfig.cs")]
    [InlineData("Minimal.Api/Configs/CrosConfig.cs")]
    [InlineData("Minimal.Api/Configs/Handlers/HttpContextSensitiveDataPrincipalAccessor.cs")]
    [InlineData("Minimal.Api/Configs/Jobs/JobSelector.cs")]
    [InlineData("Minimal.Share/Extensions/SanitizeForLogging.cs")]
    public void SharedHostingItem_ShouldNotHaveATemplateCopy(string relativePath)
    {
        var srcDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src"));
        var apiEndpointsDir = Path.Combine(srcDir, "ApiEndpoints");

        // A mistyped path would pass silently: anchor the guard to the folder the copies live in.
        Directory.Exists(Path.Combine(apiEndpointsDir, "Minimal.Api", "Configs")).ShouldBeTrue();

        File.Exists(Path.Combine(apiEndpointsDir, relativePath)).ShouldBeFalse(
            $"{relativePath} copies a hosting item DKNet 13.3.0 now provides — it should have been removed.");
    }

    #endregion
}
