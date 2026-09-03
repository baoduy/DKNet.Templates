using Minimal.App.TestSupport;

namespace Minimal.App.Tests.Integration.Support;

/// <summary>
/// <see cref="ApiFixture" /> variant with <c>EnableHttps</c> on and <c>Https:HstsMaxAgeDays</c> set to
/// <paramref name="hstsMaxAgeDays" />, to exercise the preload boundary (365 days qualifies, anything less does
/// not). Same early-bind env-var constraint as <see cref="AuthOnApiFixture" />.
/// </summary>
public abstract class HstsApiFixture(int hstsMaxAgeDays) : TestApiFactoryBase, IAsyncLifetime
{
    private const string EnableHttpsEnvKey = "FeatureManagement__EnableHttps";
    private const string HstsMaxAgeDaysEnvKey = "Https__HstsMaxAgeDays";

    private readonly Action _restoreEnv = SetEnv(hstsMaxAgeDays);

    private static Action SetEnv(int hstsMaxAgeDays)
    {
        Environment.SetEnvironmentVariable(EnableHttpsEnvKey, "true");
        Environment.SetEnvironmentVariable(HstsMaxAgeDaysEnvKey, hstsMaxAgeDays.ToString());
        return () =>
        {
            Environment.SetEnvironmentVariable(EnableHttpsEnvKey, null);
            Environment.SetEnvironmentVariable(HstsMaxAgeDaysEnvKey, null);
        };
    }

    public async Task InitializeAsync()
    {
        _ = CreateClient();
        await ResetDatabaseAsync();
    }

    Task IAsyncLifetime.DisposeAsync() => Task.CompletedTask;

    protected override void Dispose(bool disposing)
    {
        _restoreEnv();
        base.Dispose(disposing);
    }
}

public sealed class QualifyingHstsApiFixture() : HstsApiFixture(365);

public sealed class NonQualifyingHstsApiFixture() : HstsApiFixture(30);
