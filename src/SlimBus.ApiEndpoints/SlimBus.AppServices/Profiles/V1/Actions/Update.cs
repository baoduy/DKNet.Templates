using System.ComponentModel;
using DKNet.EfCore.Specifications;
using DKNet.EfCore.Specifications.Extensions;
using SlimBus.AppServices.Profiles.V1.Specs;

namespace SlimBus.AppServices.Profiles.V1.Actions;

/// <summary>
/// Command that updates editable fields for an existing customer profile.
/// </summary>
[MapsTo(typeof(CustomerProfile))]
public record UpdateProfileCommand : BaseCommand, Fluents.Requests.IWitResponse<CustomerProfileDto>
{
    #region Properties

    /// <summary>
    /// Gets the unique identifier of the profile to update.
    /// </summary>
    [Description("The unique identifier of the profile to update.")]
    public required Guid Id { get; init; }

    /// <summary>
    /// Gets the email value to update. When <see langword="null"/>, the current value is preserved.
    /// </summary>
    [Description("The email value to update. Leave null to keep the current value.")]
    public string? Email { get; init; }

    /// <summary>
    /// Gets the display name to update. When <see langword="null"/>, the current value is preserved.
    /// </summary>
    [Description("The display name to update. Leave null to keep the current value.")]
    public string? Name { get; init; }

    /// <summary>
    /// Gets the phone value to update. When <see langword="null"/>, the current value is preserved.
    /// </summary>
    [Description("The phone value to update. Leave null to keep the current value.")]
    public string? Phone { get; init; }

    #endregion
}

internal sealed class UpdateProfileCommandHandler(
    IMapper mapper,
    IRepositorySpec repo) : Fluents.Requests.IHandler<UpdateProfileCommand, CustomerProfileDto>
{
    #region Methods

    public async Task<IResult<CustomerProfileDto>> OnHandle(
        UpdateProfileCommand request,
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