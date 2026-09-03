using Minimal.App.Tests.Integration.Support;

namespace Minimal.App.Tests.Integration.Swagger;

/// <summary>
/// DRK-1028 §5: outside a Development environment, interactive documentation (<c>/docs</c> and the OpenAPI
/// document) requires an authenticated caller, independent of <c>EnableSwagger</c>. The Testing environment used
/// throughout this suite (see <see cref="TestApiFactoryBase" />) counts as "outside Development" for this check.
/// </summary>
public sealed class DocsAuthorizationTests(RequireAuthNoHandlerApiFixture fixture) : IClassFixture<RequireAuthNoHandlerApiFixture>
{
    [Fact]
    public async Task AnonymousCaller_CannotReachInteractiveDocumentation()
    {
        var response = await fixture.CreateClient().GetAsync("/docs");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AnonymousCaller_CannotReachTheOpenApiDocument()
    {
        var response = await fixture.CreateClient().GetAsync("/openapi/v1.json");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
