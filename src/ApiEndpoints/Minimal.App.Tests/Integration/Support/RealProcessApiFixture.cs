using System.Diagnostics;
using System.Net.Sockets;

namespace Minimal.App.Tests.Integration.Support;

/// <summary>
/// Request-lifetime, body-size and header-read bounds are Kestrel connection limits
/// (<c>KestrelServerOptions.Limits</c>) that <c>WebApplicationFactory</c>'s in-memory <c>TestServer</c> never
/// enforces — and <c>WebApplicationFactory&lt;TEntryPoint&gt;.Services</c>/<c>Server</c> hard-cast the built
/// <c>IServer</c> to <c>TestServer</c> internally, so overriding <c>CreateHost</c> to boot real Kestrel isn't
/// viable through that base class either (confirmed: throws <c>InvalidCastException</c>). This runs the actual
/// <c>Minimal.Api.dll</c> — already copied into this test project's own output directory as a
/// <c>ProjectReference</c> — as a real child process listening on a real loopback port, so the bound scenarios
/// exercise genuine Kestrel enforcement.
/// </summary>
public abstract class RealProcessApiFixtureBase : IAsyncLifetime
{
    private Process? _process;

    public HttpClient RealClient { get; private set; } = null!;

    public string Host { get; } = "127.0.0.1";

    public int Port { get; private set; }

    protected abstract string EnvironmentName { get; }

    protected virtual IReadOnlyDictionary<string, string?> EnvironmentOverrides { get; } =
        new Dictionary<string, string?>();

    public async Task InitializeAsync()
    {
        Port = GetFreeTcpPort();
        var dllPath = Path.Combine(AppContext.BaseDirectory, "Minimal.Api.dll");
        File.Exists(dllPath).ShouldBeTrue($"expected {dllPath} to exist as a build output of the ProjectReference.");

        var startInfo = new ProcessStartInfo("dotnet", $"\"{dllPath}\"")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = AppContext.BaseDirectory
        };
        startInfo.Environment["ASPNETCORE_URLS"] = $"http://{Host}:{Port}";
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = EnvironmentName;
        foreach (var (key, value) in EnvironmentOverrides)
        {
            startInfo.Environment[key] = value;
        }

        _process = Process.Start(startInfo) ?? throw new InvalidOperationException("failed to start Minimal.Api process.");

        RealClient = new HttpClient { BaseAddress = new Uri($"http://{Host}:{Port}"), Timeout = TimeSpan.FromSeconds(30) };
        await WaitUntilReadyAsync();
    }

    private async Task WaitUntilReadyAsync()
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                using var response = await RealClient.GetAsync("/healthz");
                return;
            }
            catch (HttpRequestException)
            {
                await Task.Delay(200);
            }
        }

        throw new TimeoutException("Minimal.Api process did not become ready within 30s.");
    }

    private static int GetFreeTcpPort()
    {
        using var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public Task DisposeAsync()
    {
        RealClient?.Dispose();
        if (_process is { HasExited: false })
        {
            _process.Kill(entireProcessTree: true);
            _process.WaitForExit(5000);
        }

        _process?.Dispose();
        return Task.CompletedTask;
    }
}

/// <summary>Real Kestrel process with the request-bound feature forced on and tightened for fast tests.</summary>
public sealed class RealProcessApiFixture : RealProcessApiFixtureBase
{
    protected override string EnvironmentName => "Testing";

    protected override IReadOnlyDictionary<string, string?> EnvironmentOverrides { get; } = new Dictionary<string, string?>
    {
        ["FeatureManagement__RunDbMigrationWhenAppStart"] = "false",
        ["FeatureManagement__EnableSwagger"] = "false",
        ["FeatureManagement__EnableAzureAppConfig"] = "false",
        ["FeatureManagement__EnableRequestBounds"] = "true",
        ["RequestBounds__RequestTimeoutSeconds"] = "2",
        ["RequestBounds__MaxRequestBodySizeBytes"] = "1048576",
        ["RequestBounds__RequestHeadersTimeoutSeconds"] = "2"
    };
}

/// <summary>
/// The load-bearing "freshly generated service, no security configuration supplied" scenario: unlike
/// <see cref="RealProcessApiFixture" />, this sets NO <c>FeatureManagement</c>/<c>Security</c>/
/// <c>RequestBounds</c>/<c>Cors</c>/<c>Https</c> override at all — it boots exactly what <c>appsettings.json</c>
/// alone states, the same shape a consumer's Production host boots with no overlay. Only
/// <c>RunDbMigrationWhenAppStart</c> is forced off, purely so the process can start without a real Postgres
/// instance — never a security-relevant value.
/// </summary>
public sealed class FreshServiceApiFixture : RealProcessApiFixtureBase
{
    protected override string EnvironmentName => "Production";

    protected override IReadOnlyDictionary<string, string?> EnvironmentOverrides { get; } = new Dictionary<string, string?>
    {
        ["FeatureManagement__RunDbMigrationWhenAppStart"] = "false"
    };
}

/// <summary>
/// Same "no security configuration supplied" premise as <see cref="FreshServiceApiFixture" />, except with
/// <c>RequireAuthorization</c> relaxed so a request can actually reach body-reading code — the FallbackPolicy's
/// 401 otherwise short-circuits the pipeline before Kestrel's body-size check ever fires, since authorization
/// runs ahead of endpoint/body binding. RequireAuthorization is not one of the values under test here (the
/// request-bound scenarios are); every bound this fixture proves is still whatever <c>appsettings.json</c> alone
/// states, with no bound-specific override.
/// </summary>
public sealed class FreshServiceNoAuthApiFixture : RealProcessApiFixtureBase
{
    protected override string EnvironmentName => "Production";

    protected override IReadOnlyDictionary<string, string?> EnvironmentOverrides { get; } = new Dictionary<string, string?>
    {
        ["FeatureManagement__RunDbMigrationWhenAppStart"] = "false",
        ["FeatureManagement__RequireAuthorization"] = "false"
    };
}
