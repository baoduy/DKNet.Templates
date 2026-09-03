using Minimal.App.TestSupport;

namespace Minimal.App.Tests.Integration.Support;

/// <summary>
/// <see cref="ApiFixture" /> variant with <c>RequireAuthorization</c> and <c>EnableSwagger</c> on, but the real
/// JWT bearer scheme left in place (no <see cref="TestAuthHandler" />) — a request with no <c>Authorization</c>
/// header genuinely fails to authenticate (JwtBearer's <c>HandleAuthenticateAsync</c> short-circuits to
/// <c>NoResult()</c> without needing network access to the token issuer), so this represents an actual
/// anonymous caller against the default-deny <c>FallbackPolicy</c>. Same early-bind env-var constraint as
/// <see cref="AuthOnApiFixture" />.
/// </summary>
public sealed class RequireAuthNoHandlerApiFixture : TestApiFactoryBase, IAsyncLifetime
{
    private const string RequireAuthorizationEnvKey = "FeatureManagement__RequireAuthorization";
    private const string EnableSwaggerEnvKey = "FeatureManagement__EnableSwagger";

    public RequireAuthNoHandlerApiFixture()
    {
        Environment.SetEnvironmentVariable(RequireAuthorizationEnvKey, "true");
        Environment.SetEnvironmentVariable(EnableSwaggerEnvKey, "true");
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
        Environment.SetEnvironmentVariable(EnableSwaggerEnvKey, null);
        base.Dispose(disposing);
    }
}
