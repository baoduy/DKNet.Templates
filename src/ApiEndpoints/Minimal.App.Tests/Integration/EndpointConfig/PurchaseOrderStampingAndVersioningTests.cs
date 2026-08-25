using System.Net.Http.Json;
using Minimal.App.TestSupport;
using Minimal.App.Tests.Integration.Support;
using Minimal.AppServices.ManualSample.V1;
using Minimal.Share;

namespace Minimal.App.Tests.Integration.EndpointConfig;

/// <summary>
/// Re-homes the platform coverage <c>EndpointStampingAndVersioningTests</c> (deleted with the removed demo
/// entity's teardown) onto <c>PurchaseOrder</c>: the versioning gate governing route shape, and the
/// endpoint's own <c>user.Identity?.Name ?? SharedConsts.SystemAccount</c> attribution — falling back to
/// <see cref="SharedConsts.SystemAccount" /> both when authorization is off (anonymous caller) and when the
/// caller is authenticated but its token carries no <see cref="System.Security.Claims.ClaimTypes.Name" /> claim.
/// Payload-spoofing resistance is already covered by <c>PurchaseOrderSecurityTests</c>; this class does not
/// duplicate it.
/// </summary>
public sealed class PurchaseOrderStampingAndVersioningTests
{
    private const string VersionedCreateUrl = "/v1/purchase-orders";
    private const string UnversionedCreateUrl = "/purchase-orders";

    #region Methods

    [Fact]
    public async Task VersioningOn_UnversionedRouteIsNotMapped()
    {
        using var fixture = new ApiFixture();
        await fixture.InitializeAsync();
        var client = fixture.CreateClient();

        using var response = await CreateAsync(client, UnversionedCreateUrl);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task VersioningOff_RouteCarriesNoVersionSegment_AndVersionedRouteIsGone()
    {
        using var fixture = new VersioningOffApiFixture();
        await fixture.InitializeAsync();
        var client = fixture.CreateClient();

        using var unversionedResponse = await CreateAsync(client, UnversionedCreateUrl);
        unversionedResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        using var versionedResponse = await CreateAsync(client, VersionedCreateUrl);
        versionedResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// <c>RequireAuthorization</c> off → <c>user.Identity?.Name</c> is null on the anonymous caller, so the
    /// endpoint's own fallback stamps <see cref="SharedConsts.SystemAccount" />.
    /// </summary>
    [Fact]
    public async Task AuthorizationOff_CreateIsAttributedToSystemAccount()
    {
        using var fixture = new ApiFixture();
        await fixture.InitializeAsync();
        var client = fixture.CreateClient();

        using var response = await CreateAsync(client, VersionedCreateUrl);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var dto = await response.Content.ReadFromJsonAsync<PurchaseOrderDto>(SharedConsts.JsonSerializerOptions);
        dto!.CreatedBy.ShouldBe(SharedConsts.SystemAccount);
    }

    /// <summary>
    /// <c>RequireAuthorization</c> on but the caller's token carries no <c>ClaimTypes.Name</c> claim →
    /// <c>user.Identity?.Name</c> is still null despite the caller being authenticated, so the same
    /// <see cref="SharedConsts.SystemAccount" /> fallback applies. Distinct from the automated sample's
    /// <c>DataOwnerHook</c> path (see <c>ProductSecurityTests</c>) — the manual sample never relies on
    /// <c>[FromClaim]</c> population for this attribution, it is stamped inline by the endpoint.
    /// </summary>
    [Fact]
    public async Task AuthenticatedCallerWithNoNameClaim_CreateFallsBackToSystemAccount_NotA500()
    {
        using var fixture = new AuthOnNoNameClaimApiFixture();
        await fixture.InitializeAsync();
        var client = fixture.CreateClient();

        using var response = await CreateAsync(client, VersionedCreateUrl);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var dto = await response.Content.ReadFromJsonAsync<PurchaseOrderDto>(SharedConsts.JsonSerializerOptions);
        dto!.CreatedBy.ShouldBe(SharedConsts.SystemAccount);
    }

    private static async Task<HttpResponseMessage> CreateAsync(HttpClient client, string url)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(new { customerName = "Acme Pte Ltd", amount = 100m })
        };
        request.Headers.Add("X-Idempotency-Key", Guid.NewGuid().ToString());

        return await client.SendAsync(request);
    }

    #endregion
}
