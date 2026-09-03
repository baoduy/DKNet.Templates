using Minimal.App.Tests.Integration.Support;

namespace Minimal.App.Tests.Integration.Healthz;

/// <summary>
/// DRK-1028 §5: <c>/healthz/detail</c> is not obtainable anonymously on the public surface, and stays available
/// to an authenticated operator, naming each dependency checked and identifying the one that failed.
/// </summary>
public sealed class DetailedHealthReportTests
{
    public sealed class WhenAnonymous(HealthProbeApiFixture fixture) : IClassFixture<HealthProbeApiFixture>
    {
        [Fact]
        public async Task CannotObtainDetailedReport()
        {
            fixture.DbHealthCheck.IsHealthy = false;
            var client = fixture.CreateClient();

            var response = await client.GetAsync("/healthz/detail");

            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            var body = await response.Content.ReadAsStringAsync();
            body.ShouldNotContain("CoreDbContext");
            body.ShouldNotContain("simulated DB outage");
        }
    }

    public sealed class WhenAuthenticatedOperator(HealthProbeOperatorApiFixture fixture)
        : IClassFixture<HealthProbeOperatorApiFixture>
    {
        [Fact]
        public async Task ReceivesDetailedReport_NamingTheFailedDependency()
        {
            fixture.DbHealthCheck.IsHealthy = false;
            var client = fixture.CreateClient();

            var response = await client.GetAsync("/healthz/detail");

            response.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized);
            var body = await response.Content.ReadAsStringAsync();
            body.ShouldContain("CoreDbContext");
            body.ShouldContain("Unhealthy");
        }
    }
}
