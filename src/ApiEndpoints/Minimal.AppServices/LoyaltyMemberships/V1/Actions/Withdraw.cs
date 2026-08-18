using DKNet.EfCore.Specifications;
using DKNet.EfCore.Specifications.Extensions;
using Minimal.AppServices.LoyaltyMemberships.V1.Specs;

namespace Minimal.AppServices.LoyaltyMemberships.V1.Actions;

/// <summary>
///     Command to withdraw an existing loyalty membership by its unique identifier. This is a hard delete;
///     no history is retained.
/// </summary>
public record WithdrawMembershipRequest : RequestBase, Fluents.Requests.INoResponse
{
    #region Properties

    /// <summary>
    ///     Gets the unique identifier of the membership to withdraw.
    /// </summary>
    public required Guid Id { get; init; }

    #endregion
}

/// <summary>
///     Handles <see cref="WithdrawMembershipRequest" /> by locating the membership and removing it from the repository.
/// </summary>
/// <param name="repository">EF Core specification repository used to look up and delete the membership.</param>
internal sealed class WithdrawMembershipCommandHandler(IRepositorySpec repository)
    : Fluents.Requests.IHandler<WithdrawMembershipRequest>
{
    #region Methods

    public async Task<IResultBase> OnHandle(WithdrawMembershipRequest request, CancellationToken cancellationToken)
    {
        if (request.Id == Guid.Empty)
        {
            return Result.Fail("The Id is in valid.")
                .WithError(new Error("The Id is in valid.") { Metadata = { ["field"] = nameof(request.Id) } });
        }

        var membership =
            await repository.FirstOrDefaultAsync(new SpecGetLoyaltyMembership(request.Id), cancellationToken);

        if (membership == null)
        {
            return Result.Fail($"The Membership {request.Id} is not found.");
        }

        repository.Delete(membership);

        return Result.Ok();
    }

    #endregion
}
