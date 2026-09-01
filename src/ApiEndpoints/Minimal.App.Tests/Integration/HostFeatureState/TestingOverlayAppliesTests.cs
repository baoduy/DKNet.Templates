using Microsoft.Extensions.Options;
using Minimal.App.TestSupport;
using Minimal.Share.Options;

namespace Minimal.App.Tests.Integration.HostFeatureState;

/// <summary>
/// DRK-901 (SEC-007) cycle-specific requirement: prove <c>appsettings.Testing.json</c> actually overrides the
/// base file's <c>RequireAuthorization</c>/<c>EnableHttps</c>/<c>EnableRateLimit</c> flags for every host both
/// suites boot under, rather than assuming it does. <c>Program.cs</c> binds <see cref="FeatureOptions"/> from
/// <c>builder.Configuration</c> in its first lines — before <see cref="TestApiFactoryBase"/>'s
/// <c>ConfigureAppConfiguration</c> override is merged in (see the remarks on
/// <see cref="Minimal.App.Tests.Integration.Support.AuthOnApiFixture"/>) — but that early-bound local is only
/// used to conditionally register services; the DI-registered <see cref="IOptions{TOptions}"/> is bound lazily
/// against the final <see cref="IConfiguration"/>, so resolving it after the host is built is what actually
/// observes whichever source (base file, Testing overlay, or an environment variable) won.
/// </summary>
public sealed class TestingOverlayAppliesTests
{
    private const string RequireAuthorizationEnvVar = "FeatureManagement__RequireAuthorization";

    #region Methods

    [Fact]
    public async Task PlainHost_TestingOverlay_OverridesBaseFileSecurityFlags()
    {
        await using var host = new PlainFactory($"overlay-{Guid.NewGuid():N}");
        _ = host.CreateClient();

        var features = host.Services.GetRequiredService<IOptions<FeatureOptions>>().Value;

        features.RequireAuthorization.ShouldBeFalse(
            "appsettings.Testing.json sets RequireAuthorization=false; the base file's true must not survive " +
            "into the Testing-environment host both suites boot under.");
        features.EnableHttps.ShouldBeFalse(
            "appsettings.Testing.json sets EnableHttps=false — an HTTPS redirect would break every plain-HTTP " +
            "TestServer request if the base file's true leaked through.");
        features.EnableRateLimit.ShouldBeFalse(
            "appsettings.Testing.json sets EnableRateLimit=false — a live limiter inherited from the base " +
            "file could reject legitimate test traffic under load.");
    }

    /// <summary>
    /// Same regression <see cref="Integration.HostFeatureState.PerHostFeatureStateTests"/> proves via HTTP
    /// status; this pins the same precedence directly on the bound option so the assertion does not depend on
    /// an unrelated part of the pipeline (auth middleware) also being correct.
    /// </summary>
    [Fact]
    public async Task EnvironmentVariable_OverridesTestingOverlay_ForRequireAuthorization()
    {
        var previous = Environment.GetEnvironmentVariable(RequireAuthorizationEnvVar);
        Environment.SetEnvironmentVariable(RequireAuthorizationEnvVar, "true");
        try
        {
            await using var host = new PlainFactory($"overlay-env-{Guid.NewGuid():N}");
            _ = host.CreateClient();

            var features = host.Services.GetRequiredService<IOptions<FeatureOptions>>().Value;

            features.RequireAuthorization.ShouldBeTrue(
                "an environment variable must outrank both the Testing overlay (false) and the base file, " +
                "matching the default configuration provider precedence AuthOnApiFixture relies on.");
        }
        finally
        {
            Environment.SetEnvironmentVariable(RequireAuthorizationEnvVar, previous);
        }
    }

    private sealed class PlainFactory(string dbName) : TestApiFactoryBase(dbName);

    #endregion
}
