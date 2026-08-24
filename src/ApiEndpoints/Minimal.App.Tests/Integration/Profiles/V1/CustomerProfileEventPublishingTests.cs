using DKNet.EfCore.Specifications.Extensions;
using DKNet.EfCore.Specifications.Repositories;
using Minimal.App.TestSupport;
using Minimal.App.Tests.Integration.Support;
using Minimal.AppServices.CustomerProfiles.V1.Actions;
using Minimal.AppServices.CustomerProfiles.V1.Events;
using Minimal.Domains.Features.Profiles.Entities;
using SlimMessageBus;

namespace Minimal.App.Tests.Integration.Profiles.V1;

/// <summary>
/// Regression guard for DRK-455's invariant: the <see cref="CustomerProfileCreatedEvent"/> — now declared via
/// <see cref="DKNet.EfCore.Abstractions.Events.RaisesEventAttribute"/> on the entity — must keep reaching its
/// in-process subscriber unchanged. The event's other destination — the external Azure Service Bus topic —
/// needs a real broker to observe and is covered separately, at the source level, by
/// <c>ServiceBusExternalTopicWiringTests</c>.
/// </summary>
public sealed class CustomerProfileEventPublishingTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    [Fact]
    public async Task CreatingAProfilePublishesTheEventToItsInProcessSubscriber()
    {
        await fixture.ResetDatabaseAsync();
        ProfileCreatedEventFromMemoryHandler.Called = false;

        using var scope = fixture.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var repository = scope.ServiceProvider.GetRequiredService<IRepositorySpec>();

        var result = await bus.Send(new CreateProfileRequest
        {
            Email = "bao.duy.events@example.com",
            Name = "Bao Duy",
            Phone = "+6598887766",
            ByUser = "integration-test"
        });

        result.IsSuccess.ShouldBeTrue();
        await repository.SaveChangesAsync(CancellationToken.None);

        (await Eventually.IsTrueAsync(() => ProfileCreatedEventFromMemoryHandler.Called)).ShouldBeTrue();
    }
}
