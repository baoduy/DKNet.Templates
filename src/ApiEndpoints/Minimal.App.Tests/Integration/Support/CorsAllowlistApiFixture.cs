using Minimal.App.TestSupport;

namespace Minimal.App.Tests.Integration.Support;

/// <summary>
/// <see cref="ApiFixture" /> variant with <c>Cors:AllowedOrigins</c> set to a single origin
/// (<c>https://app.example.com</c>), so the default CORS policy is registered and scoped to that origin.
/// </summary>
/// <remarks>
/// Same early-bind constraint as <see cref="AuthOnApiFixture" />: <c>AddCrosConfig</c> reads
/// <c>Cors:AllowedOrigins</c> from <c>builder.Configuration</c> in <c>Program.cs</c> before
/// <see cref="TestApiFactoryBase.ConfigureWebHost" />'s <c>ConfigureAppConfiguration</c> override is merged
/// in, so only an environment variable set ahead of <c>WebApplication.CreateBuilder(args)</c> takes effect.
/// </remarks>
public sealed class CorsAllowlistApiFixture : TestApiFactoryBase, IAsyncLifetime
{
    public const string AllowedOrigin = "https://app.example.com";
    private const string AllowedOriginsEnvKey = "Cors__AllowedOrigins__0";

    public CorsAllowlistApiFixture() => Environment.SetEnvironmentVariable(AllowedOriginsEnvKey, AllowedOrigin);

    #region Methods

    public async Task InitializeAsync()
    {
        _ = CreateClient();
        await ResetDatabaseAsync();
    }

    Task IAsyncLifetime.DisposeAsync() => Task.CompletedTask;

    protected override void Dispose(bool disposing)
    {
        Environment.SetEnvironmentVariable(AllowedOriginsEnvKey, null);
        base.Dispose(disposing);
    }

    #endregion
}
