using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Minimal.Domains.Services;
using Minimal.Infra.Contexts;

namespace Minimal.App.Tests.Integration.Support;

public sealed class ApiFixture : WebApplicationFactory<Minimal.Api.Program>, IAsyncLifetime
{
    #region Fields

    private readonly string _dbName = $"slimbus-integration-{Guid.NewGuid():N}";

    #endregion

    #region Methods

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FeatureManagement:RunDbMigrationWhenAppStart"] = "false",
                ["FeatureManagement:EnableSwagger"] = "false",
                ["FeatureManagement:EnableAzureAppConfig"] = "false",
                ["ConnectionStrings:AppDb"] = "UseInMemory"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDbContextOptionsConfiguration<CoreDbContext>>();
            services.RemoveAll<IConfigureOptions<DbContextOptions<CoreDbContext>>>();
            services.RemoveAll<IPostConfigureOptions<DbContextOptions<CoreDbContext>>>();
            services.RemoveAll<DbContextOptions<CoreDbContext>>();
            services.RemoveAll<CoreDbContext>();
            services.AddDbContext<CoreDbContext>(options => options
                .UseInMemoryDatabase(_dbName)
                .UseAutoConfigModel([typeof(CoreDbContext).Assembly]));

            services.RemoveAll<IMembershipService>();
            services.AddSingleton<IMembershipService, TestMembershipService>();
        });
    }

    public IServiceScope CreateScope() => Services.CreateScope();

    public async Task ResetDatabaseAsync()
    {
        using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();
    }

    public async Task InitializeAsync()
    {
        _ = CreateClient();
        await ResetDatabaseAsync();
    }

    Task IAsyncLifetime.DisposeAsync() => Task.CompletedTask;

    #endregion
}


