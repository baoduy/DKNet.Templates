using Minimal.App.Tests.Integration.Support;

namespace Minimal.App.Tests.Integration.OpenApi;

/// <summary>
/// Re-homes the platform coverage <c>OpenApiDocumentRegressionTests</c> (deleted with the removed demo
/// entities' teardown) onto the manual sample's hand-mapped
/// <c>PurchaseOrderV1Endpoint</c> routes — document title, the versioned-and-resolved path shape, and the
/// <c>{id}</c> path parameter's schema. The automated sample's generated <c>Product</c> routes are not pinned
/// here since their exact shape comes from a source generator, not from committed source.
/// </summary>
public sealed class OpenApiDocumentRegressionTests(SwaggerOnApiFixture fixture) : IClassFixture<SwaggerOnApiFixture>
{
    #region Methods

    [Fact]
    public async Task Document_IsGenerated_WithExpectedTitle()
    {
        using var doc = await FetchDocumentAsync();

        doc.RootElement.GetProperty("info").GetProperty("title").GetString()
            .ShouldBe("Minimal.Api API v1 Version");
    }

    [Fact]
    public async Task Document_PurchaseOrderPaths_UseResolvedV1SegmentWithExpectedMethods()
    {
        using var doc = await FetchDocumentAsync();
        var paths = doc.RootElement.GetProperty("paths");

        paths.GetProperty("/v1/purchase-orders").EnumerateObject().Select(m => m.Name).Order()
            .ShouldBe(["get", "post"]);
        paths.GetProperty("/v1/purchase-orders/{id}").EnumerateObject().Select(m => m.Name).Order()
            .ShouldBe(["delete", "get", "put"]);
        paths.GetProperty("/v1/purchase-orders/{id}/cancel").EnumerateObject().Select(m => m.Name)
            .ShouldBe(["post"]);
    }

    /// <summary>
    /// Cancel and Delete bind their request via <c>[AsParameters]</c> (DRK-738), which puts <c>ByUser</c> in the
    /// operation's own parameter list rather than a body schema — a different exclusion path
    /// (<c>ContextualSourceOperationTransformer</c>) than the one JSON-body-bound routes use
    /// (<c>ContextualSourceSchemaTransformer</c>). Both must still hide the <c>[FromClaim]</c>-declared member:
    /// it is never caller-supplied, so it must never be advertised as caller input.
    /// </summary>
    [Fact]
    public async Task Document_CancelAndDeleteRoutes_DoNotAdvertiseByUserAsAParameter()
    {
        using var doc = await FetchDocumentAsync();
        var paths = doc.RootElement.GetProperty("paths");

        var cancelParameters = paths.GetProperty("/v1/purchase-orders/{id}/cancel").GetProperty("post")
            .GetProperty("parameters").EnumerateArray().Select(p => p.GetProperty("name").GetString());
        cancelParameters.ShouldBe(["id"]);

        var deleteParameters = paths.GetProperty("/v1/purchase-orders/{id}").GetProperty("delete")
            .GetProperty("parameters").EnumerateArray().Select(p => p.GetProperty("name").GetString());
        deleteParameters.ShouldBe(["id"]);
    }

    [Fact]
    public async Task Document_PathIdParameter_IsRequiredUuidString()
    {
        using var doc = await FetchDocumentAsync();

        var parameters = doc.RootElement.GetProperty("paths")
            .GetProperty("/v1/purchase-orders/{id}")
            .GetProperty("get")
            .GetProperty("parameters");

        parameters.GetArrayLength().ShouldBe(1);
        var idParameter = parameters[0];
        idParameter.GetProperty("name").GetString().ShouldBe("id");
        idParameter.GetProperty("in").GetString().ShouldBe("path");
        idParameter.GetProperty("required").GetBoolean().ShouldBeTrue();
        idParameter.GetProperty("schema").GetProperty("type").GetString().ShouldBe("string");
        idParameter.GetProperty("schema").GetProperty("format").GetString().ShouldBe("uuid");
    }

    private async Task<JsonDocument> FetchDocumentAsync()
    {
        var client = fixture.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(body);
    }

    #endregion
}
