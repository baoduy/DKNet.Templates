using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Minimal.App.TestSupport;

namespace Minimal.App.Tests.Integration.Support;

/// <summary>
/// <see cref="ApiFixture" /> variant with <c>RequireAuthorization</c> on (so <c>/healthz/detail</c>'s
/// <c>.RequireAuthorization()</c> is actually enforced) and the real JWT bearer scheme left in place — an
/// unauthenticated request genuinely fails to authenticate, unlike <see cref="HealthProbeOperatorApiFixture" />
/// where <c>TestAuthHandler</c> would authenticate every request regardless of headers. Also swaps in a
/// <see cref="ToggleableDbHealthCheck" /> so a test can flip "database reachable"/"unreachable" without a live
/// database. Same early-bind env-var constraint as <see cref="AuthOnApiFixture" />.
/// </summary>
public sealed class HealthProbeApiFixture : TestApiFactoryBase, IAsyncLifetime
{
    private const string RequireAuthorizationEnvKey = "FeatureManagement__RequireAuthorization";

    public HealthProbeApiFixture() => Environment.SetEnvironmentVariable(RequireAuthorizationEnvKey, "true");

    public ToggleableDbHealthCheck DbHealthCheck { get; } = new();

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        base.ConfigureTestServices(services);

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
