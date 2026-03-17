using SlimBus.Api.Configs.Idempotency;
using SlimBus.AppServices.CustomerProfiles.V1.Actions;
using SlimBus.Domains.Features.Profiles.Entities;
using CustomerProfileDto = SlimBus.AppServices.CustomerProfiles.V1.CustomerProfileDto;

namespace SlimBus.Api.ApiEndpoints;

internal sealed class ProfileV1Endpoint : IEndpointConfig
{
    #region Properties

    public int Version => 1;

    public string GroupEndpoint => "/profiles";

    #endregion

    #region Methods

    public void Map(RouteGroupBuilder group)
    {
        group.MapGetList<CustomerProfile,CustomerProfileDto>("")
            .WithDescription("Get all profiles");
        group.MapGetById<CustomerProfile,CustomerProfileDto>("{id:guid}")
            .WithDescription("Get profile by id");

        group.MapPost<CreateProfileRequest, CustomerProfileDto>("")
            .AddIdempotencyFilter()
            .WithDescription(
                "Create profile. <br/><br/> Note: Idempotency key is required in the header. <br/>" +
                "X-Idempotency-Key: {IdempotencyKey} <br/>");

        group.MapPut<UpdateProfileRequest, CustomerProfileDto>("{id:guid}")
            .WithDescription("Update profile by id");

        group.MapDelete<DeleteProfileRequest>("{id:guid}")
            .WithDescription("Delete profile by id");
    }

    #endregion
}