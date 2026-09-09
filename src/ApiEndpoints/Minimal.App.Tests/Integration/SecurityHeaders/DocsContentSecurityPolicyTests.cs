using Minimal.App.Tests.Integration.Support;

namespace Minimal.App.Tests.Integration.SecurityHeaders;

/// <summary>
/// Scalar's documentation page hands the OpenAPI document's URL to its bundle from an inline
/// <c>&lt;script type="module"&gt;</c>, so the strict <c>script-src 'self'</c> policy leaves the page rendering
/// empty and never requesting the document at all. <c>SecurityHeadersConfig</c> therefore relaxes
/// <c>script-src</c> for that one path — and must not relax it anywhere else, the OpenAPI document included.
/// </summary>
public sealed class DocsContentSecurityPolicyTests(SwaggerOnApiFixture fixture) : IClassFixture<SwaggerOnApiFixture>
{
    private const string HeaderName = "Content-Security-Policy";
    private const string InlineScripts = "'unsafe-inline'";

    #region Methods

    [Fact]
    public async Task TheDocumentationPage_AllowsItsInlineBootstrapScript()
    {
        var policy = await FetchPolicyAsync("/docs");

        policy.ShouldContain("script-src");
        policy.ShouldContain(InlineScripts);
    }

    [Fact]
    public async Task TheOpenApiDocument_KeepsTheStrictPolicy()
    {
        var policy = await FetchPolicyAsync("/openapi/v1.json");

        policy.ShouldContain("script-src 'self'");
        policy.ShouldNotContain(InlineScripts);
    }

    [Fact]
    public async Task AnOrdinaryRoute_KeepsTheStrictPolicy()
    {
        var policy = await FetchPolicyAsync("/healthz");

        policy.ShouldNotContain(InlineScripts);
    }

    private async Task<string> FetchPolicyAsync(string path)
    {
        var response = await fixture.CreateClient().GetAsync(path);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var values = response.Headers.TryGetValues(HeaderName, out var fromResponse)
            ? fromResponse
            : response.Content.Headers.TryGetValues(HeaderName, out var fromContent)
                ? fromContent
                : throw new InvalidOperationException($"No {HeaderName} header on {path}.");

        return string.Join(';', values);
    }

    #endregion
}
