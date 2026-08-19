using DKNet.EfCore.DtoGenerator;
using Minimal.Domains.Features.LoyaltyMemberships.Entities;

namespace Minimal.AppServices.LoyaltyMemberships.V1;

[GenerateDto(typeof(LoyaltyMembership), Exclude = [])]
[MapsFrom(typeof(LoyaltyMembership))]
public sealed partial record LoyaltyMembershipDto;
