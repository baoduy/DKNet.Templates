using System.Net.Http.Json;
using Minimal.App.TestSupport;
using Minimal.App.Tests.Integration.Support;
using Minimal.AppServices.AutomatedSample.V1;

namespace Minimal.App.Tests.Integration.AutomatedSample.V1;

/// <summary>
/// Proves the row-level authorization boundary end to end through <c>DataOwnerHook</c> (write) and
/// <c>DataOwnerAuthQuery</c> (read filter) — not just that <c>PrincipalProvider</c> resolves distinct keys
/// (see <c>Unit.Configs.PrincipalProviderTests</c>). Two authenticated callers share one host/database via
/// <see cref="MultiSubjectAuthHandler"/>, so a collapse onto a shared ownership key would show up here as one
/// caller reading the other's row — not merely as an equal string somewhere.
/// </summary>
public sealed class ProductOwnershipIsolationTests(AuthOnMultiSubjectApiFixture fixture)
    : IClassFixture<AuthOnMultiSubjectApiFixture>
{
    [Fact]
    public async Task DifferentNonGuidSubjects_GetDistinctOwnershipKeys_AndNeitherCanReadTheOthersProduct()
    {
        await fixture.ResetDatabaseAsync();
        var client = fixture.CreateClient();

        var productA = await CreateProductAsync(client, "opaque-subject-a", oid: null, name: "A's Widget");
        var productB = await CreateProductAsync(client, "opaque-subject-b", oid: null, name: "B's Widget");

        productA.CreatedBy.ShouldBe("opaque-subject-a");
        productB.CreatedBy.ShouldBe("opaque-subject-b");
        productA.CreatedBy.ShouldNotBe(productB.CreatedBy);
        productA.CreatedBy.ShouldNotBe(Guid.Empty.ToString());
        productB.CreatedBy.ShouldNotBe(Guid.Empty.ToString());

        // Caller B must not be able to read caller A's product — the query filter denies it, not a 403.
        using var crossReadRequest = new HttpRequestMessage(HttpMethod.Get, $"/v1/products/{productA.Id}");
        crossReadRequest.Headers.Add(MultiSubjectAuthHandler.SubjectHeaderName, "opaque-subject-b");
        using var crossReadResponse = await client.SendAsync(crossReadRequest);
        crossReadResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // And caller B's own list must never surface caller A's row.
        using var listRequest = new HttpRequestMessage(HttpMethod.Get, "/v1/products?pageSize=100");
        listRequest.Headers.Add(MultiSubjectAuthHandler.SubjectHeaderName, "opaque-subject-b");
        using var listResponse = await client.SendAsync(listRequest);
        var envelope = await listResponse.Content.ReadFromJsonAsync<ProductListEnvelope>();
        envelope!.Items.ShouldContain(p => p.Id == productB.Id);
        envelope.Items.ShouldNotContain(p => p.Id == productA.Id);
    }

    [Fact]
    public async Task ObjectIdentifierClaim_TakesPrecedenceOverNameIdentifier_WhenResolvingOwnershipKey()
    {
        // The Entra v2.0 shape that motivated the finding: oid is a GUID, NameIdentifier (sub) is opaque.
        await fixture.ResetDatabaseAsync();
        var client = fixture.CreateClient();
        const string oid = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";

        var product = await CreateProductAsync(client, subject: "opaque-pairwise-sub", oid, name: "Oid Widget");

        product.CreatedBy.ShouldBe(oid);
    }

    [Fact]
    public async Task AuthenticatedCallerWithNoSubjectClaim_MustFailClosed_NotCrashOrLeaveAReadableRow()
    {
        // R3 — deny-closed, never a crash. A null ownership key must never surface as an unhandled 500: that
        // both leaks internal EF Core detail (column/entity names) in the response body and means the deny
        // path was never actually exercised end to end. Whatever the create's outcome, the caller must never
        // be able to read a row back afterwards, since GetAccessibleKeys() is empty for this caller.
        await fixture.ResetDatabaseAsync();
        var client = fixture.CreateClient();

        using var createRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/products")
        {
            Content = JsonContent.Create(new { name = "Orphan Widget", price = 1.23m })
        };
        // Deliberately no X-Test-Subject / X-Test-Oid header — authenticated, but no resolvable subject claim.
        using var createResponse = await client.SendAsync(createRequest);

        createResponse.StatusCode.ShouldNotBe(
            HttpStatusCode.InternalServerError,
            "an authenticated caller with no resolvable subject claim must fail closed, not crash the request pipeline");

        using var listRequest = new HttpRequestMessage(HttpMethod.Get, "/v1/products?pageSize=100");
        using var listResponse = await client.SendAsync(listRequest);
        var envelope = await listResponse.Content.ReadFromJsonAsync<ProductListEnvelope>();
        envelope!.Items.ShouldNotContain(p => p.Name == "Orphan Widget");
    }

    private static async Task<ProductDto> CreateProductAsync(HttpClient client, string subject, string? oid, string name)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/products")
        {
            Content = JsonContent.Create(new { name, price = 9.99m })
        };
        request.Headers.Add(MultiSubjectAuthHandler.SubjectHeaderName, subject);
        if (oid != null)
        {
            request.Headers.Add(MultiSubjectAuthHandler.ObjectIdHeaderName, oid);
        }

        using var response = await client.SendAsync(request);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<ProductDto>())!;
    }

    private sealed record ProductListEnvelope(List<ProductDto> Items);
}
