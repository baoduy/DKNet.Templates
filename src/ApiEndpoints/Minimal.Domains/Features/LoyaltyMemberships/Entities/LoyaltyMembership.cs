using DKNet.EfCore.Abstractions.Events;
using Minimal.Domains.Share;

namespace Minimal.Domains.Features.LoyaltyMemberships.Entities;

/// <summary>
/// The membership tier held by a <see cref="LoyaltyMembership"/>.
/// </summary>
public enum MembershipTier
{
    Bronze,
    Silver,
    Gold
}

/// <summary>
/// Represents a customer's loyalty membership aggregate root, encapsulating member identity, tier, and points.
/// </summary>
/// <remarks>
/// Enrolment, tier changes, and withdrawal are declared via <see cref="RaisesEventAttribute"/> and raised
/// automatically by the DKNet events hook on save — no method here calls <c>AddEvent</c>.
/// </remarks>
[RaisesEvent(EventOperations.Created)]
[RaisesEvent(EventOperations.Updated, nameof(Tier))]
[RaisesEvent(EventOperations.Deleted)]
public class LoyaltyMembership : AggregateRoot
{
    #region Constructors

    /// <summary>
    /// Enrols a new <see cref="LoyaltyMembership"/> with a system-assigned identity.
    /// </summary>
    /// <param name="memberName">The member's full name. Unique per membership.</param>
    /// <param name="tier">The initial membership tier.</param>
    /// <param name="points">The initial points balance.</param>
    /// <param name="byUser">The identifier of the user enrolling the member.</param>
    public LoyaltyMembership(string memberName, MembershipTier tier, int points, string byUser)
        : base(byUser)
    {
        MemberName = memberName;
        Tier = tier;
        Points = points;
    }

    /// <inheritdoc />
    protected LoyaltyMembership()
    {
    }

    #endregion

    #region Properties

    /// <summary>Gets the member's full name.</summary>
    public string MemberName { get; private set; } = null!;

    /// <summary>Gets the member's current tier.</summary>
    public MembershipTier Tier { get; private set; }

    /// <summary>Gets the member's current points balance.</summary>
    public int Points { get; private set; }

    #endregion

    #region Methods

    /// <summary>
    /// Changes the member's tier.
    /// </summary>
    /// <param name="tier">The new tier.</param>
    /// <param name="userId">The identifier of the user performing the change.</param>
    public void ChangeTier(MembershipTier tier, string userId)
    {
        Tier = tier;
        SetUpdatedBy(userId);
    }

    /// <summary>
    /// Changes the member's points balance.
    /// </summary>
    /// <param name="points">The new points balance.</param>
    /// <param name="userId">The identifier of the user performing the change.</param>
    public void ChangePoints(int points, string userId)
    {
        Points = points;
        SetUpdatedBy(userId);
    }

    #endregion
}
