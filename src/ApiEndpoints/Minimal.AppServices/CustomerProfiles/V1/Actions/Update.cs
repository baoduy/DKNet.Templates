using DKNet.EfCore.Specifications;
using DKNet.EfCore.Specifications.Extensions;
using Minimal.AppServices.CustomerProfiles.V1.Specs;

namespace Minimal.AppServices.CustomerProfiles.V1.Actions;

/// <summary>
/// Command that updates editable fields for an existing customer profile.
/// </summary>
[MapsFrom(typeof(CustomerProfile))]
public record UpdateProfileRequest : RequestBase, Fluents.Requests.IWitResponse<CustomerProfileDto>
{
    #region Properties

    /// <summary>
    /// Gets the unique identifier of the profile to update.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Gets the email value to update. When <see langword="null"/>, the current value is preserved.
    /// </summary>
    public string? Email { get; init; }

    /// <summary>
    /// Gets the display name to update. When <see langword="null"/>, the current value is preserved.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Gets the phone value to update. When <see langword="null"/>, the current value is preserved.
    /// </summary>
    public string? Phone { get; init; }

    #endregion
}

internal sealed class UpdateProfileCommandHandler(
    IMapper mapper,
    IRepositorySpec repo) : Fluents.Requests.IHandler<UpdateProfileRequest, CustomerProfileDto>
{
    #region Methods

    public async Task<IResult<CustomerProfileDto>> OnHandle(
        UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Id == Guid.Empty)
        {
            return Result.Fail<CustomerProfileDto>("The Id is in valid.");
        }

        var profile = await repo.FirstOrDefaultAsync(new SpecGetCustomerProfile(request.Id), cancellationToken);

        if (profile == null)
        {
            return Result.Fail<CustomerProfileDto>($"The Profile {request.Id} is not found.");
        }

        //Update Here
        profile.Update(null, request.Name, request.Phone, null, request.ByUser!);

        //Add Event

        //Return result
        return Result.Ok(mapper.Map<CustomerProfileDto>(profile));
    }

    #endregion
}