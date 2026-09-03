using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace Minimal.App.Tests.Integration.Support;

/// <summary>
/// TestServer never populates <c>HttpContext.Connection.RemoteIpAddress</c> the way a real socket connection
/// would, so <c>ForwardedHeadersMiddleware</c>'s "is the immediate peer a known/trusted proxy" check has nothing
/// to match against. This <see cref="IStartupFilter"/> runs as the outermost middleware (startup filters wrap
/// the whole pipeline built by <c>UseAppConfig</c>, so it executes before <c>UseForwardedHeadersConfig</c>) and
/// lets a test simulate "the request arrived from peer X" via a request-only test header, which it strips
/// before calling into the real pipeline so it never reaches application code.
/// </summary>
public sealed class RemoteIpTestStartupFilter : IStartupFilter
{
    public const string TestRemoteIpHeader = "X-Test-RemoteIp";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (context, nextMiddleware) =>
        {
            if (context.Request.Headers.TryGetValue(TestRemoteIpHeader, out var ip) &&
                IPAddress.TryParse(ip.ToString(), out var parsed))
            {
                context.Connection.RemoteIpAddress = parsed;
                context.Request.Headers.Remove(TestRemoteIpHeader);
            }

            await nextMiddleware();
        });

        next(app);
    };
}
