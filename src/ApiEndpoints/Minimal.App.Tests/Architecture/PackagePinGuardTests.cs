using System.Xml.Linq;
using Minimal.App.Tests.Architecture.Guards;

namespace Minimal.App.Tests.Architecture;

/// <summary>DRK-1233 R1: synthetic-XML scenarios for <see cref="PackagePinGuard"/>.</summary>
public class PackagePinGuardTests
{
    private static XDocument PropsWith(params (string Include, string? Version)[] packages)
    {
        var itemGroup = new XElement("ItemGroup",
            packages.Select(p =>
            {
                var element = new XElement("PackageVersion", new XAttribute("Include", p.Include));
                if (p.Version is not null) element.Add(new XAttribute("Version", p.Version));
                return element;
            }));

        return new XDocument(new XElement("Project", itemGroup));
    }

    [Fact]
    public void PinsThatAllAgree_ReportOneRelease_WhateverTheReleaseIs()
    {
        var doc = PropsWith(
            ("DKNet.Fw.Extensions", "99.9.99"),
            ("DKNet.EfCore.Events", "99.9.99"));

        var result = PackagePinGuard.DistinctDkNetVersions(doc);

        result.ShouldBe(["99.9.99"]);
    }

    [Fact]
    public void PinsStraddlingTwoReleases_AreReported()
    {
        var doc = PropsWith(
            ("DKNet.Fw.Extensions", "10.1.24"),
            ("DKNet.EfCore.Events", "10.1.21"));

        var result = PackagePinGuard.DistinctDkNetVersions(doc);

        result.ShouldBe(["10.1.24", "10.1.21"]);
    }

    [Fact]
    public void ADKNetEntryWithNoVersionAttribute_IsNotSilentlyDropped()
    {
        var doc = PropsWith(
            ("DKNet.Fw.Extensions", "10.1.24"),
            ("DKNet.EfCore.Events", null));

        var result = PackagePinGuard.DistinctDkNetVersions(doc);

        result.ShouldBe(["10.1.24", ""]);
    }

    [Fact]
    public void NonDKNetPins_AreIgnored()
    {
        var doc = PropsWith(
            ("DKNet.Fw.Extensions", "10.1.24"),
            ("Bogus", "35.6.5"));

        var result = PackagePinGuard.DistinctDkNetVersions(doc);

        result.ShouldBe(["10.1.24"]);
    }
}
