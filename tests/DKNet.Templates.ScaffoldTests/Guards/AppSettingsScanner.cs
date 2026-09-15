namespace DKNet.Templates.ScaffoldTests;

/// <summary>
/// DRK-1257 §3 row 4: the appsettings*.json Bearer-section scan used by both
/// <c>AuthPlaceholderConfigTests</c> (repo-side content checks) and the relocated
/// template.json-symbol check in <c>Architecture/TemplateRepo</c> — shared so the file-scan logic
/// is not duplicated between them.
/// </summary>
internal static class AppSettingsScanner
{
    /// <summary>Every appsettings*.json file's Authentication:Schemes:Bearer section under <paramref name="srcDir" />, skipping bin/obj.</summary>
    internal static IEnumerable<(string Path, JsonElement Bearer)> BearerSectionsInAppSettings(string srcDir)
    {
        var files = Directory.GetFiles(srcDir, "appsettings*.json", SearchOption.AllDirectories)
            .Where(f => !f.Split(Path.DirectorySeparatorChar).Any(seg =>
                seg.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
                seg.Equals("obj", StringComparison.OrdinalIgnoreCase)))
            .ToArray();
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
}
