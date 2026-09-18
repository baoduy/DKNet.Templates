using Minimal.Share;

namespace DKNet.Templates.ScaffoldTests;

/// <summary>
/// DRK-1577: the base <c>appsettings.json</c> must declare the <c>ConnectionStrings</c> key the
/// application actually opens its <c>DbContext</c> with, and no key that nothing reads. Asserted
/// against the JSON directly (no host, no environment overlay) — the same seam as
/// <see cref="SecureDefaultAppSettingsTests" />.
/// </summary>
public class ConnectionStringsContractTests
{
    #region Methods

    private static JsonElement ConnectionStringsSection()
    {
        var path = AppSettingsScanner.BaseAppSettingsPath();
        File.Exists(path).ShouldBeTrue($"base config not found at {path}");

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        doc.RootElement.TryGetProperty("ConnectionStrings", out var section)
            .ShouldBeTrue("the base appsettings.json must declare a ConnectionStrings section.");

        return section.Clone();
    }

    [Fact]
    public void BaseAppSettings_DeclaresTheDatabaseConnectionKey()
    {
        var section = ConnectionStringsSection();

        section.TryGetProperty(SharedConsts.DbConnectionString, out _).ShouldBeTrue(
            $"the base appsettings.json's ConnectionStrings section must declare " +
            $"\"{SharedConsts.DbConnectionString}\" — InfraSetup.AddInfraServices opens the DbContext with " +
            "SharedConsts.DbConnectionString, and a missing key is a null connection string at runtime.");
    }

    [Fact]
    public void BaseAppSettings_DeclaresNoUnreadConnectionKey()
    {
        var section = ConnectionStringsSection();
        var srcDir = AppSettingsScanner.SrcDir();

        var sources = Directory
            .EnumerateFiles(srcDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal))
            .Select(File.ReadAllText)
            .ToArray();

        var unread = section.EnumerateObject()
            .Select(p => p.Name)
            .Where(key => !sources.Any(s => s.Contains($"\"{key}\"", StringComparison.Ordinal)))
            .ToArray();

        unread.ShouldBeEmpty(
            "Every key under ConnectionStrings in the base appsettings.json must be read by some quoted " +
            "literal in src/**/*.cs, or it is a connection string nothing opens. Unread key(s): " +
            string.Join(", ", unread));
    }

    #endregion
}
