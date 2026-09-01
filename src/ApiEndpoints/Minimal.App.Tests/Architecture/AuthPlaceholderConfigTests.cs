using System.Text.RegularExpressions;

namespace Minimal.App.Tests.Architecture;

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
        Path.Combine(AppContext.BaseDirectory, "../../../../.."));

    private static IEnumerable<(string Path, JsonElement Bearer)> BearerSectionsInAppSettings()
    {
        var files = Directory.GetFiles(SrcDir, "appsettings*.json", SearchOption.AllDirectories);
        files.ShouldNotBeEmpty();

        foreach (var file in files)
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(file));
            if (doc.RootElement.TryGetProperty("Authentication", out var auth) &&
                auth.TryGetProperty("Schemes", out var schemes) &&
                schemes.TryGetProperty("Bearer", out var bearer))
            {
                yield return (file, bearer.Clone());
            }
        }
    }

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

    [Fact]
    public void TemplateJson_TenantIdAndApiAudienceSymbols_ShouldMatchAppSettingsPlaceholders()
    {
        var templateJsonPath = Path.Combine(SrcDir, ".template.config", "template.json");
        File.Exists(templateJsonPath).ShouldBeTrue();

        using var templateDoc = JsonDocument.Parse(File.ReadAllText(templateJsonPath));
        var symbols = templateDoc.RootElement.GetProperty("symbols");

        var tenantIdReplaces = symbols.GetProperty("TenantId").GetProperty("replaces").GetString();
        var apiAudienceReplaces = symbols.GetProperty("ApiAudience").GetProperty("replaces").GetString();

        tenantIdReplaces.ShouldNotBeNullOrWhiteSpace();
        apiAudienceReplaces.ShouldNotBeNullOrWhiteSpace();

        var checkedAtLeastOneFile = false;

        foreach (var (path, bearer) in BearerSectionsInAppSettings())
        {
            checkedAtLeastOneFile = true;

            var metadataAddress = bearer.GetProperty("MetadataAddress").GetString() ?? "";
            var validIssuer = bearer.GetProperty("ValidIssuer").GetString() ?? "";
            metadataAddress.Contains(tenantIdReplaces!, StringComparison.Ordinal).ShouldBeTrue(
                $"{Path.GetFileName(path)}: TenantId symbol no longer matches MetadataAddress");
            validIssuer.Contains(tenantIdReplaces!, StringComparison.Ordinal).ShouldBeTrue(
                $"{Path.GetFileName(path)}: TenantId symbol no longer matches ValidIssuer");

            var audiences = bearer.GetProperty("ValidAudiences")
                .EnumerateArray()
                .Select(a => a.GetString())
                .ToArray();
            audiences.ShouldContain(apiAudienceReplaces,
                $"{Path.GetFileName(path)}: ApiAudience symbol no longer matches any ValidAudiences entry");
        }

        checkedAtLeastOneFile.ShouldBeTrue("expected at least one appsettings*.json with a Bearer section");
    }
}
