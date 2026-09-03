using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Minimal.Api.Configs;
using Minimal.Share.Options;

namespace Minimal.App.Tests.Integration.RequestBounds;

/// <summary>
/// DRK-1028 §5: a request still running after its stated lifetime is refused as timed out (504) rather than
/// left to run forever. The request-lifetime bound is pure ASP.NET Core middleware
/// (<c>AddRequestTimeouts</c>/<c>UseRequestTimeouts</c>, unlike the Kestrel-level body-size/header-timeout
/// bounds in <see cref="RequestBoundsTests" />), so it is driven here through the real
/// <see cref="RequestBoundsConfig" /> wiring on a minimal <c>TestServer</c> host — the same technique
/// <c>GlobalExceptionHandlerHttpTests</c> uses for <c>GlobalExceptionConfigs</c>.
/// </summary>
public sealed class RequestLifetimeTests
{
    [Fact]
    public async Task RequestStillRunningAfterItsLifetime_IsRefusedAsTimedOut()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["RequestBounds:RequestTimeoutSeconds"] = "1" })
            .Build();
        builder.Services.AddRequestBoundsConfig(new FeatureOptions { EnableRequestBounds = true }, configuration);

        var app = builder.Build();
        app.UseRouting();
        app.UseRequestBoundsConfig();
        app.MapGet("/slow", async (CancellationToken ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
            return Results.Ok();
        });
        await app.StartAsync();

        using var client = app.GetTestClient();
        var response = await client.GetAsync("/slow");

        response.StatusCode.ShouldBe(HttpStatusCode.GatewayTimeout);
    }
}
