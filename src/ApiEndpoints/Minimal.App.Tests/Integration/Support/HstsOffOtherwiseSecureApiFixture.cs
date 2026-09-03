using Minimal.App.TestSupport;

namespace Minimal.App.Tests.Integration.Support;

/// <summary>
/// Represents "one hardening item relaxed via configuration alone, every other item still at its secure
/// default" (R3): transport-security enforcement is explicitly off, while security headers, forwarded-header
/// handling and request bounds are explicitly forced back on (the Testing environment overlay otherwise turns
/// all of them off for the rest of this suite's convenience — see <c>appsettings.Testing.json</c>). Same
/// early-bind env-var constraint as <see cref="AuthOnApiFixture" />.
/// </summary>
public sealed class HstsOffOtherwiseSecureApiFixture : TestApiFactoryBase, IAsyncLifetime
{
    private static readonly Dictionary<string, string?> EnvOverrides = new()
    {
        ["FeatureManagement__EnableHttps"] = "false",
        ["FeatureManagement__EnableSecurityHeaders"] = "true",
        ["FeatureManagement__EnableForwardedHeaders"] = "true",
        ["FeatureManagement__EnableRequestBounds"] = "true"
    };

    public HstsOffOtherwiseSecureApiFixture()
    {
        foreach (var (key, value) in EnvOverrides)
        {
            Environment.SetEnvironmentVariable(key, value);
        }
    }

    public async Task InitializeAsync()
    {
        _ = CreateClient();
        await ResetDatabaseAsync();
    }

    Task IAsyncLifetime.DisposeAsync() => Task.CompletedTask;

    protected override void Dispose(bool disposing)
    {
        foreach (var key in EnvOverrides.Keys)
        {
            Environment.SetEnvironmentVariable(key, null);
        }

        base.Dispose(disposing);
    }
}
