using DKNet.EfCore.Specifications;
using DKNet.EfCore.Specifications.Extensions;
using Minimal.App.Tests.Integration.Support;
using Minimal.AppServices.LoyaltyMemberships.V1;
using Minimal.AppServices.LoyaltyMemberships.V1.Actions;
using Minimal.AppServices.LoyaltyMemberships.V1.Specs;
using Minimal.Domains.Features.LoyaltyMemberships.Entities;
using SlimMessageBus;

namespace Minimal.App.Tests.Integration.LoyaltyMemberships.V1;

/// <summary>
/// Covers DRK-455 §5's loyalty-membership scenarios: enrolment, tier-changed, points-only-no-tier-change,
/// withdrawal, and rejected-enrolment publish nothing by hand — the aggregate's <c>[RaisesEvent]</c> declarations
/// are what actually raise them (see <see cref="Minimal.Domains.Features.LoyaltyMemberships.Entities.LoyaltyMembership"/>).
/// </summary>
public sealed class LoyaltyMembershipActionsIntegrationTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    #region Methods

    [Fact]
    public async Task EnrollingAMemberPublishesTheEnrolmentEvent()
    {
        await fixture.ResetDatabaseAsync();

        using var scope = fixture.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var repository = scope.ServiceProvider.GetRequiredService<IRepositorySpec>();

        var result = await bus.Send(new EnrollMembershipRequest
        {
            MemberName = "Alice Nguyen",
            Tier = MembershipTier.Silver,
            Points = 0,
            ByUser = "integration-test"
        });

        result.IsSuccess.ShouldBeTrue();
        await repository.SaveChangesAsync(CancellationToken.None);

        var stored = await repository.FirstOrDefaultAsync(
            new SpecGetLoyaltyMembership(byMemberName: "Alice Nguyen"),
            CancellationToken.None);
        stored.ShouldNotBeNull();

        Func<bool> logged = () => fixture.LogCapture.Messages.Any(m =>
            m.Contains("enrolled", StringComparison.OrdinalIgnoreCase) &&
            m.Contains("Alice Nguyen", StringComparison.Ordinal));
        (await Eventually.IsTrueAsync(logged)).ShouldBeTrue();

        fixture.LogCapture.Messages.Count(m =>
                m.Contains("enrolled", StringComparison.OrdinalIgnoreCase) &&
                m.Contains("Alice Nguyen", StringComparison.Ordinal))
            .ShouldBe(1);
    }

    [Fact]
    public async Task ChangingTheTierPublishesTheTierChangedEvent()
    {
        await fixture.ResetDatabaseAsync();

        using var scope = fixture.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var repository = scope.ServiceProvider.GetRequiredService<IRepositorySpec>();

        var membership = new LoyaltyMembership("Alice Nguyen", MembershipTier.Silver, 0, "seed");
        await repository.AddAsync(membership, CancellationToken.None);
        await repository.SaveChangesAsync(CancellationToken.None);
        fixture.LogCapture.Clear();

        var result = await bus.Send(new ChangeMembershipRequest
        {
            Id = membership.Id,
            Tier = MembershipTier.Gold,
            ByUser = "integration-test"
        });

        result.IsSuccess.ShouldBeTrue();
        await repository.SaveChangesAsync(CancellationToken.None);

        Func<bool> logged = () =>
            fixture.LogCapture.Messages.Any(m => m.Contains("tier changed to Gold", StringComparison.OrdinalIgnoreCase));
        (await Eventually.IsTrueAsync(logged)).ShouldBeTrue();

        fixture.LogCapture.Messages.Count(m => m.Contains("tier changed to Gold", StringComparison.OrdinalIgnoreCase))
            .ShouldBe(1);
    }

    [Fact]
    public async Task ChangingOnlyPointsDoesNotPublishTheTierChangedEvent()
    {
        await fixture.ResetDatabaseAsync();

        using var scope = fixture.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var repository = scope.ServiceProvider.GetRequiredService<IRepositorySpec>();

        var membership = new LoyaltyMembership("Alice Nguyen", MembershipTier.Gold, 120, "seed");
        await repository.AddAsync(membership, CancellationToken.None);
        await repository.SaveChangesAsync(CancellationToken.None);
        fixture.LogCapture.Clear();

        var result = await bus.Send(new ChangeMembershipRequest
        {
            Id = membership.Id,
            Tier = MembershipTier.Gold,
            Points = 300,
            ByUser = "integration-test"
        });

        result.IsSuccess.ShouldBeTrue();
        await repository.SaveChangesAsync(CancellationToken.None);

        // Negative assertion: the tier didn't change, so the [RaisesEvent] narrowing on nameof(Tier) means
        // the tier-changed event was never queued for this save — there's nothing to wait for. A short settle
        // delay still guards against a regression that queues it anyway but publishes it slightly late.
        await Task.Delay(TimeSpan.FromMilliseconds(200));
        fixture.LogCapture.Messages.ShouldNotContain(m =>
            m.Contains("tier changed", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task WithdrawingAMembershipPublishesTheWithdrawalEventWithTheValuesItLastHeld()
    {
        await fixture.ResetDatabaseAsync();

        using var scope = fixture.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var repository = scope.ServiceProvider.GetRequiredService<IRepositorySpec>();

        var membership = new LoyaltyMembership("Alice Nguyen", MembershipTier.Gold, 300, "seed");
        await repository.AddAsync(membership, CancellationToken.None);
        await repository.SaveChangesAsync(CancellationToken.None);
        fixture.LogCapture.Clear();

        var result = await bus.Send(new WithdrawMembershipRequest { Id = membership.Id });

        result.IsSuccess.ShouldBeTrue();
        await repository.SaveChangesAsync(CancellationToken.None);

        var stored = await repository.FirstOrDefaultAsync(
            new SpecGetLoyaltyMembership(membership.Id),
            CancellationToken.None);
        stored.ShouldBeNull();

        Func<bool> logged = () => fixture.LogCapture.Messages.Any(m =>
            m.Contains("withdrawn", StringComparison.OrdinalIgnoreCase) &&
            m.Contains("Gold", StringComparison.Ordinal) &&
            m.Contains("300", StringComparison.Ordinal));
        (await Eventually.IsTrueAsync(logged)).ShouldBeTrue();

        fixture.LogCapture.Messages.Count(m =>
                m.Contains("withdrawn", StringComparison.OrdinalIgnoreCase) &&
                m.Contains("Gold", StringComparison.Ordinal) &&
                m.Contains("300", StringComparison.Ordinal))
            .ShouldBe(1);
    }

    [Fact]
    public async Task ARejectedEnrolmentPublishesNothing()
    {
        await fixture.ResetDatabaseAsync();

        using var scope = fixture.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var repository = scope.ServiceProvider.GetRequiredService<IRepositorySpec>();

        await repository.AddAsync(
            new LoyaltyMembership("Alice Nguyen", MembershipTier.Bronze, 0, "seed"),
            CancellationToken.None);
        await repository.SaveChangesAsync(CancellationToken.None);

        // Wait for the seed's own enrolment log line(s) to land, then clear — otherwise the "publishes
        // nothing" assertion below could see leftover output from this seed rather than from the rejection.
        await Eventually.IsTrueAsync(() => fixture.LogCapture.Messages.Any(m =>
            m.Contains("enrolled", StringComparison.OrdinalIgnoreCase)));
        fixture.LogCapture.Clear();

        var result = await bus.Send(new EnrollMembershipRequest
        {
            MemberName = "Alice Nguyen",
            Tier = MembershipTier.Silver,
            Points = 0
        });

        result.IsFailed.ShouldBeTrue();

        fixture.LogCapture.Messages.ShouldNotContain(m =>
            m.Contains("enrolled", StringComparison.OrdinalIgnoreCase));
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
