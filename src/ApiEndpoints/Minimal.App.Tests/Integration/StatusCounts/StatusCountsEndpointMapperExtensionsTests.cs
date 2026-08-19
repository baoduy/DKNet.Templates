using DKNet.EfCore.Specifications;
using Minimal.App.Tests.Integration.Support;
using Minimal.AppServices.Share.Generics;
using Minimal.Domains.Features.LoyaltyMemberships.Entities;

namespace Minimal.App.Tests.Integration.StatusCounts;

/// <summary>
/// DRK-500 "Also verify": <c>MapGetStatusCounts</c> was relocated to its own template-local file
/// (status-count endpoints stay template-local per §4) while everything else moved into the package. No
/// current <see cref="Minimal.Api.ApiEndpoints"/> config calls it, so there is no HTTP route to hit through
/// the real host — this exercises the exact <c>GetStatusCounts</c> call its handler delegates to, against the
/// same fully-wired DI/EF stack (row-level filters included) the real app uses, proving the relocation didn't
/// break the underlying query.
/// </summary>
public sealed class StatusCountsEndpointMapperExtensionsTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    [Fact]
    public async Task GetStatusCounts_ShouldReturnAllEnumValues_WithRealCountsForSeededOnes()
    {
        await fixture.ResetDatabaseAsync();

        using var scope = fixture.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRepositorySpec>();

        await repository.AddAsync(new LoyaltyMembership("Alice Nguyen", MembershipTier.Gold, 0, "seed"),
            CancellationToken.None);
        await repository.SaveChangesAsync(CancellationToken.None);

        var results = await repository.GetStatusCounts<LoyaltyMembership>(
            new StatusPropertyInfo(nameof(LoyaltyMembership.Tier), typeof(MembershipTier)),
            new GenericStatusCountsParameters());

        results.ShouldContain(r => r.Type == nameof(MembershipTier) && r.Status == "GOLD" && r.Count == 1);
        results.ShouldContain(r => r.Type == nameof(MembershipTier) && r.Status == "BRONZE" && r.Count == 0);
        results.ShouldContain(r => r.Type == nameof(MembershipTier) && r.Status == "SILVER" && r.Count == 0);
    }
}
