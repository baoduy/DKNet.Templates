using Minimal.Domains.Features.Profiles.Entities;

namespace Minimal.AppServices.CustomerProfiles.V1.Events;

/// <summary>
///     NOTE: remove this as just for testing purposed only
/// </summary>
internal sealed class ProfileCreatedEventFromMemoryHandler : Fluents.EventsConsumers.IHandler<CustomerProfileCreatedEvent>
{
    #region Properties

    public static bool Called { get; set; }

    #endregion

    #region Methods

    public Task OnHandle(CustomerProfileCreatedEvent notification, CancellationToken cancellationToken)
    {
        Called = notification.Id != Guid.Empty;
        return Task.CompletedTask;
    }

    #endregion
}