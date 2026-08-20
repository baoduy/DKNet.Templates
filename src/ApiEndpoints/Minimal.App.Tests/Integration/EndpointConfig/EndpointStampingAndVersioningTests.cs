using System.Net.Http.Json;
using DKNet.EfCore.Specifications;
using DKNet.EfCore.Specifications.Extensions;
using Minimal.App.TestSupport;
using Minimal.App.Tests.Integration.Support;
using Minimal.AppServices.CustomerProfiles.V1.Specs;
using Minimal.Domains.Features.Profiles.Entities;
using Minimal.Share;

namespace Minimal.App.Tests.Integration.EndpointConfig;

/// <summary>
/// Covers spec §5's two "scaffolded service" scenarios end to end through the real <c>Program.cs</c> host:
/// the versioning gate governing the route shape, and the <c>ConfigureGroup</c> stamping filter attributing
/// <c>ByUser</c> to either the template's stand-in system account or the authenticated caller, depending on
/// whether <c>RequireAuthorization</c> is on.
/// </summary>
public sealed class EndpointStampingAndVersioningTests
{
    private const string VersionedUrl = "/v1/customer-profiles";
    private const string UnversionedUrl = "/customer-profiles";

    #region Methods

    [Fact]
    public async Task VersioningOnAndAuthorizationOff_UpdateIsServedFromVersion1RouteAndStampedWithSystemAccount()
    {
        using var fixture = new ApiFixture();
        await fixture.InitializeAsync();
        var client = fixture.CreateClient();

        var profile = await SeedProfileAsync(fixture, "system-stamp@example.com");

        using var response = await client.PutAsJsonAsync(VersionedUrl, new
        {
            id = profile.Id,
            name = "Updated By System",
            phone = "+6500000099"
        });

        response.IsSuccessStatusCode.ShouldBeTrue();

        var updated = await FindProfileAsync(fixture, profile.Id);
        updated.ShouldNotBeNull();
        updated.LastModifiedBy.ShouldBe(SharedConsts.SystemAccount);
    }

    [Fact]
    public async Task VersioningOnAndAuthorizationOn_UpdateIsAttributedToTheAuthenticatedCaller()
    {
        using var fixture = new AuthOnApiFixture();
        await fixture.InitializeAsync();
        var client = fixture.CreateClient();

        var profile = await SeedProfileAsync(fixture, "caller-stamp@example.com");

        using var response = await client.PutAsJsonAsync(VersionedUrl, new
        {
            id = profile.Id,
            name = "Updated By Caller",
            phone = "+6500000098"
        });

        response.IsSuccessStatusCode.ShouldBeTrue();

        var updated = await FindProfileAsync(fixture, profile.Id);
        updated.ShouldNotBeNull();
        updated.LastModifiedBy.ShouldBe(TestAuthHandler.CallerName);
    }

    [Fact]
    public async Task VersioningOff_RouteCarriesNoVersionSegment()
    {
        using var fixture = new VersioningOffApiFixture();
        await fixture.InitializeAsync();
        var client = fixture.CreateClient();

        var profile = await SeedProfileAsync(fixture, "no-version@example.com");

        using var unversionedResponse = await client.PutAsJsonAsync(UnversionedUrl, new
        {
            id = profile.Id,
            name = "Updated Unversioned",
            phone = "+6500000097"
        });
        unversionedResponse.IsSuccessStatusCode.ShouldBeTrue();

        using var versionedResponse = await client.PutAsJsonAsync(VersionedUrl, new
        {
            id = profile.Id,
            name = "Should Not Apply",
            phone = "+6500000096"
        });
        versionedResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task VersioningOn_TheVersion1RouteRemainsUnchanged()
    {
        using var fixture = new ApiFixture();
        await fixture.InitializeAsync();
        var client = fixture.CreateClient();

        var profile = await SeedProfileAsync(fixture, "version-unchanged@example.com");

        using var unversionedResponse = await client.PutAsJsonAsync(UnversionedUrl, new
        {
            id = profile.Id,
            name = "Should Not Apply",
            phone = "+6500000095"
        });

        unversionedResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private static async Task<CustomerProfile> SeedProfileAsync(TestApiFactoryBase fixture, string email)
    {
        using var scope = fixture.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRepositorySpec>();

        var profile = new CustomerProfile("Seed User", "MS-STAMP-0001", email, "+6500000000", "seed");
        await repository.AddAsync(profile, CancellationToken.None);
        await repository.SaveChangesAsync(CancellationToken.None);

        return profile;
    }

    private static async Task<CustomerProfile?> FindProfileAsync(TestApiFactoryBase fixture, Guid id)
    {
        using var scope = fixture.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRepositorySpec>();

        return await repository.FirstOrDefaultAsync(new SpecGetCustomerProfile(id), CancellationToken.None);
    }

    #endregion
}
