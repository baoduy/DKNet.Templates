using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Minimal.App.TestSupport;

namespace Minimal.App.Tests.Integration.Support;

/// <summary>
/// <see cref="HealthProbeApiFixture" /> variant with <see cref="TestAuthHandler" /> registered, so a request
/// carrying any bearer token authenticates as <see cref="TestAuthHandler.CallerName" /> — the "authenticated
/// operator" half of the detailed-health-report scenario pair.
/// </summary>
public sealed class HealthProbeOperatorApiFixture : TestApiFactoryBase, IAsyncLifetime
{
    private const string RequireAuthorizationEnvKey = "FeatureManagement__RequireAuthorization";

    public HealthProbeOperatorApiFixture() => Environment.SetEnvironmentVariable(RequireAuthorizationEnvKey, "true");

    public ToggleableDbHealthCheck DbHealthCheck { get; } = new();

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        base.ConfigureTestServices(services);
        TestAuthHandler.Register(services);

        services.AddSingleton(DbHealthCheck);
        services.PostConfigure<HealthCheckServiceOptions>(o =>
        {
            var existing = o.Registrations.FirstOrDefault(r => r.Name == "CoreDbContext");
            if (existing is not null)
            {
                o.Registrations.Remove(existing);
            }

            o.Registrations.Add(new HealthCheckRegistration(
                "CoreDbContext",
                sp => sp.GetRequiredService<ToggleableDbHealthCheck>(),
                failureStatus: null,
                tags: null));
        });
    }

    public async Task InitializeAsync()
    {
        _ = CreateClient();
        await ResetDatabaseAsync();
    }

    Task IAsyncLifetime.DisposeAsync() => Task.CompletedTask;

    protected override void Dispose(bool disposing)
    {
        Environment.SetEnvironmentVariable(RequireAuthorizationEnvKey, null);
        base.Dispose(disposing);
    }
}
