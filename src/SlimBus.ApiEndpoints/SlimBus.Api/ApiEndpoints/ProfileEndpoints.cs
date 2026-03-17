using SlimBus.Api.Configs.Idempotency;
using SlimBus.AppServices.Profiles.V1;
using SlimBus.AppServices.Profiles.V1.Actions;
using SlimBus.Domains.Features.Profiles.Entities;

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

        group.MapPost<CreateProfileCommand, CustomerProfileDto>("")
            .AddIdempotencyFilter()
            .WithDescription(
                "Create profile. <br/><br/> Note: Idempotency key is required in the header. <br/>" +
                "X-Idempotency-Key: {IdempotencyKey} <br/>");

        group.MapPut<UpdateProfileCommand, CustomerProfileDto>("{id:guid}")
            .WithDescription("Update profile by id");

        group.MapDelete<DeleteProfileCommand>("{id:guid}")
            .WithDescription("Delete profile by id");
    }

    #endregion
}