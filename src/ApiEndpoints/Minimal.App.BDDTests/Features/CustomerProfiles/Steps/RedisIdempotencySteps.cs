using DKNet.AspCore.Idempotency;
using Reqnroll.BoDi;
using StackExchange.Redis;
using Testcontainers.Redis;

namespace Minimal.App.BDDTests.Features.CustomerProfiles.Steps;

/// <summary>
/// Backs the <c>@redis</c> idempotency scenario with a real Testcontainers-hosted Redis instance. Swaps the
/// shared <see cref="BddApiFactory"/>/<see cref="HttpClient"/> registrations for ones wired to that container so
/// the reused <see cref="IdempotencySteps"/> steps run against a Redis-backed idempotency store instead of the
/// default in-memory fallback (see AppConfig.AddAppConfig).
/// </summary>
[Binding]
public sealed class RedisIdempotencySteps(IObjectContainer objectContainer)
{
    // The prefix DKNet.AspCore.Idempotency.Store applies to every cache key it writes; the rest of the key is a
    // hash of request-specific data we can't reproduce here, so "a key under this prefix exists" is the
    // observable proxy for "Redis holds an entry for that idempotency key".
    private static readonly string CacheKeyPrefix = new IdempotencyOptions().CachePrefix;

    private RedisContainer? _redisContainer;
    private BddApiFactory? _redisFactory;
    private IConnectionMultiplexer? _multiplexer;

    [Given("the service is running with a Redis connection configured")]
    public async Task GivenTheServiceIsRunningWithARedisConnectionConfigured()
    {
        // Both the parameterless RedisBuilder() and the RedisImage constant are obsolete in this Testcontainers
        // version; pin the image explicitly instead.
        _redisContainer = new RedisBuilder("redis:7.0").Build();
        await _redisContainer.StartAsync();

        var connectionString = _redisContainer.GetConnectionString();
        _multiplexer = await ConnectionMultiplexer.ConnectAsync(connectionString);

        _redisFactory = new BddApiFactory(connectionString);
        var client = _redisFactory.CreateClient();
        await _redisFactory.ResetDatabaseAsync();
        _redisFactory.LogCapture.Clear();

        // Overrides the ApiHooks-registered shared instances; safe because this Given step runs before any
        // other step class in this scenario resolves HttpClient/BddApiFactory from the container.
        objectContainer.RegisterInstanceAs(client);
        objectContainer.RegisterInstanceAs(_redisFactory);
    }

    [Then("the configured Redis instance holds an entry for that idempotency key")]
    public void ThenTheConfiguredRedisInstanceHoldsAnEntryForThatIdempotencyKey()
    {
        _multiplexer.ShouldNotBeNull();

        var server = _multiplexer.GetServer(_multiplexer.GetEndPoints().Single());
        var keys = server.Keys(pattern: $"{CacheKeyPrefix}*").ToList();

        keys.ShouldHaveSingleItem();
    }

    [AfterScenario("redis")]
    public async Task AfterScenarioAsync()
    {
        _multiplexer?.Dispose();

        if (_redisFactory is not null)
        {
            await _redisFactory.DisposeAsync();
        }

        if (_redisContainer is not null)
        {
            await _redisContainer.DisposeAsync();
        }
    }
}
