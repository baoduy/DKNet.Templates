using Minimal.Share.Options;

namespace DKNet.Templates.ScaffoldTests;

/// <summary>
/// DRK-1903 (SEC-007): the security switches on <see cref="FeatureOptions" /> must default to the secure value
/// in code, not only in the shipped <c>appsettings.json</c>. <c>Program.cs</c> binds the section with
/// <c>Get&lt;FeatureOptions&gt;() ?? new FeatureOptions()</c>, so any key missing from every configuration
/// source silently falls back to the property initialiser — a trimmed appsettings file or a key forgotten when
/// moving settings to environment variables must never turn a scaffolded service anonymous or plaintext.
/// <see cref="SecureDefaultAppSettingsTests" /> guards the JSON; this guards the fallback.
/// <para>
/// Tier-2 guard (architecture review DRK-1896): <see cref="KnownViolations" /> is today's baseline and may only
/// shrink — fixing DRK-1903 deletes both entries, and <see cref="EveryBaselineEntry_StillDefaultsInsecure" />
/// fails if an entry goes stale.
/// </para>
/// </summary>
public class FeatureOptionsSecureDefaultsTests
{
    /// <summary>Flag name → the value a bare <c>new FeatureOptions()</c> must carry.</summary>
    private static readonly Dictionary<string, Func<FeatureOptions, bool>> SecureWhenTrue = new(StringComparer.Ordinal)
    {
        [nameof(FeatureOptions.RequireAuthorization)] = o => o.RequireAuthorization,
        [nameof(FeatureOptions.EnableHttps)] = o => o.EnableHttps,
        [nameof(FeatureOptions.EnableRateLimit)] = o => o.EnableRateLimit,
        [nameof(FeatureOptions.EnableSecurityHeaders)] = o => o.EnableSecurityHeaders,
        [nameof(FeatureOptions.EnableRequestBounds)] = o => o.EnableRequestBounds,
    };

    /// <summary>Flags whose secure default is off: the demonstration identity is never a real caller.</summary>
    private static readonly Dictionary<string, Func<FeatureOptions, bool>> SecureWhenFalse = new(StringComparer.Ordinal)
    {
        [nameof(FeatureOptions.EnableDemoAuthentication)] = o => o.EnableDemoAuthentication,
    };

    /// <summary>Today's offenders (DRK-1903). Only ever remove entries; never add one.</summary>
    private static readonly HashSet<string> KnownViolations = new(StringComparer.Ordinal)
    {
        nameof(FeatureOptions.RequireAuthorization),
        nameof(FeatureOptions.EnableHttps),
    };

    private static string[] InsecureClassDefaults()
    {
        var defaults = new FeatureOptions();
        return
        [
            .. SecureWhenTrue.Where(f => !f.Value(defaults)).Select(f => f.Key),
            .. SecureWhenFalse.Where(f => f.Value(defaults)).Select(f => f.Key),
        ];
    }

    [Fact]
    public void SecurityFlags_ClassDefault_MustBeTheSecureValue()
    {
        var offenders = InsecureClassDefaults().Where(f => !KnownViolations.Contains(f)).Order().ToArray();

        offenders.ShouldBeEmpty(
            "a FeatureOptions security flag whose property initialiser is the insecure value turns every host " +
            "that omits the key into an unauthenticated, plaintext or unthrottled service with no error (SEC-007). " +
            "Default it secure in code (e.g. `= true`) and let Development/Testing overlays switch it off. " +
            "Do not add it to KnownViolations, which may only shrink. Offenders: " + string.Join(", ", offenders));
    }

    [Fact]
    public void EveryBaselineEntry_StillDefaultsInsecure()
    {
        var stale = KnownViolations.Except(InsecureClassDefaults(), StringComparer.Ordinal).Order().ToArray();

        stale.ShouldBeEmpty(
            "these FeatureOptions flags now default to the secure value, so delete them from KnownViolations " +
            "to lock the fix in (DRK-1903): " + string.Join(", ", stale));
    }
}
