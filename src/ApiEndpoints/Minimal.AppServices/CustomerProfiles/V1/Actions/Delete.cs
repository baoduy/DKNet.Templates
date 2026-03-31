using DKNet.EfCore.Specifications;
using DKNet.EfCore.Specifications.Extensions;
using Minimal.AppServices.CustomerProfiles.V1.Specs;

namespace Minimal.AppServices.CustomerProfiles.V1.Actions;

/// <summary>
///     Command to delete an existing customer profile by its unique identifier.
/// </summary>
public record DeleteProfileRequest : RequestBase, Fluents.Requests.INoResponse
{
    #region Properties

    /// <summary>
    ///     Gets the unique identifier of the customer profile to delete.
    /// </summary>
    public required Guid Id { get; init; }

    #endregion
}

/// <summary>
///     Handles <see cref="DeleteProfileRequest" /> by locating the profile and removing it from the repository.
/// </summary>
/// <param name="repository">EF Core specification repository used to look up and delete the profile.</param>
internal sealed class DeleteProfileCommandHandler(IRepositorySpec repository)
    : Fluents.Requests.IHandler<DeleteProfileRequest>
{
    #region Methods

    /// <summary>
    ///     Processes the delete-profile command.
    /// </summary>
    /// <remarks>
    ///     <list type="number">
    ///         <item>Returns a failure result when <see cref="DeleteProfileRequest.Id" /> is <see cref="Guid.Empty" />.</item>
    ///         <item>Returns a failure result when no profile with the given ID is found.</item>
    ///         <item>Marks the located profile for deletion and returns a success result.</item>
    ///     </list>
    /// </remarks>
    /// <param name="request">The incoming delete-profile command.</param>
    /// <param name="cancellationToken">Token to observe for cancellation requests.</param>
    /// <returns>
    ///     <see cref="Result.Ok()" /> on success, or a failed <see cref="IResultBase" /> describing the reason for failure.
    /// </returns>
    public async Task<IResultBase> OnHandle(DeleteProfileRequest request, CancellationToken cancellationToken)
    {
        if (request.Id == Guid.Empty)
        {
            return Result.Fail("The Id is in valid.")
                .WithError(new Error("The Id is in valid.") { Metadata = { ["field"] = nameof(request.Id) } });
        }

        var profile = await repository.FirstOrDefaultAsync(new SpecGetCustomerProfile(request.Id), cancellationToken);

        if (profile == null)
        {
            return Result.Fail($"The Profile {request.Id} is not found.");
        }

        repository.Delete(profile);

        return Result.Ok();
    }

    #endregion
}