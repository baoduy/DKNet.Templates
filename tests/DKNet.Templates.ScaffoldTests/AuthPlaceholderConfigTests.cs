using System.Text.RegularExpressions;

namespace DKNet.Templates.ScaffoldTests;

/// <summary>
/// DRK-902: Authentication:Schemes:Bearer in shipped appsettings*.json must carry only the
/// all-zero placeholder tenant/audience, never a real Entra tenant or Microsoft Graph audience —
/// and the template.json symbols that substitute those placeholders must still match the file.
/// </summary>
public class AuthPlaceholderConfigTests
{
    private const string PlaceholderTenantGuid = "00000000-0000-0000-0000-000000000000";
    private static readonly Regex GuidPattern = new(
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b",
        RegexOptions.Compiled);

    private static string SrcDir => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "../../../../../src"));

    // Guard shipped source config only — skip bin/obj so stale build-output copies (e.g. orphaned
    // net9.0 artifacts) don't produce false failures. Shared with the relocated template.json-symbol
    // check in Architecture/TemplateRepo (DRK-1257 §3 row 4).
    private static IEnumerable<(string Path, JsonElement Bearer)> BearerSectionsInAppSettings() =>
        AppSettingsScanner.BearerSectionsInAppSettings(SrcDir);

    [Fact]
    public void AppSettings_ValidAudiences_ShouldNeverTargetMicrosoftComHost()
    {
        var offenders = new List<string>();

        foreach (var (path, bearer) in BearerSectionsInAppSettings())
        {
            if (!bearer.TryGetProperty("ValidAudiences", out var audiences)) continue;

            foreach (var audience in audiences.EnumerateArray().Select(a => a.GetString() ?? ""))
            {
                var host = Uri.TryCreate(audience, UriKind.Absolute, out var uri) ? uri.Host : "";
                if (host.Equals("microsoft.com", StringComparison.OrdinalIgnoreCase) ||
                    host.EndsWith(".microsoft.com", StringComparison.OrdinalIgnoreCase))
                {
                    offenders.Add($"{Path.GetFileName(path)}: {audience}");
                }
            }
        }

        offenders.ShouldBeEmpty(
            "ValidAudiences must never contain a microsoft.com host: " + string.Join(", ", offenders));
    }

    [Fact]
    public void AppSettings_TenantFields_ShouldContainOnlyThePlaceholderGuid()
    {
        var offenders = new List<string>();

        foreach (var (path, bearer) in BearerSectionsInAppSettings())
        {
            foreach (var field in new[] { "MetadataAddress", "ValidIssuer" })
            {
                if (!bearer.TryGetProperty(field, out var valueElement)) continue;
                var value = valueElement.GetString() ?? "";

                var nonPlaceholderGuids = GuidPattern.Matches(value)
                    .Select(m => m.Value)
                    .Where(g => !g.Equals(PlaceholderTenantGuid, StringComparison.OrdinalIgnoreCase));

                offenders.AddRange(nonPlaceholderGuids.Select(g => $"{Path.GetFileName(path)}:{field}={g}"));
            }
        }

        offenders.ShouldBeEmpty(
            "MetadataAddress/ValidIssuer must carry only the placeholder tenant guid, found real tenant(s): " +
            string.Join(", ", offenders));
    }
}
