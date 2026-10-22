using System.Diagnostics;
using System.Net;
using System.Text;
using Minimal.App.TestSupport;

namespace DKNet.Templates.ScaffoldTests;

/// <summary>
/// DRK-2364 §5, the Outline rows naming "DKNet.Templates API": the template answers as before once its hosting
/// copies are replaced by DKNet 13.3.0. Black-box only — the host is driven over HTTP (TestServer, or real
/// Kestrel where a Kestrel limit is the behaviour) and as a child process, never through a template or package
/// type, so these tests compile and stay green on both sides of the swap.
/// </summary>
/// <remarks>
/// Settings go in as environment variables: <c>Program.cs</c> binds <c>FeatureOptions</c> in its first lines,
/// and the hosting items read their sections while services are added — both before an
/// <c>AddInMemoryCollection</c> override is merged (the constraint <c>AuthOnApiFixture</c> documents). Every host
/// here lives in <see cref="SharedHostingCollection" />, which never runs in parallel with anything else.
/// </remarks>
[Collection(SharedHostingCollection.Name)]
public class SharedHostingBehaviourTests
{
    #region Fields

    private const string EnableRateLimit = "FeatureManagement__EnableRateLimit";
    private const string EnableHttps = "FeatureManagement__EnableHttps";
    private const string EnableForwardedHeaders = "FeatureManagement__EnableForwardedHeaders";
    private const string EnableRequestBounds = "FeatureManagement__EnableRequestBounds";

    private const string AppOrigin = "https://app.example.com";

    #endregion

    #region Methods

    /// <summary>"Each service keeps its shipped rate limit" — DKNet.Templates API.</summary>
    /// <remarks>
    /// Only the feature flag the Testing overlay turns off is turned back on; the <c>RateLimit</c> section is the
    /// base <c>appsettings.json</c> one (100 requests per 1 second window). The Testing host signs every request
    /// in as the demonstration identity, and authentication runs after the rate limiter in <c>UseAppConfig</c>,
    /// so no caller name — "treasury-ops" or another — reaches the partition: all 101 requests share one bucket.
    /// </remarks>
    [Fact]
    public async Task ShippedRateLimit_OneCallerSends101RequestsWithinOneSecond_LastRequestIsRefusedWith429()
    {
        using var host = new SharedHostingHost(new() { [EnableRateLimit] = "true" });
        using var client = host.CreateClient();

        var statuses = await SendBurstAsync(client, 101);

        statuses[0].ShouldBe(HttpStatusCode.OK);
        statuses[^1].ShouldBe(HttpStatusCode.TooManyRequests);
    }

    /// <summary>"An item switched off by its feature flag changes nothing" — DKNet.Templates API, rate limiting.</summary>
    [Fact]
    public async Task RateLimitSwitchedOff_CallerSends101RequestsWithinOneSecond_NoneIsRefusedWith429()
    {
        using var host = new SharedHostingHost(new() { [EnableRateLimit] = "false" });
        using var client = host.CreateClient();

        var statuses = await SendBurstAsync(client, 101);

        statuses.Count.ShouldBe(101);
        statuses.ShouldNotContain(HttpStatusCode.TooManyRequests);
    }

    /// <summary>"Each service keeps the CORS headers its settings allow" — DKNet.Templates API, X-Idempotency-Key.</summary>
    [Fact]
    public async Task CorsWithAppOriginAllowed_PreflightForPostWithXIdempotencyKey_IsAllowed()
    {
        using var host = new SharedHostingHost(new() { ["Cors__AllowedOrigins__0"] = AppOrigin });
        using var client = host.CreateClient();

        using var response = await client.SendAsync(Preflight("POST", "X-Idempotency-Key"));

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        response.Headers.GetValues("Access-Control-Allow-Origin").ShouldBe([AppOrigin]);
        // The allowed list comes back whole, as the shipped Cors:AllowedHeaders setting spells it.
        response.Headers.GetValues("Access-Control-Allow-Headers")
            .ShouldBe(["Authorization,Content-Type,Accept,X-Idempotency-Key"]);
    }

