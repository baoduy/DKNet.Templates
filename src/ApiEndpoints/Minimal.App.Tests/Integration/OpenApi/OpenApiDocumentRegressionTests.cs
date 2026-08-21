using Minimal.App.Tests.Integration.Support;

namespace Minimal.App.Tests.Integration.OpenApi;

/// <summary>
/// Pins the generated <c>v1</c> OpenAPI document's shape as a regression guard for [D586-1]'s removal of the
/// four post-processors (<c>BearerSecurityTransformer</c>, <c>ExcludeInterfaceSchemaFilter</c>,
/// <c>JsonStringEnumSchemaTransformer</c>, <c>PathParameterOperationTransformer</c>) — every registration was
/// already commented out before deletion, so this asserts the document is unaffected by removing dead code,
/// not that the deletions changed anything. In particular, enum values are still emitted as camelCase strings
/// purely from <see cref="Minimal.Share.SharedConsts.JsonSerializerOptions" />'s <c>JsonStringEnumConverter</c>
/// wired through <c>ConfigureHttpJsonOptions</c> — proving the deleted enum transformer was redundant.
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
    public async Task Document_Paths_UseResolvedV1Segment_NotVersionPlaceholder()
    {
        // Confirms SwaggerConfig's own path-rewriting document transformer still runs — v{version} placeholders
        // are resolved to "v1" — independent of the four deleted post-processors.
        using var doc = await FetchDocumentAsync();
        var paths = doc.RootElement.GetProperty("paths");

        var pathNames = paths.EnumerateObject().Select(p => p.Name).Order().ToArray();
        pathNames.ShouldBe([
            "/v1/customer-profiles",
            "/v1/customer-profiles/{id}",
            "/v1/loyalty-memberships"
        ]);

        paths.GetProperty("/v1/customer-profiles").EnumerateObject().Select(m => m.Name).Order()
            .ShouldBe(["delete", "get", "post", "put"]);
        paths.GetProperty("/v1/customer-profiles/{id}").EnumerateObject().Select(m => m.Name)
            .ShouldBe(["get"]);
        paths.GetProperty("/v1/loyalty-memberships").EnumerateObject().Select(m => m.Name).Order()
            .ShouldBe(["delete", "post", "put"]);
    }

    [Fact]
    public async Task Document_PathIdParameter_IsRequiredUuidString()
    {
        // Guards against a regression the deleted PathParameterOperationTransformer could once have masked —
        // {id} must still resolve to a required string/uuid parameter without it.
        using var doc = await FetchDocumentAsync();

        var parameters = doc.RootElement.GetProperty("paths")
            .GetProperty("/v1/customer-profiles/{id}")
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

    [Fact]
    public async Task Document_MembershipTierEnum_IsSerializedAsCamelCaseStrings()
    {
        // The one behavioural claim the deleted JsonStringEnumSchemaTransformer's removal rests on: without any
        // enum-specific transformer, the schema still reflects the app's JsonStringEnumConverter — string
        // values, not the numeric defaults System.Text.Json's schema exporter would otherwise emit.
        using var doc = await FetchDocumentAsync();

        var enumSchema = doc.RootElement.GetProperty("components").GetProperty("schemas")
            .GetProperty("MembershipTier");

        var values = enumSchema.GetProperty("enum").EnumerateArray().Select(v => v.GetString()).ToArray();
        values.ShouldBe(["bronze", "silver", "gold"]);
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
