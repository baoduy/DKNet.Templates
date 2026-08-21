using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using DKNet.EfCore.Specifications;
using DKNet.EfCore.Specifications.Extensions;
using Minimal.App.TestSupport;
using Minimal.App.Tests.Integration.Support;
using Minimal.AppServices.LoyaltyMemberships.V1;
using Minimal.AppServices.LoyaltyMemberships.V1.Actions;
using Minimal.AppServices.LoyaltyMemberships.V1.Specs;
using Minimal.Domains.Features.LoyaltyMemberships.Entities;
using Minimal.Share;
using SlimMessageBus;

namespace Minimal.App.Tests.Integration.LoyaltyMemberships.V1;

/// <summary>
/// Integration coverage for the loyalty-membership actions that BDD does not own: out-of-enum validation
/// rejection, failure results (not-found / empty id) asserted at the <c>Result</c> level, population-filter
/// attribution over HTTP, and mapper config. The domain-event publication scenarios (enrol / tier-changed /
/// points-only / withdrawal / rejected-enrolment) live in BDD — see
/// <c>Features/LoyaltyMemberships/LoyaltyMembershipEvents.feature</c>.
/// </summary>
public sealed class LoyaltyMembershipActionsIntegrationTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    #region Methods

    [Fact]
    public async Task PutWithOutOfEnumTierIsRejectedAndPersistsNothing()
    {
        await fixture.ResetDatabaseAsync();

        using var scope = fixture.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRepositorySpec>();

        var membership = new LoyaltyMembership("Alice Nguyen", MembershipTier.Silver, 0, "seed");
        await repository.AddAsync(membership, CancellationToken.None);
        await repository.SaveChangesAsync(CancellationToken.None);

        // A string tier (e.g. "platinum") would fail JSON model binding before ChangeMembershipCommandValidator
        // ever runs, since JsonStringEnumConverter rejects unknown names outright. The numeric form binds fine
        // (JsonStringEnumConverter allows integer values) and is what actually reaches the validator's IsInEnum().
        using var client = fixture.CreateClient();
        var payload = $$"""{"id":"{{membership.Id}}","tier":99}""";
        var response = await client.PutAsync(
            "/v1/loyalty-memberships",
            new StringContent(payload, Encoding.UTF8, "application/json"));

        response.IsSuccessStatusCode.ShouldBeFalse();

        var stored = await repository.FirstOrDefaultAsync(
            new SpecGetLoyaltyMembership(membership.Id),
            CancellationToken.None);
        stored.ShouldNotBeNull();
        stored.Tier.ShouldBe(MembershipTier.Silver);
    }

    [Fact]
    public async Task ChangeActionShouldFailWhenMembershipIsMissing()
    {
        await fixture.ResetDatabaseAsync();

        using var scope = fixture.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var missingId = Guid.NewGuid();

        var result = await bus.Send(new ChangeMembershipRequest { Id = missingId, Tier = MembershipTier.Gold });

        result.IsFailed.ShouldBeTrue();
        result.Errors.Select(x => x.Message).ShouldContain($"The Membership {missingId} is not found.");
    }

    [Fact]
    public async Task WithdrawActionShouldFailWhenIdIsEmpty()
    {
        await fixture.ResetDatabaseAsync();

        using var scope = fixture.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var result = await bus.Send(new WithdrawMembershipRequest { Id = Guid.Empty });

        result.IsFailed.ShouldBeTrue();
        result.Errors.Select(x => x.Message).ShouldContain("The Id is in valid.");
    }

    /// <summary>
    /// <c>EnrollMembershipRequest.ByUser</c> is a <c>[FromClaim]</c> member. The scenarios above reach the
    /// handler via <c>bus.Send</c> directly (bypassing the HTTP pipeline, so <c>ByUser</c> is set by hand
    /// there). This test instead goes through the real endpoint, proving the population filter attributes
    /// <c>CreatedBy</c> for enrolment, and that a caller-supplied <c>byUser</c> in the request body never
    /// survives.
    /// </summary>
    [Fact]
    public async Task EnrollActionViaHttp_StampsCreatedByFromThePopulationFilterNotTheRequestBody()
    {
        await fixture.ResetDatabaseAsync();
        using var client = fixture.CreateClient();

        using var response = await client.PostAsJsonAsync("/v1/loyalty-memberships", new
        {
            memberName = "Http Enroll",
            tier = MembershipTier.Bronze,
            points = 0,
            byUser = "attacker-supplied-value"
        });
        response.IsSuccessStatusCode.ShouldBeTrue();

        using var scope = fixture.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRepositorySpec>();
        var enrolled = await repository.FirstOrDefaultAsync(
            new SpecGetLoyaltyMembership(byMemberName: "Http Enroll"),
            CancellationToken.None);

        enrolled.ShouldNotBeNull();
        enrolled.LastModifiedBy.ShouldNotBe("attacker-supplied-value");
        enrolled.LastModifiedBy.ShouldBe(SharedConsts.SystemAccount);
    }

    /// <summary>
    /// Same proof as <see cref="EnrollActionViaHttp_StampsCreatedByFromThePopulationFilterNotTheRequestBody" />
    /// for the change (tier/points update) action.
    /// </summary>
    [Fact]
    public async Task ChangeActionViaHttp_StampsUpdatedByFromThePopulationFilterNotTheRequestBody()
    {
        await fixture.ResetDatabaseAsync();

        Guid membershipId;
        using (var seedScope = fixture.CreateScope())
        {
            var seedRepository = seedScope.ServiceProvider.GetRequiredService<IRepositorySpec>();
            var membership = new LoyaltyMembership("Http Change", MembershipTier.Silver, 0, "seed");
            await seedRepository.AddAsync(membership, CancellationToken.None);
            await seedRepository.SaveChangesAsync(CancellationToken.None);
            membershipId = membership.Id;
        }

        using var client = fixture.CreateClient();
        using var response = await client.PutAsJsonAsync("/v1/loyalty-memberships", new
        {
            id = membershipId,
            tier = MembershipTier.Gold,
            byUser = "attacker-supplied-value"
        });
        response.IsSuccessStatusCode.ShouldBeTrue();

        // Fresh scope: the seeding DbContext above still tracks the pre-update entity, and EF Core's identity
        // map would otherwise hand back that stale tracked instance instead of re-reading what the HTTP
        // request (a separate DbContext) actually persisted.
        using var verifyScope = fixture.CreateScope();
        var verifyRepository = verifyScope.ServiceProvider.GetRequiredService<IRepositorySpec>();
        var updated = await verifyRepository.FirstOrDefaultAsync(
            new SpecGetLoyaltyMembership(membershipId),
            CancellationToken.None);

        updated.ShouldNotBeNull();
        updated.Tier.ShouldBe(MembershipTier.Gold);
        updated.LastModifiedBy.ShouldNotBe("attacker-supplied-value");
        updated.LastModifiedBy.ShouldBe(SharedConsts.SystemAccount);
    }

    /// <summary>
    /// <c>WithdrawMembershipRequest.ByUser</c> is declared <c>[FromClaim]</c> like the other five adopting
    /// models, but its handler never reads it — there is no persisted attribution to assert on after a
    /// withdrawal. This proves the population filter doesn't break the request pipeline for a model that
    /// doesn't consume its populated member.
    /// </summary>
    [Fact]
    public async Task WithdrawActionViaHttp_SucceedsWithThePopulationFilterInThePipeline()
    {
        await fixture.ResetDatabaseAsync();

        Guid membershipId;
        using (var seedScope = fixture.CreateScope())
        {
            var seedRepository = seedScope.ServiceProvider.GetRequiredService<IRepositorySpec>();
            var membership = new LoyaltyMembership("Http Withdraw", MembershipTier.Bronze, 0, "seed");
            await seedRepository.AddAsync(membership, CancellationToken.None);
            await seedRepository.SaveChangesAsync(CancellationToken.None);
            membershipId = membership.Id;
        }

        using var client = fixture.CreateClient();
        using var response = await client.DeleteAsync($"/v1/loyalty-memberships?id={membershipId}");
        response.IsSuccessStatusCode.ShouldBeTrue();

        using var verifyScope = fixture.CreateScope();
        var verifyRepository = verifyScope.ServiceProvider.GetRequiredService<IRepositorySpec>();
        var stored = await verifyRepository.FirstOrDefaultAsync(new SpecGetLoyaltyMembership(membershipId), CancellationToken.None);
        stored.ShouldBeNull();
    }

    [Fact]
    public void Test_LoyaltyMembership_Mapping()
    {
        var mapper = fixture.Services.GetRequiredService<IMapper>();
        var membership = new LoyaltyMembership("Mapping Test", MembershipTier.Bronze, 10, "seed");

        var dto = mapper.Map<LoyaltyMembershipDto>(membership);

        dto.ShouldNotBeNull();
        dto.MemberName.ShouldBe("Mapping Test");
        dto.Tier.ShouldBe(MembershipTier.Bronze);
        dto.Points.ShouldBe(10);
    }

    #endregion
}
