using System.Reflection;
using System.Text.Json;
using Minimal.Share.Options;

namespace Minimal.App.Tests.Architecture;

/// <summary>
/// Enforces the `FeatureManagement` config-binding contract: every JSON key under that section in every
/// `appsettings*.json` must map to a public settable <see cref="FeatureOptions" /> property (case-insensitive,
/// matching how <c>IConfiguration</c> binds), and every public settable property must be read by at least one
/// production class — an unread property is a dead flag nothing exercises (see repo `CLAUDE.md` gotcha on
/// `FeatureOptions`/`appsettings*.json` staying in step).
/// </summary>
public class FeatureOptionsBindingTests
{
    #region Methods

    [Fact]
    public void EveryFeatureManagementKey_InEveryAppSettingsFile_ShouldMapToAFeatureOptionsProperty()
    {
        var srcDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
        var appSettingsFiles = Directory.GetFiles(
            Path.Combine(srcDir, "ApiEndpoints", "Minimal.Api"), "appsettings*.json");
        appSettingsFiles.ShouldNotBeEmpty();

        var propertyNames = FeatureOptionsProperties()
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var offenders = new List<string>();
        foreach (var file in appSettingsFiles)
        {
            using var document = JsonDocument.Parse(File.ReadAllText(file));
            if (!document.RootElement.TryGetProperty("FeatureManagement", out var section))
                continue;

            offenders.AddRange(
                from key in section.EnumerateObject()
                where !propertyNames.Contains(key.Name)
                select $"{Path.GetFileName(file)}: {key.Name}");
        }

        offenders.ShouldBeEmpty(
            "The following FeatureManagement keys do not map to any public settable FeatureOptions " +
            $"property: {string.Join(", ", offenders)}");
    }

    [Fact]
    public void EveryFeatureOptionsProperty_ShouldBeReadBySomeProductionClass()
    {
        var srcDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));

        var productionSourceFiles = Directory.GetFiles(srcDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                        && !f.Contains("Minimal.App.Tests")
                        && !f.Contains("Minimal.App.BDDTests")
                        && !f.EndsWith(Path.Combine("Options", "FeatureOptions.cs"), StringComparison.Ordinal))
            .Select(File.ReadAllText)
            .ToArray();

        var unread = FeatureOptionsProperties()
            .Where(p => !productionSourceFiles.Any(source => source.Contains($".{p.Name}", StringComparison.Ordinal)))
            .Select(p => p.Name)
            .ToArray();

        unread.ShouldBeEmpty(
            "The following FeatureOptions properties are never read by any production class — dead flags: " +
            string.Join(", ", unread));
    }

    private static PropertyInfo[] FeatureOptionsProperties() => typeof(FeatureOptions)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(p => p.CanWrite)
        .ToArray();

    #endregion
}
