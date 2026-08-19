using DKNet.EfCore.Abstractions.Events;
using Microsoft.Extensions.Logging;
using SlimMessageBus;

namespace Minimal.Infra.Services;

/// <summary>
///     The event publisher, IMessageBus for both internal and external events.
/// </summary>
/// <param name="bus"></param>
/// <param name="logger"></param>
internal sealed class EventPublisher(IMessageBus bus, ILogger<EventPublisher> logger) : DefaultEventPublisher
{
    #region Methods

    public override async Task PublishAsync(object eventObj, CancellationToken cancellationToken = default)
    {
        await bus.Publish(eventObj, cancellationToken: cancellationToken);
        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation($"EventPublisher: {eventObj.GetType().Name}");
    }

    #endregion
}