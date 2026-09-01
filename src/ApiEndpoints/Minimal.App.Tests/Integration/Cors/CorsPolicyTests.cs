using Minimal.App.Tests.Integration.Support;

namespace Minimal.App.Tests.Integration.Cors;

/// <summary>
/// DRK-905/SEC-007: the default CORS policy is deny-by-default, driven entirely by
/// <c>Cors:AllowedOrigins</c>. These assertions must fail against pre-fix code
/// (<c>AllowAnyOrigin()</c> registered unconditionally) — an absent/empty allowlist must never
/// reflect a cross-origin request, a configured allowlist must accept only its own entries, and
/// credentials must never be allowed on any path.
/// </summary>
public sealed class CorsPolicyTests
{
    private const string AllowOriginHeader = "Access-Control-Allow-Origin";
    private const string AllowCredentialsHeader = "Access-Control-Allow-Credentials";
    private const string UnlistedOrigin = "https://evil.example";

    public sealed class WhenAllowedOriginsIsAbsentOrEmpty(ApiFixture fixture) : IClassFixture<ApiFixture>
    {
        [Fact]
        public async Task Get_ShouldNotEmitAllowOriginHeader_ForAnyCrossOriginRequest()
        {
            var client = fixture.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, "/healthz");
            request.Headers.Add("Origin", UnlistedOrigin);

            var response = await client.SendAsync(request);

            response.Headers.Contains(AllowOriginHeader).ShouldBeFalse(
                "an empty/absent Cors:AllowedOrigins must never wire CORS middleware, so no origin is ever reflected back.");
        }

        [Fact]
        public async Task Get_ShouldNotEmitAllowCredentialsHeader_ForAnyCrossOriginRequest()
        {
            var client = fixture.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, "/healthz");
            request.Headers.Add("Origin", UnlistedOrigin);

            var response = await client.SendAsync(request);

            response.Headers.Contains(AllowCredentialsHeader).ShouldBeFalse();
        }
    }

    public sealed class WhenAllowedOriginsIsConfigured(CorsAllowlistApiFixture fixture)
        : IClassFixture<CorsAllowlistApiFixture>
    {
        [Fact]
        public async Task Get_ShouldEmitAllowOriginHeader_ForConfiguredOrigin()
        {
            var client = fixture.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, "/healthz");
            request.Headers.Add("Origin", CorsAllowlistApiFixture.AllowedOrigin);

            var response = await client.SendAsync(request);

            response.Headers.GetValues(AllowOriginHeader).ShouldContain(CorsAllowlistApiFixture.AllowedOrigin);
        }

        [Fact]
        public async Task Get_ShouldNotEmitAllowOriginHeader_ForUnlistedOrigin()
        {
            var client = fixture.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, "/healthz");
            request.Headers.Add("Origin", UnlistedOrigin);

            var response = await client.SendAsync(request);

            response.Headers.Contains(AllowOriginHeader).ShouldBeFalse(
                "an origin outside Cors:AllowedOrigins must never be reflected back, even once CORS is wired for other origins.");
        }

        [Fact]
        public async Task Get_ShouldNotEmitAllowCredentialsHeader_ForConfiguredOrigin()
        {
            var client = fixture.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, "/healthz");
            request.Headers.Add("Origin", CorsAllowlistApiFixture.AllowedOrigin);

            var response = await client.SendAsync(request);

            response.Headers.Contains(AllowCredentialsHeader).ShouldBeFalse();
        }
    }
}
