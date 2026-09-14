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
    internal static bool IsEntraShaped(JsonElement bearer) => bearer.TryGetProperty("MetadataAddress", out _);

    /// <summary>Checks Entra-shaped sections only. Offender format: "&lt;fileName&gt;: &lt;reason&gt;".</summary>
    internal static EntraPlaceholderResult Check(
        IEnumerable<(string Path, JsonElement Bearer)> bearerSections,
        string tenantIdReplaces,
        string apiAudienceReplaces)
    {
        var checkedCount = 0;
        var offenders = new List<string>();

        foreach (var (path, bearer) in bearerSections)
        {
            if (!IsEntraShaped(bearer)) continue;
            checkedCount++;
            var fileName = Path.GetFileName(path);

            var metadataAddress = bearer.GetProperty("MetadataAddress").GetString() ?? "";
            if (!metadataAddress.Contains(tenantIdReplaces, StringComparison.Ordinal))
                offenders.Add($"{fileName}: MetadataAddress does not contain the tenant id placeholder");

            if (!bearer.TryGetProperty("ValidIssuer", out var validIssuerElement))
            {
                offenders.Add($"{fileName}: ValidIssuer is missing");
            }
            else
            {
                var validIssuer = validIssuerElement.GetString() ?? "";
                if (!validIssuer.Contains(tenantIdReplaces, StringComparison.Ordinal))
                    offenders.Add($"{fileName}: ValidIssuer does not contain the tenant id placeholder");
            }

            if (!bearer.TryGetProperty("ValidAudiences", out var validAudiencesElement) ||
                validAudiencesElement.ValueKind != JsonValueKind.Array)
            {
                offenders.Add($"{fileName}: ValidAudiences is missing");
            }
            else
            {
                var audiences = validAudiencesElement.EnumerateArray().Select(a => a.GetString() ?? "");
                if (!audiences.Contains(apiAudienceReplaces, StringComparer.Ordinal))
                    offenders.Add($"{fileName}: ValidAudiences does not contain the api audience placeholder");
            }
        }

        return new EntraPlaceholderResult(checkedCount, offenders);
    }
}
