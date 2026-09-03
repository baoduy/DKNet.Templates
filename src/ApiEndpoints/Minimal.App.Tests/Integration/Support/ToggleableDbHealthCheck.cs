using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Minimal.App.Tests.Integration.Support;

/// <summary>
/// Stands in for the real <c>AddDbContextCheck&lt;CoreDbContext&gt;()</c> check under a fixture that needs to
/// flip between "database reachable" and "database unreachable" — EF Core's InMemory provider always reports
/// <c>CanConnectAsync() == true</c>, so there is no way to make the real check fail without this double.
/// Registered under the SAME check name ("CoreDbContext") the real check uses, via <c>HealthProbeApiFixture</c>
/// replacing the registration in <c>HealthCheckServiceOptions</c>.
/// </summary>
public sealed class ToggleableDbHealthCheck : IHealthCheck
{
    public bool IsHealthy { get; set; } = true;

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(IsHealthy
            ? HealthCheckResult.Healthy("database reachable")
            : HealthCheckResult.Unhealthy("database unreachable", new InvalidOperationException("simulated DB outage")));
}
