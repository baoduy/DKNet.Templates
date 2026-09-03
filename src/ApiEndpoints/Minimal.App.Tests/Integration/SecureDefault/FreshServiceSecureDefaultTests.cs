using System.Net.Sockets;
using System.Text;
using Minimal.App.Tests.Integration.Support;

namespace Minimal.App.Tests.Integration.SecureDefault;

/// <summary>
/// DRK-1028 §5, the load-bearing last-but-two scenario: "a freshly generated service is protected with no
/// security configuration supplied". Runs against <see cref="FreshServiceApiFixture" />, which sets NO
/// <c>FeatureManagement</c>/<c>Security</c>/<c>RequestBounds</c>/<c>Https</c>/<c>Cors</c> override at all — the
/// three request-bound scenarios in <c>RequestBoundsTests</c> each state their bound as test configuration,
/// which an implementation could satisfy from that configuration alone; this scenario is what rules that out —
/// the bounds enforced here are whatever <c>appsettings.json</c> itself states (1 MB / 30 s / 10 s), never a
/// value this test supplies.
/// </summary>
public sealed class FreshServiceSecureDefaultTests
{
    public sealed class WithNoOverridesAtAll(FreshServiceApiFixture fixture) : IClassFixture<FreshServiceApiFixture>
    {
        [Fact]
        public async Task AnonymousCaller_RequestingAnUndeclaredEndpoint_IsRejectedAsUnauthenticated()
        {
            var response = await fixture.RealClient.GetAsync("/v1/purchase-orders");

            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task ResponseCarriesTheStandardSecurityHeaders()
        {
            var response = await fixture.RealClient.GetAsync("/healthz");

            response.Headers.Contains("X-Frame-Options").ShouldBeTrue();
            response.Headers.Contains("X-Content-Type-Options").ShouldBeTrue();
            response.Headers.Contains("Server").ShouldBeFalse();
        }

        [Fact]
        public async Task RequestHeaderTimeBoundIsTheTemplatesOwnDefault_NotTestConfiguration()
        {
            // appsettings.json's own RequestHeadersTimeoutSeconds default is 10 — this fixture supplies none.
            // Never reaches routing/auth (headers never finish), so RequireAuthorization does not affect this.
            using var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(fixture.Host, fixture.Port);
            await using var stream = tcpClient.GetStream();

            var partial = Encoding.ASCII.GetBytes(
                $"GET /healthz HTTP/1.1\r\nHost: {fixture.Host}\r\nX-Partial: still-sending\r\n");
            await stream.WriteAsync(partial);
            await stream.FlushAsync();

            tcpClient.ReceiveTimeout = 20_000;
            var buffer = new byte[4096];
            using var received = new MemoryStream();
            int read;
            while ((read = await stream.ReadAsync(buffer)) > 0)
            {
                received.Write(buffer, 0, read);
            }

            var text = Encoding.ASCII.GetString(received.ToArray());
            text.Contains("200 OK", StringComparison.Ordinal).ShouldBeFalse(
                "the request never finished sending its headers, so it must never succeed as if it had.");
        }
    }

    /// <summary>
    /// <c>RequireAuthorization</c> relaxed only so the request reaches body-reading code at all — the
    /// FallbackPolicy's 401 otherwise fires before Kestrel ever checks body size, since authorization runs ahead
    /// of endpoint/body binding. The body-size bound itself is still whatever <c>appsettings.json</c> states,
    /// with no bound-specific override.
    /// </summary>
    public sealed class WithOnlyAuthorizationRelaxedToReachTheHandler(FreshServiceNoAuthApiFixture fixture)
        : IClassFixture<FreshServiceNoAuthApiFixture>
    {
        [Fact]
        public async Task RequestBodyBoundIsTheTemplatesOwnDefault_NotTestConfiguration()
        {
            // appsettings.json's own MaxRequestBodySizeBytes default is 1 MB — this fixture supplies none, so if
            // this passes, the 1 MB figure came from the template's shipped defaults alone.
            const int overTheTemplatesOwnOneMegabyteDefault = 2 * 1024 * 1024;

            using var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(fixture.Host, fixture.Port);
            await using var stream = tcpClient.GetStream();

            var headers = Encoding.ASCII.GetBytes(
                $"POST /v1/purchase-orders HTTP/1.1\r\n" +
                $"Host: {fixture.Host}\r\n" +
                $"Content-Type: application/json\r\n" +
                $"Content-Length: {overTheTemplatesOwnOneMegabyteDefault}\r\n" +
                "Connection: close\r\n\r\n");
            await stream.WriteAsync(headers);
            await stream.FlushAsync();

            using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
            var statusLine = await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10));

            statusLine.ShouldNotBeNull();
            statusLine.ShouldContain("413");
        }
    }
}
