namespace Minimal.App.Tests.Architecture.Guards;

/// <summary>Result of checking the Entra-shaped bearer sections found across a set of appsettings files.</summary>
internal sealed record EntraPlaceholderResult(int EntraSectionsChecked, IReadOnlyList<string> Offenders);

/// <summary>
/// R2-R5 (DRK-1233): the template-symbol invariant reads only bearer sections that are actually
/// Entra-shaped, and never throws on a section missing an expected property.
/// </summary>
internal static class EntraPlaceholderGuard
{
    /// <summary>A bearer section is Entra-shaped iff it carries a MetadataAddress property.</summary>
    internal static bool IsEntraShaped(JsonElement bearer) => throw new NotImplementedException();

    /// <summary>Checks Entra-shaped sections only. Offender format: "&lt;fileName&gt;: &lt;reason&gt;".</summary>
    internal static EntraPlaceholderResult Check(
        IEnumerable<(string Path, JsonElement Bearer)> bearerSections,
        string tenantIdReplaces,
        string apiAudienceReplaces)
        => throw new NotImplementedException();
}
