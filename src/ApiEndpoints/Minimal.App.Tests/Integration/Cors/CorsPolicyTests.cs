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

    /// <summary>
    /// DRK-1028 §5: the CORS policy enumerates methods and headers from configuration (default methods
    /// GET/POST/PUT/PATCH — DELETE excluded; default headers Authorization/Content-Type/Accept/X-Idempotency-Key
    /// — no tracing header enumerated). A preflight for an enumerated method/header is granted; one outside the
    /// list is not.
    /// </summary>
    public sealed class WhenAskingPreflightPermission(CorsAllowlistApiFixture fixture) : IClassFixture<CorsAllowlistApiFixture>
    {
        private const string AllowMethodsHeader = "Access-Control-Allow-Methods";
        private const string AllowHeadersHeader = "Access-Control-Allow-Headers";

        private static HttpRequestMessage Preflight(string method, string? requestHeaders = null)
        {
            var request = new HttpRequestMessage(HttpMethod.Options, "/v1/purchase-orders");
            request.Headers.Add("Origin", CorsAllowlistApiFixture.AllowedOrigin);
            request.Headers.Add("Access-Control-Request-Method", method);
            if (requestHeaders is not null)
            {
                request.Headers.Add("Access-Control-Request-Headers", requestHeaders);
            }

            return request;
        }

        [Fact]
        public async Task EnumeratedMethodAndHeader_PermissionIsGranted()
        {
            var client = fixture.CreateClient();

            var response = await client.SendAsync(Preflight("POST", "Authorization"));

            response.Headers.GetValues(AllowMethodsHeader).ShouldContain(v => v.Contains("POST"));
            response.Headers.GetValues(AllowHeadersHeader).ShouldContain(v => v.Contains("Authorization", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task MethodNotEnumerated_PermissionIsNotGranted()
        {
            var client = fixture.CreateClient();

            var response = await client.SendAsync(Preflight("DELETE"));

            var methods = response.Headers.TryGetValues(AllowMethodsHeader, out var values) ? values : [];
            methods.ShouldNotContain(v => v.Contains("DELETE"));
        }

        [Fact]
        public async Task TracingHeaderNotEnumerated_PermissionIsNotGranted()
        {
            var client = fixture.CreateClient();

            var response = await client.SendAsync(Preflight("POST", "traceparent"));

            var headers = response.Headers.TryGetValues(AllowHeadersHeader, out var values) ? values : [];
            headers.ShouldNotContain(v => v.Contains("traceparent", StringComparison.OrdinalIgnoreCase));
        }
    }
}
