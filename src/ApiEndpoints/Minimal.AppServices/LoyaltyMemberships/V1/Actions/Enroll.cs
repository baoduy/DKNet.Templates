using DKNet.EfCore.Specifications;
using DKNet.EfCore.Specifications.Extensions;
using Minimal.AppServices.LoyaltyMemberships.V1.Specs;
using Minimal.Domains.Features.LoyaltyMemberships.Entities;

// ReSharper disable UnusedType.Global

namespace Minimal.AppServices.LoyaltyMemberships.V1.Actions;

/// <summary>
///     Command to enrol a new loyalty membership.
/// </summary>
[MapsFrom(typeof(LoyaltyMembership))]
public sealed record EnrollMembershipRequest : RequestBase, Fluents.Requests.IWitResponse<LoyaltyMembershipDto>
{
    #region Properties

    /// <summary>
    ///     Gets or sets the member's full name. Required; unique per membership.
    /// </summary>
    [Required]
    [StringLength(150, MinimumLength = 1)]
    public string MemberName { get; set; } = null!;

    /// <summary>
    ///     Gets or sets the initial membership tier.
    /// </summary>
    [Required]
    public MembershipTier Tier { get; set; }

    /// <summary>
    ///     Gets or sets the initial points balance.
    /// </summary>
    public int Points { get; set; }

    #endregion
}

/// <summary>
///     FluentValidation validator for <see cref="EnrollMembershipRequest" />.
/// </summary>
internal sealed class EnrollMembershipCommandValidator : AbstractValidator<EnrollMembershipRequest>
{
    #region Constructors

    public EnrollMembershipCommandValidator()
    {
        RuleFor(a => a.MemberName).NotEmpty().Length(1, 150);
        RuleFor(a => a.Tier).IsInEnum();
    }

    #endregion
}

/// <summary>
///     Handles <see cref="EnrollMembershipRequest" /> by validating uniqueness, mapping the command to a
///     <see cref="LoyaltyMembership" /> entity, and persisting it. Raises no event by hand — enrolment is
///     declared via <see cref="DKNet.EfCore.Abstractions.Events.RaisesEventAttribute" /> on the entity.
/// </summary>
/// <param name="repository">EF Core specification repository used for duplicate checking and persistence.</param>
/// <param name="mapper">Mapster mapper used for entity mapping and lazy result projection.</param>
internal sealed class EnrollMembershipCommandHandler(
    IRepositorySpec repository,
    IMapper mapper)
    : Fluents.Requests.IHandler<EnrollMembershipRequest, LoyaltyMembershipDto>
{
    #region Methods

    public async Task<IResult<LoyaltyMembershipDto>> OnHandle(
        EnrollMembershipRequest request,
        CancellationToken cancellationToken)
    {
        //Check duplicate
        if (await repository.AnyAsync(new SpecGetLoyaltyMembership(byMemberName: request.MemberName),
                cancellationToken: cancellationToken))
        {
            return Result.Fail<LoyaltyMembershipDto>($"Member {request.MemberName} is already enrolled.");
        }

        var membership = mapper.Map<LoyaltyMembership>(request);

        //Add
        await repository.AddAsync(membership, cancellationToken);

        //NOTE this will return a lazy mapping result and only map membership to LoyaltyMembershipDto after SaveChanges is called.
        return mapper.ResultOf<LoyaltyMembershipDto>(membership);
    }

    #endregion
}
