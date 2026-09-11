using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore.Design;
using Minimal.Infra.Extensions;

namespace Minimal.Infra.Contexts;

[ExcludeFromCodeCoverage]
internal sealed class DbContextFactory : IDesignTimeDbContextFactory<CoreDbContext>
{
    #region Methods

    public CoreDbContext CreateDbContext(string[] args) => BuildServiceProvider(args).GetRequiredService<CoreDbContext>();

    /// <summary>
    /// Builds the same design-time service provider <see cref="CreateDbContext"/> resolves
    /// <see cref="CoreDbContext"/> from. <c>UseAutoDataSeeding</c>'s post-migration hook publishes
    /// domain events through <see cref="Minimal.Infra.Services.EventPublisher"/>, which needs an
    /// <c>IMessageBus</c> to activate — without one, <c>dotnet ef database update</c> throws while
    /// resolving the seeding hook. Registers an in-memory-only bus (mirrors <c>ServiceBusSetup</c>'s
    /// <c>AddMemoryBus</c> shape, minus the Azure child bus and minus <c>AddSlimBusEfCoreInterceptor</c>)
    /// so nothing is ever published to a real broker at design time.
    /// </summary>
    internal static IServiceProvider BuildServiceProvider(string[] args)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["ConnectionStrings:AppDb"] =
                        "Host=localhost;Username=postgres;Password=postgres;Database=SampleDb"
                })
            .Build();

        return new ServiceCollection()
            .AddSingleton<IConfiguration>(config)
            .AddInfraServices()
            .AddSlimMessageBus(mbb => mbb.AddJsonSerializer()
                .AddChildBus(
                    "ImMemory",
                    me => me.WithProviderMemory(cf =>
                        {
                            cf.EnableMessageHeaders = false;
                            cf.EnableMessageSerialization = false;
                            cf.EnableBlockingPublish = false;
                        })
                        .AutoDeclareFrom(typeof(InfraSetup).Assembly)
                        .AddServicesFromAssembly(typeof(InfraSetup).Assembly)))
            .AddLogging()
            .BuildServiceProvider();
    }

    #endregion
}