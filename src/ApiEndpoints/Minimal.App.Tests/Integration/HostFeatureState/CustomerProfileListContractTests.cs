using Minimal.App.Tests.Integration.Support;

namespace Minimal.App.Tests.Integration.HostFeatureState;

/// <summary>
/// DRK-521 re-pin (reverses the DRK-500/DRK-513 review-round-1 finding 3 decision): as of
/// <c>DKNet.AspCore.Extensions</c> 10.1.3, <c>MapGetList</c>'s handler binds <c>pageNumber</c>/<c>pageSize</c> as
/// optional <c>int</c> parameters defaulting to 1 and 20, so a paramless call now succeeds and returns the first
/// page (page size 20) rather than failing parameter binding. A call with both query parameters supplied still
/// succeeds and returns a <c>PagedResponse&lt;T&gt;</c>-shaped body.
/// </summary>
public sealed class CustomerProfileListContractTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    #region Methods

    [Fact]
    public async Task ListWithoutQueryStringReturnsFirstPageOfTwenty()
    {
        var response = await fixture.CreateClient().GetAsync("/v1/customer-profiles");

        response.StatusCode.ShouldBe(HttpStatusCode.OK,
            "pageNumber/pageSize now default to 1/20, so a paramless call must bind successfully.");

        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = body.RootElement;

        root.GetProperty("pageNumber").GetInt32().ShouldBe(1);
        root.GetProperty("pageSize").GetInt32().ShouldBe(20);
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
