using System.Net.Http.Json;
using Minimal.App.TestSupport;
using Minimal.App.Tests.Integration.Support;
using Minimal.AppServices.AutomatedSample.V1;

namespace Minimal.App.Tests.Integration.AutomatedSample.V1;

/// <summary>
/// The automated sample's half of the cycle's sharpest acceptance point. Unlike the manual sample, the
/// generated create request has no acting-user property at all to spoof (see
/// <c>Architecture.SampleInvariantTests.GeneratedCreateProductRequest_ShouldCarryNoActingUserProperty</c>) —
/// this test proves the runtime consequence: <c>CreatedBy</c> is stamped from the authenticated caller via
/// <c>DataOwnerHook</c>, regardless of anything the payload contains.
/// </summary>
/// <remarks>
/// <c>DataOwnerHook</c> stamps <c>CreatedBy</c> from <c>IDataOwnerProvider.GetOwnershipKey()</c>, which
/// <c>PrincipalProvider</c> implements as <c>ProfileId.ToString()</c> (read from the <c>NameIdentifier</c>
/// claim) — not from the caller's name. Assert against <see cref="TestAuthHandler.CallerProfileId"/>, not
/// <see cref="TestAuthHandler.CallerName"/>.
/// </remarks>
public sealed class ProductSecurityTests(AuthOnApiFixture fixture) : IClassFixture<AuthOnApiFixture>
{
    [Fact]
    public async Task Create_ShouldStampCreatedByFromAuthenticatedCallersOwnershipKey()
    {
        await fixture.ResetDatabaseAsync();
        var client = fixture.CreateClient();

        var response = await client.PostAsJsonAsync("/v1/products", new { name = "Widget", price = 9.99m });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var dto = await response.Content.ReadFromJsonAsync<ProductDto>();
        dto!.CreatedBy.ShouldBe(TestAuthHandler.CallerProfileId.ToString());
    }

    [Fact]
    public async Task Create_ShouldIgnoreAnyExtraActingUserFieldInThePayload()
    {
        // Even a caller who tries to smuggle an acting-user value onto an unknown JSON property gets nothing —
        // System.Text.Json ignores properties the target type doesn't declare, and CreateProductRequest
        // (generated) declares none.
        await fixture.ResetDatabaseAsync();
        var client = fixture.CreateClient();

        var response = await client.PostAsJsonAsync("/v1/products", new
        {
            name = "Gadget",
            price = 5.00m,
            createdBy = "someone-else",
            byUser = "someone-else"
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var dto = await response.Content.ReadFromJsonAsync<ProductDto>();
        dto!.CreatedBy.ShouldBe(TestAuthHandler.CallerProfileId.ToString());
        dto.CreatedBy.ShouldNotBe("someone-else");
    }
}