    /// <summary>
    /// Presence sibling of the CORS row (spec §3 Must stay true, brief R1): with the shipped empty origin list
    /// CORS stays off, so the same preflight is not answered with an allowed origin.
    /// </summary>
    [Fact]
    public async Task CorsWithNoOriginConfigured_PreflightForPostWithXIdempotencyKey_IsNotAllowed()
    {
        using var host = new SharedHostingHost([]);
        using var client = host.CreateClient();

        using var response = await client.SendAsync(Preflight("POST", "X-Idempotency-Key"));

        response.Headers.Contains("Access-Control-Allow-Origin").ShouldBeFalse();
        response.Headers.Contains("Access-Control-Allow-Headers").ShouldBeFalse();
    }

    /// <summary>"An item switched off by its feature flag changes nothing" — DKNet.Templates API, HTTPS.</summary>
    /// <remarks>
    /// <c>HTTPS_PORT</c> and a non-loopback host are set so the redirect could happen at all: without them
    /// <c>UseHttpsRedirection</c> finds no port and never redirects, flag on or off. ASP.NET Core sends HSTS only
    /// on HTTPS responses, so on this plain HTTP request the redirect half carries the gate;
    /// <see cref="HttpsSwitchedOn_PlainHttpRequest_IsRedirectedAndHttpsResponseCarriesHsts" /> is its sibling.
    /// </remarks>
    [Fact]
    public async Task HttpsSwitchedOff_PlainHttpRequest_IsNotRedirectedAndGetsNoHstsHeader()
    {
        using var host = new SharedHostingHost(new() { [EnableHttps] = "false", ["HTTPS_PORT"] = "443" });
        using var client = host.CreateClient(new() { BaseAddress = new Uri("http://api.example.com"), AllowAutoRedirect = false });

        using var response = await client.GetAsync("/healthz");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.Location.ShouldBeNull();
        response.Headers.Contains("Strict-Transport-Security").ShouldBeFalse();
    }

    /// <summary>
    /// Presence sibling of the HTTPS flag-off row: the same host with the flag on redirects plain HTTP and sends
    /// the shipped 365-day HSTS header on HTTPS (spec §3 HTTPS; base <c>Https:HstsMaxAgeDays</c> 365).
    /// </summary>
    [Fact]
    public async Task HttpsSwitchedOn_PlainHttpRequest_IsRedirectedAndHttpsResponseCarriesHsts()
    {
        using var host = new SharedHostingHost(new() { [EnableHttps] = "true", ["HTTPS_PORT"] = "443" });
        using var httpClient = host.CreateClient(new() { BaseAddress = new Uri("http://api.example.com"), AllowAutoRedirect = false });
        using var httpsClient = host.CreateClient(new() { BaseAddress = new Uri("https://api.example.com"), AllowAutoRedirect = false });

        using var plain = await httpClient.GetAsync("/healthz");
        using var secure = await httpsClient.GetAsync("/healthz");

        plain.StatusCode.ShouldBe(HttpStatusCode.TemporaryRedirect);
        plain.Headers.Location.ShouldBe(new Uri("https://api.example.com/healthz"));
        secure.Headers.GetValues("Strict-Transport-Security").ShouldBe(["max-age=31536000; includeSubDomains; preload"]);
    }

    /// <summary>"An item switched off by its feature flag changes nothing" — DKNet.Templates API, forwarded headers.</summary>
    [Fact]
    public async Task ForwardedHeadersSwitchedOff_TrustedPeerSaysItForwardsFor198_51_100_7_ServiceSeesThePeer()
    {
        using var host = new SharedHostingHost(new()
        {
            [EnableForwardedHeaders] = "false",
            ["Security__TrustedNetworks__0"] = "10.0.0.0/8"
        });

        var client = await ClientAddressSeenByServiceAsync(host, peer: "10.1.2.3", forwardedFor: "198.51.100.7");

        client.ShouldBe(IPAddress.Parse("10.1.2.3"));
    }

