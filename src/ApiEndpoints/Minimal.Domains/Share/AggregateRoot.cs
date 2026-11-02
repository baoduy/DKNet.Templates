namespace Minimal.Domains.Share;

public abstract class AggregateRoot : DomainEntity
{
    #region Constructors

    protected AggregateRoot(string createdBy, DateTimeOffset? createdOn = null)
        : this(Guid.CreateVersion7(), createdBy, createdOn)
    {
    }

    protected AggregateRoot(Guid id, string createdBy, DateTimeOffset? createdOn = null)
        : base(id,createdBy, createdOn)
    {
    }

    /// <inheritdoc />
    protected AggregateRoot()
    {
    }

    #endregion
}