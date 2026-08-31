using System.Net.Http.Json;
using DKNet.EfCore.Specifications.Extensions;
using DKNet.EfCore.Specifications.Repositories;
using Minimal.App.TestSupport;
using Minimal.App.Tests.Integration.Support;
using Minimal.AppServices.ManualSample.V1;
using Minimal.AppServices.ManualSample.V1.Specs;
using Minimal.Share;

namespace Minimal.App.Tests.Integration.EndpointConfig;

/// <summary>
/// Re-homes the platform coverage <c>EndpointStampingAndVersioningTests</c> (deleted with the removed demo
/// entity's teardown) onto <c>PurchaseOrder</c>: the versioning gate governing route shape, and acting-user
/// attribution — sourced entirely from <c>[FromClaim]</c> + <c>AddContextualRequestPopulation</c>, never
/// stamped by the endpoint itself. <see cref="SharedConsts.SystemAccount" /> is only ever the resolved value
/// when <c>RequireAuthorization</c> is off (anonymous caller); an authenticated caller whose token carries no
/// <see cref="System.Security.Claims.ClaimTypes.Name" /> claim gets an unresolved (null) <c>ByUser</c> and the
/// write is refused — never attributed to <see cref="SharedConsts.SystemAccount" />. Payload-spoofing
/// resistance is already covered by <c>PurchaseOrderSecurityTests</c>; this class does not duplicate it.
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
    /// <c>RequireAuthorization</c> off → <c>Identity?.Name</c> is null on the anonymous caller, so contextual
    /// population resolves <c>ByUser</c> to its configured <c>SystemAccountFallback</c> (<c>Program.cs:23</c>) —
    /// the endpoint stamps nothing itself.
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
    /// <c>RequireAuthorization</c> on but the caller's token carries no <c>ClaimTypes.Name</c> claim → the
    /// <c>[FromClaim]</c> resolver cannot resolve <c>ByUser</c>, and an authenticated-but-unresolved member never
    /// receives the <see cref="SharedConsts.SystemAccount" /> fallback (that only applies when authorization is
    /// off — see <see cref="AuthorizationOff_CreateIsAttributedToSystemAccount" />) — it holds its default
    /// (<see langword="null" />) instead, so the handler's own <c>string.IsNullOrEmpty(ByUser)</c> guard refuses
    /// the write. Distinct from the automated sample's <c>DataOwnerHook</c> path (see
    /// <c>ProductSecurityTests</c>).
    /// </summary>
    [Fact]
    public async Task AuthenticatedCallerWithNoNameClaim_CreateIsRefused_NeverAttributedToSystemAccount()
    {
        using var fixture = new AuthOnNoNameClaimApiFixture();
        await fixture.InitializeAsync();
        var client = fixture.CreateClient();

        using var response = await CreateAsync(client, VersionedCreateUrl);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        using var scope = fixture.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRepositorySpec>();
        var count = await repository.CountAsync(new SpecGetPurchaseOrder(), CancellationToken.None);
        count.ShouldBe(0, "a refused write must not create a row attributed to anyone, System included.");
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
