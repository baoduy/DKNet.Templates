using Minimal.App.Tests.Integration.Support;

namespace Minimal.App.Tests.Integration.SecureDefault;

/// <summary>
/// DRK-1028 §5 (R3): a hardening item can be relaxed for local development by configuration alone, and doing so
/// does not affect any other item. Transport-security enforcement is switched off while security headers,
/// forwarded-header handling and request bounds are explicitly forced to their secure default.
/// </summary>
public sealed class HardeningItemRelaxedByConfigTests(HstsOffOtherwiseSecureApiFixture fixture)
    : IClassFixture<HstsOffOtherwiseSecureApiFixture>
{
    [Fact]
    public async Task TransportSecurityIsNotEnforced()
    {
        var response = await fixture.CreateClient().GetAsync("/healthz");

        response.Headers.Contains("Strict-Transport-Security").ShouldBeFalse(
            "EnableHttps=false must mean no HSTS header is emitted.");
    }

    [Fact]
    public async Task NoOtherHardeningItemIsAffected()
    {
        var response = await fixture.CreateClient().GetAsync("/healthz");

        response.Headers.Contains("X-Frame-Options").ShouldBeTrue(
            "security headers must still be enforced independently of the HSTS switch.");
        response.Headers.Contains("Server").ShouldBeFalse();
    }
}
