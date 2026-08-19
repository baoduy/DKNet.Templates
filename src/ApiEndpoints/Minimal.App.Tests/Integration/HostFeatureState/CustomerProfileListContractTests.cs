using Minimal.App.Tests.Integration.Support;

namespace Minimal.App.Tests.Integration.HostFeatureState;

/// <summary>
/// DRK-500 §3 review round 1, finding 3: pins the customer-profile list endpoint's contract against
/// <c>DKNet.AspCore.Extensions</c> 10.1.2's <c>MapGetList</c> handler, which binds non-nullable
/// <c>int pageNumber, int pageSize</c> with no defaults — a paramless call fails parameter binding before the
/// handler body runs, and a call with both query parameters supplied succeeds and returns a
/// <c>PagedResponse&lt;T&gt;</c>-shaped body.
/// </summary>
public sealed class CustomerProfileListContractTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    #region Methods

    [Fact]
    public async Task ListWithoutQueryStringFailsParameterBinding()
    {
        var response = await fixture.CreateClient().GetAsync("/v1/customer-profiles");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest,
            "pageNumber/pageSize are bound as non-nullable int with no defaults, so a paramless call must fail binding.");
    }

    [Fact]
    public async Task ListWithPageNumberAndPageSizeReturnsPagedResponseShape()
    {
        var response = await fixture.CreateClient().GetAsync("/v1/customer-profiles?pageNumber=1&pageSize=10");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = body.RootElement;

        root.TryGetProperty("items", out _).ShouldBeTrue("body must carry the page's Items");
        root.TryGetProperty("pageNumber", out _).ShouldBeTrue("body must carry PageNumber");
        root.TryGetProperty("pageSize", out _).ShouldBeTrue("body must carry PageSize");
        root.TryGetProperty("pageCount", out _).ShouldBeTrue("body must carry PageCount");
        root.TryGetProperty("totalItemCount", out _).ShouldBeTrue("body must carry TotalItemCount");
    }

    #endregion
}