    /// <summary>
    /// Presence sibling of the forwarded-headers flag-off row (brief §6a D1, flag true): the same trusted peer is
    /// rewritten to the forwarded client, so the flag-off row is not satisfied by a seam that never rewrites.
    /// </summary>
    [Fact]
    public async Task ForwardedHeadersSwitchedOn_TrustedPeerSaysItForwardsFor198_51_100_7_ServiceSeesTheForwardedClient()
    {
        using var host = new SharedHostingHost(new()
        {
            [EnableForwardedHeaders] = "true",
            ["Security__TrustedNetworks__0"] = "10.0.0.0/8"
        });

        var client = await ClientAddressSeenByServiceAsync(host, peer: "10.1.2.3", forwardedFor: "198.51.100.7");

        client.ShouldBe(IPAddress.Parse("198.51.100.7"));
    }

    /// <summary>"An item switched off by its feature flag changes nothing" — DKNet.Templates API, request bounds.</summary>
    /// <remarks>Real Kestrel: TestServer does not enforce <c>KestrelServerOptions.Limits.MaxRequestBodySize</c>.</remarks>
    [Fact]
    public async Task RequestBoundsSwitchedOff_CallerPosts2MiBBody_RequestIsNotRefusedForItsSize()
    {
        using var host = new SharedHostingHost(new() { [EnableRequestBounds] = "false" }, useKestrel: true);
        using var client = host.CreateClient();

        using var response = await client.SendAsync(Post2MiBBody());

        response.StatusCode.ShouldNotBe(HttpStatusCode.RequestEntityTooLarge);
    }

    /// <summary>
    /// Presence sibling of the request-bounds flag-off row (brief §6a D2, flag true): with the shipped
    /// <c>RequestBounds</c> section (1 MiB maximum) the same 2 MiB body is refused with 413.
    /// </summary>
    [Fact]
    public async Task RequestBoundsSwitchedOn_CallerPosts2MiBBody_RequestIsRefusedWith413()
    {
        using var host = new SharedHostingHost(new() { [EnableRequestBounds] = "true" }, useKestrel: true);
        using var client = host.CreateClient();

        using var response = await client.SendAsync(Post2MiBBody());

        response.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
    }

    /// <summary>"Antiforgery stays off in each service" — DKNet.Templates API.</summary>
    /// <remarks>
    /// The template's own copy names its cookies <c>x-csrf-*</c>, so the literal <c>XSRF-TOKEN</c> check alone
    /// could not fail today; asserting no cookie at all covers the copy and the package alike.
    /// </remarks>
    [Fact]
    public async Task ShippedSettings_BrowserSendsGetToARoute_ResponseSetsNoXsrfTokenCookie()
    {
        using var host = new SharedHostingHost([]);
        using var client = host.CreateClient();

        using var response = await client.GetAsync("/v1/purchase-orders");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var cookies = response.Headers.TryGetValues("Set-Cookie", out var values) ? values.ToArray() : [];
        cookies.ShouldNotContain(c => c.StartsWith("XSRF-TOKEN=", StringComparison.Ordinal));
        cookies.ShouldBeEmpty();
    }

