using Microsoft.Extensions.Configuration;
using Minimal.AppServices;
using Minimal.Infra.Extensions;
using Minimal.Share.Options;
using SlimMessageBus.Host;

namespace Minimal.App.Tests.Unit.Extensions;

public class ServiceBusSetupTests
{
    #region Methods

    [Theory]
    [InlineData(true, "Endpoint=sb://fake.servicebus.windows.net/;SharedAccessKeyName=k;SharedAccessKey=v", true)]
    [InlineData(true, "", false)]
    [InlineData(false, "Endpoint=sb://fake.servicebus.windows.net/;SharedAccessKeyName=k;SharedAccessKey=v", false)]
    [InlineData(false, "", false)]
    public void AddServiceBus_ShouldGateAzureChildBus_OnEnableServiceBusAndConnectionString(
        bool enableServiceBus,
        string azureBusConnectionString,
        bool expectAzureBus)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:AzureBus"] = azureBusConnectionString
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddServiceBus(configuration, typeof(AppSetup).Assembly,
            new FeatureOptions { EnableServiceBus = enableServiceBus });

        using var provider = services.BuildServiceProvider();
        var masterBus = provider.GetRequiredService<IMasterMessageBus>();
        var childBusNames = masterBus.Settings.Children.Select(c => c.Name).ToArray();

        childBusNames.ShouldContain("ImMemory",
            "the in-memory child bus dispatches internal CQRS handlers and must stay registered " +
            "regardless of the Azure Service Bus feature flag.");

        if (expectAzureBus)
            childBusNames.ShouldContain("AzureBus");
        else
            childBusNames.ShouldNotContain("AzureBus");
    }

    #endregion
}
