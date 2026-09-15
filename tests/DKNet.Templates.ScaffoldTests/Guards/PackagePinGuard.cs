using System.Xml.Linq;

namespace DKNet.Templates.ScaffoldTests;

/// <summary>
/// R1 (DRK-1233): the DKNet pin invariant is that every DKNet.* package agrees on one release,
/// never that the release matches a hardcoded literal.
/// </summary>
internal static class PackagePinGuard
{
    /// <summary>Distinct Version values of every PackageVersion whose Include starts with "DKNet.".</summary>
    internal static IReadOnlyList<string> DistinctDkNetVersions(XDocument directoryPackagesProps)
        => directoryPackagesProps.Descendants("PackageVersion")
            .Where(e => (e.Attribute("Include")?.Value ?? "").StartsWith("DKNet.", StringComparison.Ordinal))
            .Select(e => e.Attribute("Version")?.Value ?? "")
            .Distinct()
            .ToList();
}