    /// <summary>"A service that runs jobs still refuses an unknown job" — DKNet.Templates API.</summary>
    /// <remarks>
    /// The unknown-job path returns before <c>builder.Build()</c>, where <c>WebApplicationFactory</c> cannot see
    /// the exit code, so <c>Minimal.Api</c> runs as a child process from its own build output.
    /// </remarks>
    [Fact]
    public async Task ServiceStartsWithArgumentCleanup_ReportsTheUnknownJobCleanupAndExitsWithCode1()
    {
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var apiBinDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "../../../../../src/ApiEndpoints/Minimal.Api/bin", configuration, "net10.0"));
        var apiDll = Path.Combine(apiBinDir, "Minimal.Api.dll");
        File.Exists(apiDll).ShouldBeTrue($"{apiDll} should be built before this test runs.");

        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = apiBinDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add(apiDll);
        startInfo.ArgumentList.Add("cleanup");

        using var process = Process.Start(startInfo)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }

        var errorLines = (await stderr).Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
        await stdout;

        process.ExitCode.ShouldBe(1);
        errorLines.ShouldContain("Unrecognized job \"cleanup\". Known jobs: migration");
    }

    /// <summary>
    /// Sends <paramref name="count" /> sequential GETs after one warm-up request on another host name, and fails
    /// if the burst itself took a second or more — the scenarios say "within 1 second", and a slower burst would
    /// span two fixed windows and prove nothing.
    /// </summary>
    private static async Task<List<HttpStatusCode>> SendBurstAsync(HttpClient client, int count)
    {
        using (var warmUp = new HttpRequestMessage(HttpMethod.Get, "/healthz"))
        {
            warmUp.Headers.Host = "warm-up.example";
            using var _ = await client.SendAsync(warmUp);
        }

        var statuses = new List<HttpStatusCode>(count);
        var stopwatch = Stopwatch.StartNew();
        for (var i = 0; i < count; i++)
        {
            using var response = await client.GetAsync("/healthz");
            statuses.Add(response.StatusCode);
        }

        stopwatch.Stop();
        stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(1));
        return statuses;
    }

    private static HttpRequestMessage Preflight(string method, string header)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/v1/purchase-orders");
        request.Headers.Add("Origin", AppOrigin);
        request.Headers.Add("Access-Control-Request-Method", method);
        request.Headers.Add("Access-Control-Request-Headers", header);
        return request;
    }

    private static HttpRequestMessage Post2MiBBody()
    {
        const int twoMiB = 2 * 1024 * 1024;
        const string prefix = "{\"note\":\"";
        const string suffix = "\"}";
        var json = prefix + new string('a', twoMiB - prefix.Length - suffix.Length) + suffix;

        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/purchase-orders")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Idempotency-Key", Guid.NewGuid().ToString());

        // Kestrel refuses an oversized Content-Length before it asks for the body: without 100-continue the
        // client may still be uploading when the connection closes, and sees a reset instead of the 413.
        request.Headers.ExpectContinue = true;
        return request;
    }

    /// <summary>
    /// Runs one GET through the whole pipeline from <paramref name="peer" /> with an <c>X-Forwarded-For</c> header,
    /// and returns the client address the pipeline left on the connection.
    /// </summary>
    private static async Task<IPAddress?> ClientAddressSeenByServiceAsync(SharedHostingHost host, string peer, string forwardedFor)
    {
        var context = await host.Server.SendAsync(c =>
        {
            c.Request.Method = "GET";
            c.Request.Path = "/healthz";
            c.Connection.RemoteIpAddress = IPAddress.Parse(peer);
            c.Request.Headers["X-Forwarded-For"] = forwardedFor;
        });

        return context.Connection.RemoteIpAddress;
    }

    #endregion
}

/// <summary>Every DRK-2364 host-building test: they set process-wide environment variables, so never in parallel.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SharedHostingCollection
{
    public const string Name = "Shared hosting hosts";
}

/// <summary>
/// The shared <see cref="TestApiFactoryBase" /> host with the given settings applied as environment variables for
/// its lifetime (see <see cref="SharedHostingBehaviourTests" /> remarks), optionally on real Kestrel.
/// </summary>
internal sealed class SharedHostingHost : TestApiFactoryBase
{
    private readonly string[] _settingKeys;

    public SharedHostingHost(Dictionary<string, string> settings, bool useKestrel = false)
    {
        _settingKeys = [.. settings.Keys];
        foreach (var (key, value) in settings)
        {
            Environment.SetEnvironmentVariable(key, value);
        }

        if (useKestrel)
        {
            UseKestrel();
        }
    }

    protected override void Dispose(bool disposing)
    {
        foreach (var key in _settingKeys)
        {
            Environment.SetEnvironmentVariable(key, null);
        }

        base.Dispose(disposing);
    }
}
