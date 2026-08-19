using Microsoft.Extensions.Logging;
using Minimal.Domains.Features.LoyaltyMemberships.Entities;
using Minimal.Domains.Features.LoyaltyMemberships.Events;

namespace Minimal.AppServices.LoyaltyMemberships.V1.Events;

/// <summary>
///     Logs the enrolment of a new loyalty membership. Raised by the DKNet events hook via
///     <see cref="DKNet.EfCore.Abstractions.Events.RaisesEventAttribute" /> — no persistence, no outbound call.
/// </summary>
internal sealed class LoyaltyMembershipEnrolledEventHandler(ILogger<LoyaltyMembershipEnrolledEventHandler> logger)
    : Fluents.EventsConsumers.IHandler<LoyaltyMembershipEvents>
{
    #region Methods

    public Task OnHandle(LoyaltyMembershipEvents notification, CancellationToken cancellationToken)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Loyalty membership enrolled for member {MemberName}.", notification.MemberName);
        }

        return Task.CompletedTask;
    }

    #endregion
}

/// <summary>
///     Logs a loyalty membership's tier change. Raised by the DKNet events hook only when the tier
///     actually changed in that save.
/// </summary>
internal sealed class LoyaltyMembershipTierChangedEventHandler(
    ILogger<LoyaltyMembershipTierChangedEventHandler> logger)
    : Fluents.EventsConsumers.IHandler<LoyaltyMembershipTierChangedEvent>
{
    #region Methods

    public Task OnHandle(LoyaltyMembershipTierChangedEvent notification, CancellationToken cancellationToken)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Loyalty membership {MemberName} tier changed to {Tier}.",
                notification.MemberName,
                notification.Tier);
        }

        return Task.CompletedTask;
    }

    #endregion
}

/// <summary>
///     Logs a loyalty membership's withdrawal, carrying the last-held tier and points.
/// </summary>
internal sealed class LoyaltyMembershipWithdrawnEventHandler(ILogger<LoyaltyMembershipWithdrawnEventHandler> logger)
    : Fluents.EventsConsumers.IHandler<LoyaltyMembershipWithdrawnEvent>
{
    #region Methods

    public Task OnHandle(LoyaltyMembershipWithdrawnEvent notification, CancellationToken cancellationToken)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Loyalty membership {MemberName} withdrawn at tier {Tier} with {Points} points.",
                notification.MemberName,
                notification.Tier,
                notification.Points);
        }

        return Task.CompletedTask;
    }

    #endregion
}
