namespace DKNet.Templates.ScaffoldTests;

/// <summary>
/// DRK-1576 acceptance tests: <see cref="StartupAnnouncementGuard" /> gets synthetic-input coverage
/// here, then the same guard drives the real-file scenarios (§7) against the shipped template source.
/// </summary>
public class StartupAnnouncementGuardTests
{
    #region Guard unit tests (synthetic input)

    [Fact]
    public void AnnouncesThroughLogger_TrueWhenTheExactTemplateAndTokenArePresent()
    {
        const string source = """app.Logger.LogInformation("{Feature} enabled", nameof(CrosConfig));""";

        StartupAnnouncementGuard.AnnouncesThroughLogger(source, "CrosConfig").ShouldBeTrue();
    }

    [Fact]
    public void AnnouncesThroughLogger_FalseWhenTheTokenBelongsToADifferentConfigClass()
    {
        const string source = """app.Logger.LogInformation("{Feature} enabled", nameof(CrosConfig));""";

        StartupAnnouncementGuard.AnnouncesThroughLogger(source, "SwaggerConfig").ShouldBeFalse();
    }

    [Fact]
    public void AnnouncesThroughLogger_FalseWhenOnlyConsoleWriteLineIsPresent()
    {
        const string source = """Console.WriteLine("CROS enabled.");""";

        StartupAnnouncementGuard.AnnouncesThroughLogger(source, "CrosConfig").ShouldBeFalse();
    }

    [Fact]
    public void WritesToConsole_TrueForConsoleWriteLine()
    {
        StartupAnnouncementGuard.WritesToConsole("""Console.WriteLine("Swagger enabled.");""").ShouldBeTrue();
    }

    [Fact]
    public void WritesToConsole_FalseForConsoleErrorWriteLineAsync()
    {
        // R5: the process-boundary failure report is a deliberate exception, never a violation.
        StartupAnnouncementGuard.WritesToConsole("""await Console.Error.WriteLineAsync("boom");""").ShouldBeFalse();
    }

    [Fact]
    public void CountOccurrences_CountsNonOverlappingMatches()
    {
        const string source = "LogInformation(a); LogInformation(b); Console.WriteLine(c);";

        StartupAnnouncementGuard.CountOccurrences(source, "LogInformation(").ShouldBe(2);
    }

    [Fact]
    public void MigrationAnnouncesStartAndCompletionThroughLogger_FalseWhenSignatureStillTakesConfiguration()
    {
        const string source = """
            public static async Task<int> RunAsync(IConfiguration configuration)
            {
                logger.LogInformation("Running Db migration...");
                logger.LogInformation("Db migration is completed");
                await using var provider = builder.Services.BuildServiceProvider();
            }
            """;

        StartupAnnouncementGuard.MigrationAnnouncesStartAndCompletionThroughLogger(source).ShouldBeFalse();
    }

    [Fact]
    public void MigrationAnnouncesStartAndCompletionThroughLogger_FalseWhenConsoleWriteLineRemains()
    {
        const string source = """
            public static async Task<int> RunAsync(WebApplicationBuilder builder)
            {
                await using var provider = builder.Services.BuildServiceProvider();
                logger.LogInformation("Running Db migration...");
                Console.WriteLine("Db migration is completed");
            }
            """;

        StartupAnnouncementGuard.MigrationAnnouncesStartAndCompletionThroughLogger(source).ShouldBeFalse();
    }

    [Fact]
    public void MigrationAnnouncesStartAndCompletionThroughLogger_TrueWhenBothLinesLogAndProviderIsDisposed()
    {
        const string source = """
            public static async Task<int> RunAsync(WebApplicationBuilder builder)
            {
                await using var provider = builder.Services.BuildServiceProvider();
                var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("Migration");
                logger.LogInformation("Running Db migration...");
                logger.LogInformation("Db migration is completed");
            }
            """;

        StartupAnnouncementGuard.MigrationAnnouncesStartAndCompletionThroughLogger(source).ShouldBeTrue();
    }

