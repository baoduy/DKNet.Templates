using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Minimal.Api.Configs;
using Minimal.Share.Options;

namespace DKNet.Templates.ScaffoldTests;

/// <summary>
/// DRK-1570 (SEC-007): <c>ForwardedHeadersConfig.AddForwardedHeadersConfig</c> must accept a trusted CIDR
/// range via <c>Security:TrustedNetworks</c> in addition to the existing single-IP
/// <c>Security:TrustedProxies</c>, so a deployment behind a proxy with a dynamic address (K8s ingress,
/// Container Apps) can still be expressed instead of collapsing every anonymous caller into one
/// rate-limit partition. Drives the resolved <see cref="ForwardedHeadersOptions" /> — never source text —
/// through the internal <c>Minimal.Api</c> extension (see its <c>InternalsVisibleTo</c>).
/// </summary>
public class ForwardedHeadersTrustTests
{
    #region Methods

    private static ServiceProvider BuildProvider(
        bool enableForwardedHeaders,
        string[] trustedProxies,
        string[] trustedNetworks)
    {
        var data = new Dictionary<string, string?>();
        for (var i = 0; i < trustedProxies.Length; i++)
        {
            data[$"Security:TrustedProxies:{i}"] = trustedProxies[i];
        }

        for (var i = 0; i < trustedNetworks.Length; i++)
        {
            data[$"Security:TrustedNetworks:{i}"] = trustedNetworks[i];
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(data).Build();
        var features = new FeatureOptions { EnableForwardedHeaders = enableForwardedHeaders };

        var services = new ServiceCollection();
        services.AddForwardedHeadersConfig(features, configuration);
        return services.BuildServiceProvider();
    }

    private static ForwardedHeadersOptions ResolveOptions(ServiceProvider provider) =>
        provider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

    /// <summary>S1 (@characterization, R3): a CIDR in TrustedProxies still fails fast, unchanged.</summary>
    [Fact]
    public void TrustedProxies_CidrEntry_ThrowsFormatException()
    {
        Should.Throw<FormatException>(() => BuildProvider(true, ["10.244.0.0/16"], []));
    }

    /// <summary>S2 (@new, R2): TrustedNetworks alone enables headers and populates only KnownIPNetworks.</summary>
    [Fact]
    public void TrustedNetworks_Only_EnablesHeaders_PopulatesKnownIPNetworks_LeavesKnownProxiesEmpty()
    {
        using var provider = BuildProvider(true, [], ["10.244.0.0/16"]);
        var options = ResolveOptions(provider);

        options.ForwardedHeaders.ShouldBe(ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);
        options.KnownIPNetworks.ShouldBe([System.Net.IPNetwork.Parse("10.244.0.0/16")]);
        options.KnownProxies.ShouldBeEmpty();
    }

    /// <summary>S3 (@characterization, R1): both keys empty still disables forwarded headers outright.</summary>
    [Fact]
    public void BothKeysEmpty_DisablesForwardedHeaders_LeavesBothCollectionsEmpty()
    {
        using var provider = BuildProvider(true, [], []);
        var options = ResolveOptions(provider);

        options.ForwardedHeaders.ShouldBe(ForwardedHeaders.None);
        options.KnownProxies.ShouldBeEmpty();
        options.KnownIPNetworks.ShouldBeEmpty();
    }

    /// <summary>S4 (@new, R2): both keys populated enables headers and populates both collections.</summary>
    [Fact]
    public void BothKeysPopulated_EnablesHeaders_PopulatesBothCollections()
    {
        using var provider = BuildProvider(true, ["10.0.0.4"], ["10.244.0.0/16"]);
        var options = ResolveOptions(provider);

        options.ForwardedHeaders.ShouldBe(ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);
        options.KnownProxies.ShouldBe([IPAddress.Parse("10.0.0.4")]);
        options.KnownIPNetworks.ShouldBe([System.Net.IPNetwork.Parse("10.244.0.0/16")]);
    }

    /// <summary>S5 (@characterization, R5): the feature flag off skips registration entirely.</summary>
    [Fact]
    public void FeatureFlagOff_ConfigNotRegistered()
    {
        using var provider = BuildProvider(false, ["10.0.0.4"], ["10.244.0.0/16"]);

        provider.IsConfigAdded(nameof(ForwardedHeadersConfig)).ShouldBeFalse();
    }

    /// <summary>S7 (@new, R4): a malformed CIDR in TrustedNetworks fails fast, same as R3 for TrustedProxies.</summary>
    [Fact]
    public void TrustedNetworks_MalformedCidr_ThrowsFormatException()
    {
        Should.Throw<FormatException>(() => BuildProvider(true, [], ["not-a-cidr"]));
    }

    #endregion
}
