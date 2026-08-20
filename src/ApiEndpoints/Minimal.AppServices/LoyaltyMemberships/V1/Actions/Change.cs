using DKNet.EfCore.Specifications;
using DKNet.EfCore.Specifications.Extensions;
using Minimal.AppServices.LoyaltyMemberships.V1.Specs;
using Minimal.Domains.Features.LoyaltyMemberships.Entities;

namespace Minimal.AppServices.LoyaltyMemberships.V1.Actions;

/// <summary>
///     Command that changes the tier and/or points balance of an existing loyalty membership.
///     Properties left <see langword="null" /> are left unchanged.
/// </summary>
[MapsFrom(typeof(LoyaltyMembership))]
public record ChangeMembershipRequest : Fluents.Requests.IWitResponse<LoyaltyMembershipDto>
{
    #region Properties

    /// <summary>
    ///     Gets or sets the identity of the acting user. Populated from the caller's claims — never settable by
    ///     the caller — via <see cref="FromClaimAttribute" />.
    /// </summary>
    [FromClaim(ClaimTypes.Name)]
    public string? ByUser { get; set; }

    /// <summary>
    ///     Gets the unique identifier of the membership to change.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    ///     Gets the new tier. When <see langword="null" />, the current tier is preserved.
    /// </summary>
    public MembershipTier? Tier { get; init; }

    /// <summary>
    ///     Gets the new points balance. When <see langword="null" />, the current balance is preserved.
    /// </summary>
    public int? Points { get; init; }

    #endregion
}

/// <summary>
///     FluentValidation validator for <see cref="ChangeMembershipRequest" />.
/// </summary>
internal sealed class ChangeMembershipCommandValidator : AbstractValidator<ChangeMembershipRequest>
{
    #region Constructors

    public ChangeMembershipCommandValidator()
    {
        RuleFor(a => a.Tier).IsInEnum().When(a => a.Tier is not null);
    }

    #endregion
}

internal sealed class ChangeMembershipCommandHandler(
    IMapper mapper,
    IRepositorySpec repo) : Fluents.Requests.IHandler<ChangeMembershipRequest, LoyaltyMembershipDto>
{
    #region Methods

    public async Task<IResult<LoyaltyMembershipDto>> OnHandle(
        ChangeMembershipRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Id == Guid.Empty)
        {
            return Result.Fail<LoyaltyMembershipDto>("The Id is in valid.");
        }

        var membership = await repo.FirstOrDefaultAsync(new SpecGetLoyaltyMembership(request.Id), cancellationToken);

        if (membership == null)
        {
            return Result.Fail<LoyaltyMembershipDto>($"The Membership {request.Id} is not found.");
        }

        //Only raises the tier-changed event when the tier actually differs.
        if (request.Tier is not null && request.Tier != membership.Tier)
        {
            membership.ChangeTier(request.Tier.Value, request.ByUser!);
        }

        if (request.Points is not null)
        {
            membership.ChangePoints(request.Points.Value, request.ByUser!);
        }

        //Return result
        return Result.Ok(mapper.Map<LoyaltyMembershipDto>(membership));
    }

    #endregion
}
