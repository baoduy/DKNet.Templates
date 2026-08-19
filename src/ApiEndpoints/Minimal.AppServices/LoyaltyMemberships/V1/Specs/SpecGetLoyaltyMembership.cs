using DKNet.EfCore.Specifications;
using DKNet.EfCore.Specifications.Extensions;
using Minimal.Domains.Features.LoyaltyMemberships.Entities;

namespace Minimal.AppServices.LoyaltyMemberships.V1.Specs;

internal sealed class SpecGetLoyaltyMembership : Specification<LoyaltyMembership>
{
    public SpecGetLoyaltyMembership(Guid? byId = null, string? byMemberName = null)
    {
        var predicator = CreatePredicate();

        if (byId is not null)
        {
            predicator = predicator.And(a => a.Id == byId);
        }

        if (!string.IsNullOrEmpty(byMemberName))
        {
            predicator = predicator.And(a => a.MemberName == byMemberName);
        }

        WithFilter(predicator);
    }
}
