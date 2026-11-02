using Minimal.Domains.Share;

namespace Minimal.App.Tests.Unit.Generics;

/// <summary>
/// A minimal <see cref="AggregateRoot"/> double that exposes both stamping constructors, so a test can pass a
/// fixed <c>createdOn</c> that no feature aggregate forwards.
/// </summary>
file sealed class ProbeAggregate : AggregateRoot
{
    public ProbeAggregate(string createdBy, DateTimeOffset? createdOn)
        : base(createdBy, createdOn)
    {
    }

    public ProbeAggregate(Guid id, string createdBy, DateTimeOffset? createdOn)
        : base(id, createdBy, createdOn)
    {
    }
}

/// <summary>
/// DRK-2491: characterizes how both <see cref="AggregateRoot"/> stamping constructors set the audit fields, so
/// removing the redundant <c>SetCreatedBy</c> call in the constructor chain cannot change them.
/// </summary>
public class AggregateRootTests
{
    #region Methods

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AggregateRootCtor_ShouldStampCreatedByAndCreatedOn(bool withExplicitId)
    {
        var createdOn = new DateTimeOffset(2026, 3, 14, 9, 26, 53, TimeSpan.FromHours(7));

        var aggregate = withExplicitId
            ? new ProbeAggregate(new Guid("0190f1a2-7b3c-7d4e-8f50-6a7b8c9d0e1f"), "alice", createdOn)
            : new ProbeAggregate("alice", createdOn);

        aggregate.CreatedBy.ShouldBe("alice");
        aggregate.CreatedOn.ShouldBe(new DateTimeOffset(2026, 3, 14, 9, 26, 53, TimeSpan.FromHours(7)));
    }

    [Theory]
    [InlineData(null, typeof(ArgumentNullException))]
    [InlineData("", typeof(ArgumentException))]
    [InlineData("   ", typeof(ArgumentException))]
    public void AggregateRootCtor_ShouldRejectBlankCreatedBy(string? createdBy, Type expectedException)
    {
        var thrown = Should.Throw<ArgumentException>(() => new ProbeAggregate(createdBy!, null));

        thrown.GetType().ShouldBe(expectedException);
    }

    #endregion
}
