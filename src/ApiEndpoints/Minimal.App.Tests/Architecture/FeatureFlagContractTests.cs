using Minimal.Share.Options;

namespace Minimal.App.Tests.Architecture;

/// <summary>
/// Guards the contract between the FeatureManagement section in appsettings and <see cref="FeatureOptions"/>.
/// The section is bound with <c>Get&lt;FeatureOptions&gt;()</c>, which ignores keys that match no property, so a
/// misspelled flag fails silently: the operator flips it, nothing happens, and the service keeps doing whatever
/// the code default said. For a template that is stamped into every new service, one drifted key is debugged
/// independently by every consumer.
/// </summary>
public class FeatureFlagContractTests
{
    #region Fields

    /// <summary>
    /// Keys present in appsettings today that bind to no <see cref="FeatureOptions"/> property (DRK-904).
    /// This allow-list may only shrink: delete an entry once the key is renamed to match its property.
    /// Do not add to it — a new entry means a new flag that silently does nothing.
    /// </summary>
    private static readonly HashSet<string> KnownViolations = new(StringComparer.Ordinal)
    {
        "EnableServiceBusProcess",
        "EnableAzureAppConfiguration"
    };

    #endregion

    #region Methods

    [Theory]
    [InlineData("Minimal.Api/appsettings.json")]
    [InlineData("Minimal.Api/appsettings.Development.json")]
    public void FeatureManagementKeys_ShouldBindToAFeatureOptionsProperty(string relativePath)
    {
        var srcDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
        var settingsPath = Path.Combine(srcDir, "ApiEndpoints", relativePath);

        File.Exists(settingsPath).ShouldBeTrue($"{relativePath} should exist.");

        using var doc = JsonDocument.Parse(File.ReadAllText(settingsPath));

        if (!doc.RootElement.TryGetProperty(FeatureOptions.Name, out var section))
        {
            return; // no FeatureManagement section in this file — nothing to check
        }

        var properties = typeof(FeatureOptions)
            .GetProperties()
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var keys = section.EnumerateObject().Select(p => p.Name).ToArray();

        var orphans = keys
            .Where(k => !properties.Contains(k))
            .Where(k => !KnownViolations.Contains(k))
            .ToArray();

        orphans.ShouldBeEmpty(
            $"Every key under {FeatureOptions.Name} in {relativePath} must match a FeatureOptions property, " +
            "because Get<FeatureOptions>() silently drops the ones that don't — the flag reads as a working " +
            "switch and controls nothing. Rename the key (or add the property). New orphans: " +
            string.Join(", ", orphans));

        var fixedUp = KnownViolations
            .Where(k => keys.Contains(k, StringComparer.Ordinal) && properties.Contains(k))
            .ToArray();

        fixedUp.ShouldBeEmpty(
            "These keys are on the KnownViolations allow-list but now bind correctly — that is the fix landing. " +
            "Delete them from KnownViolations so the rule is enforced for them: " + string.Join(", ", fixedUp));
    }

    /// <summary>
    /// A FeatureOptions property that no code reads is a switch the operator cannot actually throw. The template
    /// declares <c>EnableMsGraphJwtTokenValidation</c> but hardwires the behaviour with a const in AuthConfig
    /// (DRK-897, DRK-904), so flipping the flag has no effect.
    /// </summary>
    [Fact]
    public void FeatureOptionsProperties_ShouldBeReadBySomeConfigCode()
    {
        var srcDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));

        var sources = Directory
            .EnumerateFiles(Path.Combine(srcDir, "ApiEndpoints"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal))
            .Where(f => !f.EndsWith("FeatureOptions.cs", StringComparison.Ordinal))
            .Where(f => !f.EndsWith("FeatureFlagContractTests.cs", StringComparison.Ordinal))
            .Select(File.ReadAllText)
            .ToArray();

        var unread = typeof(FeatureOptions)
            .GetProperties()
            .Where(p => p.Name != nameof(FeatureOptions.Name))
            .Select(p => p.Name)
            .Where(name => !sources.Any(s => s.Contains(name, StringComparison.Ordinal)))
            .Where(name => !KnownUnreadProperties.Contains(name))
            .ToArray();

        unread.ShouldBeEmpty(
            "Every FeatureOptions property must be read somewhere, or it is a switch that looks like it works " +
            "and controls nothing — the worst kind for a security flag. New unread properties: " +
            string.Join(", ", unread));
    }

    /// <summary>
    /// FeatureOptions properties nothing reads today (DRK-904). Shrink only.
    /// </summary>
    private static readonly HashSet<string> KnownUnreadProperties = new(StringComparer.Ordinal)
    {
        nameof(FeatureOptions.EnableServiceBus)
    };

    #endregion
}
