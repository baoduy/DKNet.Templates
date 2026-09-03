using Microsoft.AspNetCore.Hosting;
using Minimal.App.TestSupport;

namespace Minimal.App.Tests.Integration.Support;

/// <summary>
/// <see cref="ApiFixture" /> variant with rate limiting on and NO trusted proxy configured — the secure default
/// (<c>Security:TrustedProxies</c> empty). Used to prove a forwarded claim from an untrusted peer is ignored
/// (R1): the budget spent must be the immediate peer's, never the claimed one.
/// </summary>
public sealed class UntrustedForwardedRateLimitApiFixture : TestApiFactoryBase, IAsyncLifetime
{
    private static readonly Dictionary<string, string?> EnvOverrides = new()
    {
        ["FeatureManagement__EnableRateLimit"] = "true",
        ["RateLimit__DefaultRequestLimit"] = "1",
        ["RateLimit__DefaultConcurrentLimit"] = "20",
        ["RateLimit__TimeWindowInSeconds"] = "30"
    };

    public UntrustedForwardedRateLimitApiFixture()
    {
        foreach (var (key, value) in EnvOverrides)
        {
            Environment.SetEnvironmentVariable(key, value);
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services => services.AddSingleton<IStartupFilter, RemoteIpTestStartupFilter>());
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
