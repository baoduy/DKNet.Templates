using DKNet.EfCore.DataAuthorization;
using Minimal.Domains.Share;

namespace DKNet.Templates.ScaffoldTests;

/// <summary>
/// DRK-1901 (SEC-001): every aggregate root the template ships must be row-level isolated. DKNet's
/// <c>DataOwnerAuthQuery</c> only filters entity types that implement <see cref="IOwnedBy" />, so an aggregate
/// without the marker is readable and writable by every authenticated caller of every tenant — and the samples
/// are what each new service copies for its first real aggregate.
/// <para>
/// Tier-2 guard (architecture review DRK-1896): <see cref="KnownViolations" /> is today's baseline and may only
/// shrink. Resolving DRK-1901 — by making the aggregate <see cref="IOwnedBy" />, or by the owner recording that
/// it is deliberately tenant-global (keep the entry and say so next to it) — settles the one entry;
/// <see cref="EveryBaselineEntry_StillLacksOwnership" /> fails if an entry goes stale.
/// </para>
/// </summary>
public class AggregateOwnershipTests
{
    /// <summary>Today's offenders by full type name (DRK-1901). Only ever remove entries.</summary>
    private static readonly HashSet<string> KnownViolations = new(StringComparer.Ordinal)
    {
        "Minimal.Domains.Features.ManualSample.Entities.PurchaseOrder",
    };

    private static Type[] ConcreteAggregates() =>
    [
        .. typeof(AggregateRoot).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.IsSubclassOf(typeof(AggregateRoot)))
    ];

    private static string[] UnownedAggregates() =>
    [
        .. ConcreteAggregates().Where(t => !typeof(IOwnedBy).IsAssignableFrom(t)).Select(t => t.FullName!)
    ];

    [Fact]
    public void EveryShippedAggregateRoot_MustImplementIOwnedBy()
    {
        var offenders = UnownedAggregates().Where(t => !KnownViolations.Contains(t)).Order().ToArray();

        offenders.ShouldBeEmpty(
            "an aggregate root without IOwnedBy gets no DataOwnerAuthQuery filter, so every authenticated caller " +
            "can list, read and change every tenant's rows (SEC-001) — and teams copy these samples verbatim. " +
            "Implement IOwnedBy (map OwnedBy, seed an explicit owner for static data). Do not add the type to " +
            "KnownViolations, which may only shrink. Offenders: " + string.Join(", ", offenders));
    }

    [Fact]
    public void EveryBaselineEntry_StillLacksOwnership()
    {
        var stale = KnownViolations.Except(UnownedAggregates(), StringComparer.Ordinal).Order().ToArray();

        stale.ShouldBeEmpty(
            "these aggregates now implement IOwnedBy (or no longer exist), so delete them from KnownViolations " +
            "to lock the fix in (DRK-1901): " + string.Join(", ", stale));
    }

    [Fact]
    public void Rule_ScansAtLeastOneAggregateRoot()
    {
        // Self-check: if AggregateRoot moves assembly the scan would find nothing and the rule pass vacuously.
        ConcreteAggregates().ShouldNotBeEmpty(
            "the ownership rule must scan the assembly that holds the shipped aggregate roots.");
    }
}
