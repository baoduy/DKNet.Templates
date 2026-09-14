using System.Xml.Linq;

namespace Minimal.App.Tests.Architecture.Guards;

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
        => throw new NotImplementedException();

    /// <summary>Declared ids with no generated-guid symbol in template.json's "symbols" object replacing them.</summary>
    internal static IReadOnlyList<string> IdsWithoutGeneratedGuidSymbol(
        IEnumerable<string> declaredIds,
        JsonElement templateJsonSymbols)
        => throw new NotImplementedException();
}
