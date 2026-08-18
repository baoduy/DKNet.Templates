using DKNet.EfCore.DtoGenerator;
using Minimal.Domains.Features.LoyaltyMemberships.Entities;

namespace Minimal.Domains.Features.LoyaltyMemberships.Events;

/// <summary>
/// Declared domain event raised when a <see cref="LoyaltyMembership"/> is withdrawn.
/// Carries the last-held tier and points, since a deleted entity's in-memory values are
/// untouched by the database delete.
/// </summary>
[GenerateDto(typeof(LoyaltyMembership))]
public partial record LoyaltyMembershipWithdrawnEvent;
