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

    /// <summary>
    /// DRK-506 §5 "Status counts over the full history": DRK-521's Build stage removed the implicit 30-day
    /// window (<see cref="GenericStatusCountsParameters.From"/>/<see cref="GenericStatusCountsParameters.To"/> now
    /// apply only when supplied), so a call with no date parameters must count records regardless of age.
    /// </summary>
    [Fact]
    public async Task GetStatusCounts_ShouldCountRecordsOfAnyAge_WhenNoDateRangeGiven()
    {
        await fixture.ResetDatabaseAsync();

        using var scope = fixture.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRepositorySpec>();

        var recentMember = new LoyaltyMembership("Recent Nguyen", MembershipTier.Gold, 0, "seed");
        var historicalMember = new LoyaltyMembership("Historical Tran", MembershipTier.Gold, 0, "seed");

        await repository.AddAsync(recentMember, CancellationToken.None);
        await repository.AddAsync(historicalMember, CancellationToken.None);
        repository.Entry(historicalMember).Property(x => x.CreatedOn).CurrentValue =
            DateTimeOffset.UtcNow.AddDays(-90);
        await repository.SaveChangesAsync(CancellationToken.None);

        var results = await repository.GetStatusCounts<LoyaltyMembership>(
            new StatusPropertyInfo(nameof(LoyaltyMembership.Tier), typeof(MembershipTier)),
            new GenericStatusCountsParameters());

        results.ShouldContain(r =>
            r.Type == nameof(MembershipTier) && r.Status == "GOLD" && r.Count == 2,
            "a 90-day-old record must still be counted when no From/To is supplied.");
    }

    /// <summary>
    /// DRK-506 §5 "Status counts over the full history": an explicit date range must still narrow the result
    /// even though the full-history default (no bound) now applies when dates are absent.
    /// </summary>
    [Fact]
    public async Task GetStatusCounts_ShouldExcludeRecordsOutsideRange_WhenExplicitDateRangeGiven()
    {
        await fixture.ResetDatabaseAsync();

        using var scope = fixture.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRepositorySpec>();

        var recentMember = new LoyaltyMembership("Recent Nguyen", MembershipTier.Gold, 0, "seed");
        var historicalMember = new LoyaltyMembership("Historical Tran", MembershipTier.Gold, 0, "seed");

        await repository.AddAsync(recentMember, CancellationToken.None);
        await repository.AddAsync(historicalMember, CancellationToken.None);
        repository.Entry(historicalMember).Property(x => x.CreatedOn).CurrentValue =
            DateTimeOffset.UtcNow.AddDays(-90);
        await repository.SaveChangesAsync(CancellationToken.None);

        var results = await repository.GetStatusCounts<LoyaltyMembership>(
            new StatusPropertyInfo(nameof(LoyaltyMembership.Tier), typeof(MembershipTier)),
            new GenericStatusCountsParameters { From = DateTimeOffset.UtcNow.AddDays(-7) });

        results.ShouldContain(r =>
            r.Type == nameof(MembershipTier) && r.Status == "GOLD" && r.Count == 1,
            "the 90-day-old record must be excluded once an explicit From narrows the window to the last 7 days.");
    }
}
