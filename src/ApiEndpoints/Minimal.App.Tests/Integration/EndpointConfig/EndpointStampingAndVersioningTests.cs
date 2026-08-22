using System.Net.Http.Json;
using DKNet.EfCore.Specifications.Extensions;
using DKNet.EfCore.Specifications.Repositories;
using Minimal.App.TestSupport;
using Minimal.App.Tests.Integration.Support;
using Minimal.AppServices.CustomerProfiles.V1.Specs;
using Minimal.Domains.Features.Profiles.Entities;
using Minimal.Share;

namespace Minimal.App.Tests.Integration.EndpointConfig;

/// <summary>
/// Covers DRK-563/DRK-565's "scaffolded service" scenarios end to end through the real <c>Program.cs</c> host:
/// the versioning gate governing the route shape, and the <c>[FromClaim(ClaimTypes.Name)]</c> +
/// <c>AddContextualRequestPopulation</c> mechanism (registered once on <c>Program.cs</c>'s service collection,
/// applied to every mapped endpoint by an <c>AddEndpointFilterFactory</c> in <c>DKNet.AspCore.Extensions</c>)
/// attributing <c>ByUser</c> to either the template's stand-in system account (<see cref="SharedConsts.SystemAccount" />,
/// only when <c>RequireAuthorization</c> is off) or the authenticated caller (when it is on). Also proves the
/// mechanism's non-forgeability guarantee: <c>ByUser</c> is a plain settable property with no
/// <c>[JsonIgnore]</c>, so a caller can put a value for it in the request body, but the population filter runs
/// before the handler and unconditionally overwrites it — the caller-supplied value never survives.
/// </summary>
public sealed class EndpointStampingAndVersioningTests
{
    private const string VersionedUrl = "/v1/customer-profiles";
    private const string UnversionedUrl = "/customer-profiles";

    #region Methods

    /// <summary>
    /// <c>RequireAuthorization</c> off → the resolver finds no <c>ClaimTypes.Name</c> claim on the (anonymous)
    /// caller, so <c>ContextualRequestPopulationService</c> falls back to <see cref="SharedConsts.SystemAccount" />.
    /// </summary>
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

    /// <summary>
    /// <c>RequireAuthorization</c> on and the caller carries a <c>ClaimTypes.Name</c> claim → the resolver
    /// populates <c>ByUser</c> from that claim; the <see cref="SharedConsts.SystemAccount" /> fallback never
    /// applies to an authenticated caller.
    /// </summary>
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

    /// <summary>
    /// Non-forgeability, authorization off: <c>ByUser</c> has no <c>[JsonIgnore]</c>, so a caller can put its
    /// own value in the request body. The population filter overwrites it before the handler runs — the
    /// caller-supplied value never survives, and the system-account fallback wins instead.
    /// </summary>
    [Fact]
    public async Task CallerSuppliedByUserInRequestBodyIsNeverPersisted_SystemAccountFallbackWinsWhenAuthorizationIsOff()
    {
        using var fixture = new ApiFixture();
        await fixture.InitializeAsync();
        var client = fixture.CreateClient();

        var profile = await SeedProfileAsync(fixture, "forged-stamp-off@example.com");

        using var response = await client.PutAsJsonAsync(VersionedUrl, new
        {
            id = profile.Id,
            name = "Updated By Forged Value",
            phone = "+6500000094",
            byUser = "attacker-supplied-value"
        });

        response.IsSuccessStatusCode.ShouldBeTrue();

        var updated = await FindProfileAsync(fixture, profile.Id);
        updated.ShouldNotBeNull();
        updated.LastModifiedBy.ShouldNotBe("attacker-supplied-value");
        updated.LastModifiedBy.ShouldBe(SharedConsts.SystemAccount);
    }

    /// <summary>
    /// Non-forgeability, authorization on: even though the caller is authenticated, its request-body
    /// <c>byUser</c> value is still overwritten by the population filter — the persisted attribution always
    /// resolves to the authenticated caller's own claim, never the value the caller typed into the body.
    /// </summary>
    [Fact]
    public async Task CallerSuppliedByUserInRequestBodyIsNeverPersisted_AuthenticatedCallerWinsWhenAuthorizationIsOn()
    {
        using var fixture = new AuthOnApiFixture();
        await fixture.InitializeAsync();
        var client = fixture.CreateClient();

        var profile = await SeedProfileAsync(fixture, "forged-stamp-on@example.com");

        using var response = await client.PutAsJsonAsync(VersionedUrl, new
        {
            id = profile.Id,
            name = "Updated By Forged Value",
            phone = "+6500000093",
            byUser = "attacker-supplied-value"
        });

        response.IsSuccessStatusCode.ShouldBeTrue();

        var updated = await FindProfileAsync(fixture, profile.Id);
        updated.ShouldNotBeNull();
        updated.LastModifiedBy.ShouldNotBe("attacker-supplied-value");
        updated.LastModifiedBy.ShouldBe(TestAuthHandler.CallerName);
    }

    /// <summary>
    /// Missing-claim default: an authenticated caller (<c>RequireAuthorization</c> on) whose token carries no
    /// <c>ClaimTypes.Name</c> claim resolves <c>ByUser</c> to <see langword="null" /> — the
    /// <see cref="SharedConsts.SystemAccount" /> fallback only applies when authorization is off, so it is never
    /// substituted here. None of the six adopting request models' validators require <c>ByUser</c>, so
    /// FluentValidation never rejects the request for it; the null instead reaches
    /// <c>CustomerProfile.Update</c>/<c>SetUpdatedBy</c>, whose own guard rejects a null/blank user id — the
    /// request fails downstream as a 500 (via the host's <c>GlobalExceptionHandler</c>), not as a 400 validation
    /// error and not as a silently-accepted null attribution. This is existing domain behaviour predating this
    /// adoption (the audit-trail base class has always required a non-blank user id to record an update), not a
    /// regression introduced by <c>[FromClaim]</c>.
    /// </summary>
    [Fact]
    public async Task MissingNameClaimWhileAuthenticated_ByUserHoldsItsDefaultAndNeverFallsBackToSystemAccount()
    {
        using var fixture = new AuthOnNoNameClaimApiFixture();
        await fixture.InitializeAsync();
        var client = fixture.CreateClient();

        var profile = await SeedProfileAsync(fixture, "missing-claim@example.com");

        using var response = await client.PutAsJsonAsync(VersionedUrl, new
        {
            id = profile.Id,
            name = "Updated Without Name Claim",
            phone = "+6500000092"
        });

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);

        // The failed SetUpdatedBy call never reaches SaveChanges, so the profile keeps its seeded attribution —
        // proving the null ByUser was never silently substituted with the system-account fallback.
        var untouched = await FindProfileAsync(fixture, profile.Id);
        untouched.ShouldNotBeNull();
        untouched.LastModifiedBy.ShouldBe("seed");
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