    [Fact]
    public void MigrationFailureStillReportsOnStandardErrorWithExitCode1_FalseWhenSignatureStillTakesConfiguration()
    {
        const string source = """
            public static async Task<int> RunAsync(IConfiguration configuration)
            {
                await Console.Error.WriteLineAsync($"Db migration failed: {ex.Message}");
                return 1;
            }
            """;

        StartupAnnouncementGuard.MigrationFailureStillReportsOnStandardErrorWithExitCode1(source).ShouldBeFalse();
    }

    [Fact]
    public void MigrationFailureStillReportsOnStandardErrorWithExitCode1_TrueWhenErrorPathIsUnchanged()
    {
        const string source = """
            public static async Task<int> RunAsync(WebApplicationBuilder builder)
            {
                await Console.Error.WriteLineAsync($"Db migration failed: {ex.Message}");
                return 1;
            }
            """;

        StartupAnnouncementGuard.MigrationFailureStillReportsOnStandardErrorWithExitCode1(source).ShouldBeTrue();
    }

    #endregion

    #region Real-file scenarios (§7)

    private static string SrcDir => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "../../../../../src"));

    private static string ReadConfigFile(string relativePath) =>
        File.ReadAllText(Path.Combine(SrcDir, "ApiEndpoints/Minimal.Api/Configs", relativePath));

    public static TheoryData<string, string> AnnouncementSites() => new()
    {
        { "ForwardedHeadersConfig.cs", "ForwardedHeadersConfig" },
        { "SecurityHeadersConfig.cs", "SecurityHeadersConfig" },
        { "HttpsConfig.cs", "HttpsConfig" },
        { "RequestBoundsConfig.cs", "RequestBoundsConfig" },
        { "Healthz/HealthzConfig.cs", "HealthzConfig" },
        { "Antiforgery/AntiforgeryConfig.cs", "AntiforgeryConfig" },
        { "CrosConfig.cs", "CrosConfig" },
        { "Swagger/SwaggerConfig.cs", "SwaggerConfig" },
        { "Auth/AuthConfig.cs", "AuthConfig" },
        { "Auth/AuthConfig.cs", "DemoAuthConfig" },
        { "RateLimits/RateLimitConfig.cs", "RateLimitConfig" },
        { "AzureAppConfig/AzureAppConfigSetup.cs", "AzureAppConfigSetup" },
    };

    [Theory]
    [MemberData(nameof(AnnouncementSites))]
    public void ConfigClass_IsAnnouncedThroughILogger(string relativePath, string configClassName)
    {
        var source = ReadConfigFile(relativePath);

        StartupAnnouncementGuard.AnnouncesThroughLogger(source, configClassName).ShouldBeTrue(
            $"{relativePath} must call LogInformation(\"{{Feature}} enabled\", nameof({configClassName}))");
        StartupAnnouncementGuard.WritesToConsole(source).ShouldBeFalse(
            $"{relativePath} must not call Console.WriteLine");
    }

    [Fact]
    public void Migration_StartAndCompletionAreLoggedThroughILogger()
    {
        var source = ReadConfigFile("Jobs/MigrationJob.cs");

        StartupAnnouncementGuard.MigrationAnnouncesStartAndCompletionThroughLogger(source).ShouldBeTrue(
            "Jobs/MigrationJob.cs must log both the start and completion lines through ILogger, take a " +
            "WebApplicationBuilder, and dispose the built provider with 'await using'");
    }

    [Fact]
    public void Migration_Failure_StillReportsOnStandardErrorWithExitCode1()
    {
        var source = ReadConfigFile("Jobs/MigrationJob.cs");

        StartupAnnouncementGuard.MigrationFailureStillReportsOnStandardErrorWithExitCode1(source).ShouldBeTrue(
            "Jobs/MigrationJob.cs must still write ex.Message to stderr and return 1 on failure, under the " +
            "widened WebApplicationBuilder signature");
    }

    #endregion
}
