using Minimal.App.TestSupport;

namespace Minimal.App.Tests.Integration.Support;

/// <summary>
/// <see cref="ApiFixture" /> variant with <c>FeatureManagement:RequireAuthorization</c> flipped on, like
/// <see cref="AuthOnApiFixture" />, but authenticates every request via <see cref="NoNameClaimAuthHandler" />
/// instead — a caller who is authenticated but whose token carries no <see cref="System.Security.Claims.ClaimTypes.Name" />
/// claim. Proves the missing-claim-while-authenticated path: the declared member holds its default
/// (<see langword="null" />), never the <c>SystemAccountFallback</c> (that fallback only applies when
/// authorization is off).
/// </summary>
/// <remarks>
/// See <see cref="AuthOnApiFixture" />'s remarks for why the early-bind env var is required here too.
/// </remarks>
public sealed class AuthOnNoNameClaimApiFixture : TestApiFactoryBase, IAsyncLifetime
{
    private const string RequireAuthorizationEnvKey = "FeatureManagement__RequireAuthorization";

    public AuthOnNoNameClaimApiFixture() => Environment.SetEnvironmentVariable(RequireAuthorizationEnvKey, "true");

    #region Methods

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        base.ConfigureTestServices(services);
        NoNameClaimAuthHandler.Register(services);
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

    #endregion
}
