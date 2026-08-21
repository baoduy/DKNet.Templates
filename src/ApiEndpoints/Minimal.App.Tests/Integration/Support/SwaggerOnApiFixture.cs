using Minimal.App.TestSupport;

namespace Minimal.App.Tests.Integration.Support;

/// <summary>
/// <see cref="ApiFixture" /> variant with <c>FeatureManagement:EnableSwagger</c> flipped on, so
/// <c>/openapi/v1.json</c> is mapped for regression coverage of the generated document.
/// </summary>
/// <remarks>
/// Same early-bind constraint as <see cref="AuthOnApiFixture" />: <c>Program.cs</c> reads
/// <c>FeatureOptions</c> from configuration before <see cref="TestApiFactoryBase.ConfigureWebHost" />'s
/// <c>ConfigureAppConfiguration</c> override is merged in, so only an environment variable set ahead of
/// <c>WebApplication.CreateBuilder(args)</c> takes effect.
/// </remarks>
public sealed class SwaggerOnApiFixture : TestApiFactoryBase, IAsyncLifetime
{
    private const string EnableSwaggerEnvKey = "FeatureManagement__EnableSwagger";

    public SwaggerOnApiFixture() => Environment.SetEnvironmentVariable(EnableSwaggerEnvKey, "true");

    #region Methods

    public async Task InitializeAsync()
    {
        _ = CreateClient();
        await ResetDatabaseAsync();
    }

    Task IAsyncLifetime.DisposeAsync() => Task.CompletedTask;

    protected override void Dispose(bool disposing)
    {
        Environment.SetEnvironmentVariable(EnableSwaggerEnvKey, null);
        base.Dispose(disposing);
    }

    #endregion
}
