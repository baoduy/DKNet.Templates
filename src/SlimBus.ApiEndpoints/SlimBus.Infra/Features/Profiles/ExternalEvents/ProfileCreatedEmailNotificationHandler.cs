using DKNet.SlimBus.Extensions;
using Microsoft.Extensions.Logging;
using SlimBus.AppServices.Profiles.V1.Events;

namespace SlimBus.Infra.Features.Profiles.ExternalEvents;

internal sealed class ProfileCreatedEmailNotificationHandler(ILogger<ProfileCreatedEmailNotificationHandler> logger)
    : Fluents.EventsConsumers.IHandler<ProfileCreatedEvent>
{
    #region Properties

    public static bool Called { get; set; }

    #endregion

    #region Methods

    public Task OnHandle(ProfileCreatedEvent notification, CancellationToken cancellationToken)
    {
        Called = notification.Id != Guid.Empty;
        logger.LogInformation("ProfileCreatedEmailNotificationHandler called with Id: {Id}", notification.Id);
        return Task.CompletedTask;
    }

    #endregion
}