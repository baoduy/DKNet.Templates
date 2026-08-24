using DKNet.SlimBus.Extensions;
using Microsoft.Extensions.Logging;
using Minimal.Domains.Features.Profiles.Entities;

namespace Minimal.Infra.Features.Profiles.ExternalEvents;

internal sealed class CustomerProfileCreatedEmailNotificationHandler(
    ILogger<CustomerProfileCreatedEmailNotificationHandler> logger)
    : Fluents.EventsConsumers.IHandler<CustomerProfileCreatedEvent>
{
    #region Properties

    public static bool Called { get; set; }

    #endregion

    #region Methods

    public Task OnHandle(CustomerProfileCreatedEvent notification, CancellationToken cancellationToken)
    {
        Called = notification.Id != Guid.Empty;
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("ProfileCreatedEmailNotificationHandler called with Id: {Id}", notification.Id);
        }

        return Task.CompletedTask;
    }

    #endregion
}