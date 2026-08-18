using Minimal.AppServices.LoyaltyMemberships.V1;
using Minimal.AppServices.LoyaltyMemberships.V1.Actions;

namespace Minimal.Api.ApiEndpoints;

internal sealed class LoyaltyMembershipV1Endpoint : IEndpointConfig
{
    #region Properties

    public int Version => 1;

    public string GroupEndpoint => "/loyalty-memberships";

    #endregion

    #region Methods

    public void Map(RouteGroupBuilder group)
    {
        group.MapPost<EnrollMembershipRequest, LoyaltyMembershipDto>()
            .WithDescription("Enrol a new loyalty membership");

        group.MapPut<ChangeMembershipRequest, LoyaltyMembershipDto>()
            .WithDescription("Change a loyalty membership's tier and/or points");

        group.MapDelete<WithdrawMembershipRequest>()
            .WithDescription("Withdraw a loyalty membership");
    }

    #endregion
}
