using Minimal.App.Tests.Integration.Support;

namespace Minimal.App.Tests.Integration.Auth;

/// <summary>
/// DRK-1028 §5: an endpoint published without being declared anonymous still requires authentication — the
/// <c>FallbackPolicy</c> default-deny. <c>/v1/purchase-orders</c> never calls <c>.AllowAnonymous()</c>, so it
/// stands in for "any endpoint outside a configured anonymous group".
/// </summary>
public sealed class DefaultDenyTests(RequireAuthNoHandlerApiFixture fixture) : IClassFixture<RequireAuthNoHandlerApiFixture>
{
    [Fact]
    public async Task AnonymousCaller_IsRejectedAsUnauthenticated()
    {
        var response = await fixture.CreateClient().GetAsync("/v1/purchase-orders");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeclaredAnonymousHealthProbe_StaysReachable()
    {
        var response = await fixture.CreateClient().GetAsync("/healthz");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
