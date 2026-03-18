using Minimal.Api.Configs.Idempotency;
using Minimal.AppServices.CustomerProfiles.V1.Actions;
using Minimal.Domains.Features.Profiles.Entities;
using CustomerProfileDto = Minimal.AppServices.CustomerProfiles.V1.CustomerProfileDto;

namespace Minimal.Api.ApiEndpoints;

internal sealed class CustomerProfileV1Endpoint : IEndpointConfig
{
    #region Properties

    public int Version => 1;

    public string GroupEndpoint => "/customer-profiles";

    #endregion

    #region Methods

    public void Map(RouteGroupBuilder group)
    {
        group.MapGetList<CustomerProfile, CustomerProfileDto>()
            .WithDescription("Get all profiles");
        group.MapGetById<CustomerProfile, CustomerProfileDto>()
            .WithDescription("Get profile by id");

        group.MapPost<CreateProfileRequest, CustomerProfileDto>()
            .AddIdempotencyFilter()
            .WithDescription(
                "Create profile. <br/><br/> Note: Idempotency key is required in the header. <br/>" +
                "X-Idempotency-Key: {IdempotencyKey} <br/>");

        group.MapPut<UpdateProfileRequest, CustomerProfileDto>()
            .WithDescription("Update profile by id");

        group.MapDelete<DeleteProfileRequest>()
            .WithDescription("Delete profile by id");
    }

    #endregion
}