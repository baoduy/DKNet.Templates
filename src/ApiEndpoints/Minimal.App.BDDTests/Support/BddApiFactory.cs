using DKNet.AspCore.Idempotency;
using DKNet.AspCore.Idempotency.RedisStore;
using DKNet.AspCore.Idempotency.Store;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Minimal.App.TestSupport;

namespace Minimal.App.BDDTests.Support;

public sealed class BddApiFactory(string? redisConnectionString = null) : TestApiFactoryBase("bdd-tests")
{
    protected override void AddFeatureOverrides(IDictionary<string, string?> settings)
    {
        settings["FeatureManagement:RequireAuthorization"] = "false";

        // Only the @redis scenario passes this. Setting it alone isn't enough to flip AppConfig.AddAppConfig's
        // redis-vs-fallback branch — WebApplicationFactory merges this config in after Program.cs's own
        // startup code already read it, so the ConfigureTestServices override below does the actual swap. Kept
        // here too so anything else that reads ConnectionStrings:Redis at runtime (rather than at startup)
        // sees the real value.
        if (!string.IsNullOrWhiteSpace(redisConnectionString))
        {
            settings["ConnectionStrings:Redis"] = redisConnectionString;
        }
    }

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        base.ConfigureTestServices(services);

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
    }
}
