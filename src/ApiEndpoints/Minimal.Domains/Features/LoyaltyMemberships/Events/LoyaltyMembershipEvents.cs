using DKNet.EfCore.DtoGenerator;
using Minimal.Domains.Features.LoyaltyMemberships.Entities;

namespace Minimal.Domains.Features.LoyaltyMemberships.Events;

/// <summary>
/// Declared domain event raised when a <see cref="LoyaltyMembership"/> is enrolled.
/// </summary>
[GenerateDto(typeof(LoyaltyMembership))]
public partial record LoyaltyMembershipEvents;