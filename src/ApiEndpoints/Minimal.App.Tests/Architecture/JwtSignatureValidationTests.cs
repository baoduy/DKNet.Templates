namespace Minimal.App.Tests.Architecture;

/// <summary>
///     Regression guard for SEC-007: a token handler that skips JWT signature verification
///     (via <c>TokenValidationParameters.SignatureValidator</c> or
///     <c>ValidateIssuerSigningKey = false</c>) or replaces the stock JWT bearer handler
///     (via <c>TokenHandlers.Clear()</c>) must never reappear in <c>Minimal.Api</c>.
/// </summary>
public class JwtSignatureValidationTests
{
    private static readonly string[] ForbiddenPatterns =
    [
        "SignatureValidator",
        "ValidateIssuerSigningKey = false",
        "TokenHandlers.Clear()"
    ];

    [Fact]
    public void MinimalApiSource_ShouldNotDisableJwtSignatureValidation()
    {
        var apiSourceDir = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "../../../../../ApiEndpoints/Minimal.Api"));

        Directory.Exists(apiSourceDir).ShouldBeTrue();

        var sourceFiles = Directory.GetFiles(apiSourceDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .ToArray();

        sourceFiles.ShouldNotBeEmpty();

        var offenders = sourceFiles
            .SelectMany(file => ForbiddenPatterns
                .Where(pattern => File.ReadAllText(file).Contains(pattern, StringComparison.Ordinal))
                .Select(pattern => $"{Path.GetFileName(file)}: {pattern}"))
            .ToArray();

        offenders.ShouldBeEmpty(
            "Minimal.Api must rely on stock JWT bearer signature validation — none of its source files " +
            "may disable it. Offenders: " + string.Join(", ", offenders));
    }
}
