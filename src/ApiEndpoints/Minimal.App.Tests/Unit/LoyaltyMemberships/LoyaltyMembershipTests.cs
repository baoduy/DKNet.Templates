using DKNet.EfCore.Abstractions.Events;
using Minimal.AppServices.LoyaltyMemberships.V1.Actions;
using Minimal.Domains.Features.LoyaltyMemberships.Entities;
using Minimal.Domains.Features.LoyaltyMemberships.Events;

namespace Minimal.App.Tests.Unit.LoyaltyMemberships;

public class LoyaltyMembershipTests
{
    #region Methods

    [Fact]
    public void Constructor_ShouldSetMemberNameTierAndPoints()
    {
        var membership = new LoyaltyMembership("Alice Nguyen", MembershipTier.Silver, 0, "seed");

        membership.MemberName.ShouldBe("Alice Nguyen");
        membership.Tier.ShouldBe(MembershipTier.Silver);
        membership.Points.ShouldBe(0);
    }

    [Fact]
    public void ChangeTier_ShouldUpdateTier_AndLeavePointsUnchanged()
    {
        var membership = new LoyaltyMembership("Alice Nguyen", MembershipTier.Silver, 120, "seed");

        membership.ChangeTier(MembershipTier.Gold, "updater");

        membership.Tier.ShouldBe(MembershipTier.Gold);
        membership.Points.ShouldBe(120);
    }

    [Fact]
    public void ChangePoints_ShouldUpdatePoints_AndLeaveTierUnchanged()
    {
        var membership = new LoyaltyMembership("Alice Nguyen", MembershipTier.Gold, 120, "seed");

        membership.ChangePoints(300, "updater");

        membership.Points.ShouldBe(300);
        membership.Tier.ShouldBe(MembershipTier.Gold);
    }

    [Fact]
    public void Validator_ShouldRejectUnknownTier()
    {
        var validator = new EnrollMembershipCommandValidator();

        var result = validator.Validate(new EnrollMembershipRequest
        {
            MemberName = "Alice Nguyen",
            Tier = (MembershipTier)99,
            Points = 0
        });

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(EnrollMembershipRequest.Tier));
    }

    [Theory]
    [InlineData(MembershipTier.Bronze)]
    [InlineData(MembershipTier.Silver)]
    [InlineData(MembershipTier.Gold)]
    public void Validator_ShouldAcceptEachOfTheThreeKnownTiers(MembershipTier tier)
    {
        var validator = new EnrollMembershipCommandValidator();

        var result = validator.Validate(new EnrollMembershipRequest
        {
            MemberName = "Alice Nguyen",
            Tier = tier,
            Points = 0
        });

        result.IsValid.ShouldBeTrue();
    }

    #endregion
}
