using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Minimal.App.TestSupport;

/// <summary>
/// Fake authentication scheme standing in for the real JWT bearer scheme (which needs a live MS Graph token
/// to validate), so the "authorization required" path can be exercised in-process. Every request is
/// unconditionally authenticated as <see cref="CallerName" />.
/// </summary>
public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "TestScheme";
    public const string CallerName = "test-authenticated-caller";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, CallerName)], SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    /// <summary>
    /// Registers this scheme as the default authenticate/challenge scheme, overriding whatever the host's own
    /// <c>AddAuthConfig</c> configured. Call from a test factory's <c>ConfigureTestServices</c> override.
    /// </summary>
    public static void Register(IServiceCollection services) =>
        services.AddAuthentication(SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(SchemeName, _ => { });
}
