using Microsoft.AspNetCore.Hosting;
using Minimal.App.TestSupport;

namespace Minimal.App.Tests.Integration.Support;

/// <summary>
/// <see cref="ApiFixture" /> variant with rate limiting on, a low request budget so a test can exhaust it in a
/// handful of calls, and a single trusted proxy (<see cref="TrustedProxy" />) so
/// <c>ForwardedHeadersMiddleware</c> honours <c>X-Forwarded-For</c> only when the immediate peer (set via
/// <see cref="RemoteIpTestStartupFilter" />) is that address. Same early-bind env-var constraint as
/// <see cref="AuthOnApiFixture" /> — applies to <c>FeatureManagement</c>, <c>Security:TrustedProxies</c> and
/// <c>RateLimit</c> alike, since all three are read from the same <c>builder.Configuration</c> reference at the
/// same synchronous point in <c>Program.cs</c>, ahead of <see cref="TestApiFactoryBase.ConfigureWebHost" />'s
/// <c>ConfigureAppConfiguration</c> merge.
/// </summary>
public sealed class TrustedProxyRateLimitApiFixture : TestApiFactoryBase, IAsyncLifetime
{
    public const string TrustedProxy = "10.0.0.5";

    private static readonly Dictionary<string, string?> EnvOverrides = new()
    {
        ["FeatureManagement__EnableRateLimit"] = "true",
        ["Security__TrustedProxies__0"] = TrustedProxy,
        ["RateLimit__DefaultRequestLimit"] = "1",
        ["RateLimit__DefaultConcurrentLimit"] = "20",
        ["RateLimit__TimeWindowInSeconds"] = "30"
    };

    public TrustedProxyRateLimitApiFixture()
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
