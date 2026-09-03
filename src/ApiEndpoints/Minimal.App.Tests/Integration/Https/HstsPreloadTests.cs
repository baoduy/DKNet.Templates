using Microsoft.AspNetCore.Mvc.Testing;
using Minimal.App.Tests.Integration.Support;

namespace Minimal.App.Tests.Integration.Https;

/// <summary>
/// DRK-1028 §5: a qualifying transport-security lifetime (365 days) announces preload; a non-qualifying one (30
/// days) must not request it — HSTS preload below the 365-day minimum is meaningless and rejected by preload
/// tooling, so requesting it anyway would be a false claim.
/// </summary>
public sealed class HstsPreloadTests
{
    // HstsMiddleware only writes the header for an HTTPS request (and skips loopback/localhost hosts, meaningless
    // for local dev) — TestServer's default client is plain http://localhost, so requests here go over an
    // explicit https:// base address with a non-loopback host instead.
    private static HttpClient SecureClient(WebApplicationFactory<Minimal.Api.Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://acme.example") });

    public sealed class WithAQualifyingLifetime(QualifyingHstsApiFixture fixture) : IClassFixture<QualifyingHstsApiFixture>
    {
        [Fact]
        public async Task AnnouncesPreload()
        {
            var response = await SecureClient(fixture).GetAsync("/healthz");

            var hsts = response.Headers.GetValues("Strict-Transport-Security").Single();
            hsts.ShouldContain("preload");
            hsts.ShouldContain("max-age=31536000");
        }
    }

    public sealed class WithANonQualifyingLifetime(NonQualifyingHstsApiFixture fixture)
        : IClassFixture<NonQualifyingHstsApiFixture>
    {
        [Fact]
        public async Task DoesNotAnnouncePreload()
        {
            var response = await SecureClient(fixture).GetAsync("/healthz");

            var hsts = response.Headers.GetValues("Strict-Transport-Security").Single();
            hsts.ShouldNotContain("preload");
        }
    }
}
