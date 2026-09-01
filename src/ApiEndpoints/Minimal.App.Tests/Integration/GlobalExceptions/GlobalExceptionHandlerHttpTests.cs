using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging;
using Minimal.Api.Configs.GlobalExceptions;

namespace Minimal.App.Tests.Integration.GlobalExceptions;

/// <summary>
/// Drives <see cref="GlobalExceptionHandler"/> through the real <c>AddGlobalException</c>/<c>UseGlobalException</c>
/// wiring on a minimal <see cref="WebApplication"/> + <see cref="TestServer"/> host, so assertions are made on the
/// actual HTTP response body rather than the handler's internals.
/// </summary>
public sealed class GlobalExceptionHandlerHttpTests
{
    private const string GenericDetail = "An unexpected error occurred. Quote the trace-id when reporting this.";

    [Fact]
    public async Task NonDevelopment_ReturnsGenericDetailWithNoTypeAndNoInnerExceptionText()
    {
        var inner = new Exception(
            "23505: duplicate key value violates unique constraint \"IX_Products_Name\": Key (Name)=(Widget) already exists.");
        var outer = new InvalidOperationException("Saving changes failed.", inner);

        var result = await ThrowInHostAsync("Production", outer);

        result.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        result.Body.GetProperty("detail").GetString().ShouldBe(GenericDetail);
        result.Body.TryGetProperty("type", out _).ShouldBeFalse();

        var raw = result.Body.ToString();
        raw.ShouldNotContain("23505");
        raw.ShouldNotContain("IX_Products_Name");
        raw.ShouldNotContain("Widget");
    }

    [Fact]
    public async Task NonDevelopment_StillExposesTraceId()
    {
        var result = await ThrowInHostAsync("Production", new Exception("boom"));

        result.Body.TryGetProperty("trace-id", out var traceId).ShouldBeTrue();
        traceId.GetString().ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task Development_ReturnsThrownExceptionMessageAndType()
    {
        var result = await ThrowInHostAsync("Development", new InvalidOperationException("boom"));

        result.Body.GetProperty("detail").GetString().ShouldBe("boom");
        result.Body.GetProperty("type").GetString().ShouldBe(nameof(InvalidOperationException));
    }

    [Fact]
    public async Task Development_NeverExposesInnerExceptionText()
    {
        var inner = new Exception("inner secret data");
        var outer = new InvalidOperationException("outer boom", inner);

        var result = await ThrowInHostAsync("Development", outer);

        result.Body.GetProperty("detail").GetString().ShouldBe("outer boom");
        result.Body.ToString().ShouldNotContain("inner secret data");
    }

    [Fact]
    public async Task AnyEnvironment_LogsFullExceptionIncludingInnerChain()
    {
        var inner = new Exception("inner failure");
        var outer = new InvalidOperationException("outer failure", inner);

        var result = await ThrowInHostAsync("Production", outer);

        var logged = result.Logs.ShouldHaveSingleItem();
        logged.Level.ShouldBe(LogLevel.Error);
        logged.Exception.ShouldBeSameAs(outer);
        logged.Exception!.InnerException.ShouldBeSameAs(inner);
    }

    [Fact]
    public async Task ExceptionMessageContainingBraces_DoesNotBreakLoggingOrResponse()
    {
        var exception = new InvalidOperationException("Unexpected token { at position } 12");

        var result = await ThrowInHostAsync("Development", exception);

        result.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        result.Body.GetProperty("detail").GetString().ShouldBe(exception.Message);
        result.Logs.ShouldHaveSingleItem().Exception.ShouldBeSameAs(exception);
    }

    private static async Task<ThrowResult> ThrowInHostAsync(string environmentName, Exception exceptionToThrow)
    {
        var logs = new List<(LogLevel Level, Exception? Exception)>();

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environmentName });
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(new CapturingLoggerProvider(logs));
        builder.Services.AddRouting();
        builder.Services.AddGlobalException();

        await using var app = builder.Build();
        app.UseGlobalException();
        app.MapGet("/throw", (Func<IResult>)(() => throw exceptionToThrow));
        await app.StartAsync();

        using var client = app.GetTestClient();
        var response = await client.GetAsync("/throw");
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();

        return new ThrowResult(response.StatusCode, body, logs);
    }

    private sealed record ThrowResult(HttpStatusCode StatusCode, JsonElement Body, IReadOnlyList<(LogLevel Level, Exception? Exception)> Logs);

    private sealed class CapturingLoggerProvider(List<(LogLevel Level, Exception? Exception)> sink) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new CapturingLogger(sink);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(List<(LogLevel Level, Exception? Exception)> sink) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter) =>
                sink.Add((logLevel, exception));
        }
    }
}
