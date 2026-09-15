using System.Xml.Linq;

namespace DKNet.Templates.ScaffoldTests;

/// <summary>A template.json symbol entry relevant to the user-secrets invariant.</summary>
internal sealed record UserSecretsSymbol(string Replaces, string? Type, string? Generator);

/// <summary>
/// R8 (DRK-1233): every UserSecretsId declared by a csproj must be the replace target of a
/// generated-guid template.json symbol, so generated projects each get an independent secrets store.
/// </summary>
internal static class UserSecretsGuard
{
    /// <summary>Every non-empty &lt;UserSecretsId&gt; value declared across the given csproj documents.</summary>
    internal static IReadOnlyList<string> DeclaredUserSecretsIds(IEnumerable<XDocument> csprojDocuments)
        => csprojDocuments
            .SelectMany(doc => doc.Descendants("UserSecretsId"))
            .Select(e => e.Value)
            .Where(v => !string.IsNullOrEmpty(v))
            .ToList();

    /// <summary>Declared ids with no generated-guid symbol in template.json's "symbols" object replacing them.</summary>
    internal static IReadOnlyList<string> IdsWithoutGeneratedGuidSymbol(
        IEnumerable<string> declaredIds,
        JsonElement templateJsonSymbols)
    {
        var generatedGuidReplaces = templateJsonSymbols
            .EnumerateObject()
            .Select(p => p.Value)
            .Where(symbol =>
                symbol.TryGetProperty("type", out var type) && type.GetString() == "generated" &&
                symbol.TryGetProperty("generator", out var generator) && generator.GetString() == "guid" &&
                symbol.TryGetProperty("replaces", out _))
            .Select(symbol => symbol.GetProperty("replaces").GetString() ?? "")
            .ToHashSet(StringComparer.Ordinal);

        return declaredIds.Where(id => !generatedGuidReplaces.Contains(id)).ToList();
    }
}
