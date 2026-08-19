using DKNet.AspCore.Idempotency;
using DKNet.AspCore.Idempotency.RedisStore;
using DKNet.AspCore.Idempotency.Store;
using DKNet.EfCore.Hooks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minimal.Domains.Services;

namespace Minimal.App.BDDTests.Support;

public sealed class BddApiFactory(string? redisConnectionString = null) : WebApplicationFactory<Minimal.Api.Program>
{
    private readonly string _dbName = "bdd-tests";

    /// <summary>Captures log lines written by the app during a scenario, for asserting on log output.</summary>
    public TestLogCapture LogCapture { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureLogging(logging => logging.AddProvider(LogCapture));

        builder.ConfigureAppConfiguration((_, config) =>
        {
            var settings = new Dictionary<string, string?>
            {
                ["FeatureManagement:RunDbMigrationWhenAppStart"] = "false",
                ["FeatureManagement:EnableSwagger"] = "false",
                ["FeatureManagement:EnableAzureAppConfig"] = "false",
                ["FeatureManagement:RequireAuthorization"] = "false",
                ["ConnectionStrings:AppDb"] = "UseInMemory"
            };

            // Only the @redis scenario passes this. Setting it alone isn't enough to flip AppConfig.AddAppConfig's
            // redis-vs-fallback branch — WebApplicationFactory merges this config in after Program.cs's own
            // startup code already read it, so the ConfigureServices override below does the actual swap. Kept
            // here too so anything else that reads ConnectionStrings:Redis at runtime (rather than at startup)
            // sees the real value.
            if (!string.IsNullOrWhiteSpace(redisConnectionString))
            {
                settings["ConnectionStrings:Redis"] = redisConnectionString;
            }

            config.AddInMemoryCollection(settings);
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDbContextOptionsConfiguration<CoreDbContext>>();
            services.RemoveAll<IConfigureOptions<DbContextOptions<CoreDbContext>>>();
            services.RemoveAll<IPostConfigureOptions<DbContextOptions<CoreDbContext>>>();
            services.RemoveAll<DbContextOptions<CoreDbContext>>();
            services.RemoveAll<CoreDbContext>();

            // AddDbContext (rather than AddDbContextWithHook) here would silently drop the DKNet events hook —
            // AddEvent-raised and [RaisesEvent]-declared domain events would never publish under this fixture.
            services.AddDbContextWithHook<CoreDbContext>((_, options) => options
                .UseInMemoryDatabase(_dbName)
                .UseAutoConfigModel([typeof(CoreDbContext).Assembly]));

            services.RemoveAll<IMembershipService>();
            services.AddSingleton<IMembershipService, TestMembershipService>();

            if (!string.IsNullOrWhiteSpace(redisConnectionString))
            {
                // Program.cs's own AddAppConfig already chose the in-memory idempotency fallback (it ran before
                // the config above was merged in), so replace that choice directly instead.
                services.RemoveAll<IIdempotencyKeyStore>();
                services.RemoveAll<IOptions<IdempotencyOptions>>();
                services.AddIdempotencyWithRedisStore(
                    redisConnectionString,
                    o => o.ConflictHandling = IdempotentConflictHandling.CachedResult);
            }
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
}
