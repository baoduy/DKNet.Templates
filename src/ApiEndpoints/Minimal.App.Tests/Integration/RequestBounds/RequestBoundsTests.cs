using System.Net.Sockets;
using System.Text;
using Minimal.App.Tests.Integration.Support;

namespace Minimal.App.Tests.Integration.RequestBounds;

/// <summary>
/// DRK-1028 §5: max request body size and header-read timeout are <c>KestrelServerOptions.Limits</c> — Kestrel
/// connection-level enforcement that only a real Kestrel process exercises (see
/// <see cref="RealProcessApiFixture" />). Both scenarios here run over a real loopback socket so the bounds are
/// proven, not assumed from configuration alone.
/// </summary>
public sealed class RequestBoundsTests(RealProcessApiFixture fixture) : IClassFixture<RealProcessApiFixture>
{
    [Fact]
    public async Task OversizedRequestBody_IsRefused()
    {
        // Kestrel rejects as soon as a declared Content-Length exceeds MaxRequestBodySize — it responds 413 and
        // closes the connection right after parsing headers, before reading any body. Sending the body via
        // HttpClient races that close (the framework throws "broken pipe" instead of surfacing the response), so
        // this writes the request line/headers directly and reads the status line without ever sending the body.
        const int oversizedBodyLength = 5 * 1024 * 1024; // over the 1 MB bound this fixture configures.

        using var tcpClient = new TcpClient();
        await tcpClient.ConnectAsync(fixture.Host, fixture.Port);
        await using var stream = tcpClient.GetStream();

        var headers = Encoding.ASCII.GetBytes(
            $"POST /v1/purchase-orders HTTP/1.1\r\n" +
            $"Host: {fixture.Host}\r\n" +
            $"Content-Type: application/json\r\n" +
            $"Content-Length: {oversizedBodyLength}\r\n" +
            "Connection: close\r\n\r\n");
        await stream.WriteAsync(headers);
        await stream.FlushAsync();

        using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
        var statusLine = await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10));

        statusLine.ShouldNotBeNull();
        statusLine.ShouldContain("413");
    }

    [Fact]
    public async Task ClientSendingHeadersTooSlowly_IsCutOffWithoutWaitingForTheRest()
    {
        using var tcpClient = new TcpClient();
        await tcpClient.ConnectAsync(fixture.Host, fixture.Port);
        await using var stream = tcpClient.GetStream();

        // Send the request line and ONE header, then stop — never send the terminating blank line, so the
        // server is left waiting for "the rest of the headers" past its configured RequestHeadersTimeout (2s).
        var partial = Encoding.ASCII.GetBytes(
            $"GET /healthz HTTP/1.1\r\nHost: {fixture.Host}\r\nX-Partial: still-sending\r\n");
        await stream.WriteAsync(partial);
        await stream.FlushAsync();

        // The server must give up on the request once the header-read timeout elapses rather than wait forever
        // for the remaining headers — whether that means a 408-style response, an abrupt reset, or a plain
        // close, the connection must not still be open (and no full response to the never-completed request)
        // once comfortably past the configured 2s bound. Drain whatever the server sends until it closes.
        tcpClient.ReceiveTimeout = 10_000;
        var buffer = new byte[4096];
        using var received = new MemoryStream();
        int read;
        while ((read = await stream.ReadAsync(buffer)) > 0)
        {
            received.Write(buffer, 0, read);
        }

        var text = Encoding.ASCII.GetString(received.ToArray());
        text.Contains("200 OK", StringComparison.Ordinal).ShouldBeFalse(
            "the request was never completed (headers never finished), so it must never succeed as if it had.");
    }
}
