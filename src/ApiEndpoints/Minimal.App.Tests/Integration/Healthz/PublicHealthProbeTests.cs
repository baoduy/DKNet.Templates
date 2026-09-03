using Minimal.App.Tests.Integration.Support;

namespace Minimal.App.Tests.Integration.Healthz;

/// <summary>
/// DRK-1028 §5: the public probe (<c>/healthz</c> and <c>/</c>) stays reachable without credentials and reports
/// status only — no dependency name, timing, description or exception text (R4) — in either health state.
/// </summary>
public sealed class PublicHealthProbeTests(HealthProbeApiFixture fixture) : IClassFixture<HealthProbeApiFixture>
{
    [Theory]
    [InlineData("/healthz")]
    [InlineData("/")]
    public async Task AnonymousCaller_WhenHealthy_ReportsServing(string path)
    {
        fixture.DbHealthCheck.IsHealthy = true;
        var client = fixture.CreateClient();

        var response = await client.GetAsync(path);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("Healthy");
    }

    [Theory]
    [InlineData("/healthz")]
    [InlineData("/")]
    public async Task AnonymousCaller_WhenDatabaseUnreachable_ReportsNotServingWithNoLeak(string path)
    {
        fixture.DbHealthCheck.IsHealthy = false;
        var client = fixture.CreateClient();

        var response = await client.GetAsync(path);

        response.StatusCode.ShouldNotBe(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("Unhealthy");
        body.ShouldNotContain("CoreDbContext");
        body.ShouldNotContain("simulated DB outage");
        body.ShouldNotContain("InvalidOperationException");
        body.ShouldNotContain("database unreachable");
    }
}
