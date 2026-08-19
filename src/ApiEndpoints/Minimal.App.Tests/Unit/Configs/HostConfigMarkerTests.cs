using Minimal.Api.Configs;

namespace Minimal.App.Tests.Unit.Configs;

/// <summary>
/// <see cref="HostConfigMarker"/> replaced the old process-wide "already configured" statics with a keyed
/// singleton scoped to each host's own <see cref="IServiceProvider"/> — see
/// Minimal.App.Tests.Integration.HostFeatureState.PerHostFeatureStateTests for the end-to-end regression
/// coverage of that per-host isolation.
/// </summary>
public class HostConfigMarkerTests
{
    #region Methods

    [Fact]
    public void IsConfigAdded_ShouldBeTrue_AfterMarkConfigAdded()
    {
        var services = new ServiceCollection();
        services.MarkConfigAdded("SampleConfig");
        var provider = services.BuildServiceProvider();

        provider.IsConfigAdded("SampleConfig").ShouldBeTrue();
    }

    [Fact]
    public void IsConfigAdded_ShouldBeFalse_WhenNeverMarked()
    {
        var provider = new ServiceCollection().BuildServiceProvider();

        provider.IsConfigAdded("NeverAddedConfig").ShouldBeFalse();
    }

    [Fact]
    public void IsConfigAdded_ShouldNotLeakBetweenDifferentServiceProviders()
    {
        // Regression guard, at the unit level, for the defect the keyed singleton exists to fix: marking a
        // config on one host's IServiceCollection must not be visible from a different host's IServiceProvider.
        var marked = new ServiceCollection();
        marked.MarkConfigAdded("AuthConfig");
        var markedProvider = marked.BuildServiceProvider();

        var unmarkedProvider = new ServiceCollection().BuildServiceProvider();

        markedProvider.IsConfigAdded("AuthConfig").ShouldBeTrue();
        unmarkedProvider.IsConfigAdded("AuthConfig").ShouldBeFalse();
    }

    #endregion
}
