using Minimal.App.Tests.Integration.Support;

namespace Minimal.App.Tests.Integration.RateLimits;

/// <summary>
/// DRK-1028 §5 (R1): forwarded caller information is honoured only from a peer in the configured trusted-proxy
/// list. Two clients behind the same trusted ingress hold separate rate-limit budgets; a forwarded claim from
/// an untrusted peer is ignored and the budget spent is the immediate peer's own.
/// </summary>
public sealed class ForwardedCallerRateLimitTests
{
    public sealed class BehindATrustedIngress(TrustedProxyRateLimitApiFixture fixture)
        : IClassFixture<TrustedProxyRateLimitApiFixture>
    {
        [Fact]
        public async Task TwoClientsBehindTheSameTrustedIngress_HoldSeparateBudgets()
        {
            var client = fixture.CreateClient();

            using var first = ForwardedRequest(TrustedProxyRateLimitApiFixture.TrustedProxy, "203.0.113.10");
            var firstResponse = await client.SendAsync(first);
            firstResponse.StatusCode.ShouldBe(HttpStatusCode.OK, "the first request from 203.0.113.10 should spend its own budget.");

            using var exhausted = ForwardedRequest(TrustedProxyRateLimitApiFixture.TrustedProxy, "203.0.113.10");
            var exhaustedResponse = await client.SendAsync(exhausted);
            exhaustedResponse.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests,
                "203.0.113.10 already spent its single request this window.");

            using var otherClient = ForwardedRequest(TrustedProxyRateLimitApiFixture.TrustedProxy, "198.51.100.20");
            var otherResponse = await client.SendAsync(otherClient);
            otherResponse.StatusCode.ShouldBe(HttpStatusCode.OK,
                "198.51.100.20 holds a separate budget from 203.0.113.10, even through the same trusted ingress.");
        }

        private static HttpRequestMessage ForwardedRequest(string peer, string forwardedFor)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/healthz");
            request.Headers.Add(RemoteIpTestStartupFilter.TestRemoteIpHeader, peer);
            request.Headers.Add("X-Forwarded-For", forwardedFor);
            return request;
        }
    }

    public sealed class BehindAnUntrustedPeer(UntrustedForwardedRateLimitApiFixture fixture)
        : IClassFixture<UntrustedForwardedRateLimitApiFixture>
    {
        private const string UntrustedPeer = "203.0.113.10";
        private const string ClaimedIdentity = "198.51.100.20";

        [Fact]
        public async Task ForwardedClaimFromAnUntrustedPeer_SpendsTheImmediatePeersOwnBudget()
        {
            var client = fixture.CreateClient();

            using var spoofed = new HttpRequestMessage(HttpMethod.Get, "/healthz");
            spoofed.Headers.Add(RemoteIpTestStartupFilter.TestRemoteIpHeader, UntrustedPeer);
            spoofed.Headers.Add("X-Forwarded-For", ClaimedIdentity);
            var spoofedResponse = await client.SendAsync(spoofed);
            spoofedResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

            // No trusted proxy configured, so the claimed 198.51.100.20 identity was never honoured — the permit
            // spent above must belong to the real peer, 203.0.113.10. A second request from that same real peer
            // (no forwarded claim at all this time) must find its budget already exhausted.
            using var second = new HttpRequestMessage(HttpMethod.Get, "/healthz");
            second.Headers.Add(RemoteIpTestStartupFilter.TestRemoteIpHeader, UntrustedPeer);
            var secondResponse = await client.SendAsync(second);
            secondResponse.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests,
                "the untrusted peer's own budget (not the claimed identity's) must already be spent.");
        }
    }
}
