using System.Linq.Expressions;
using Minimal.Domains.Share;

namespace Minimal.AppServices.Share.Generics;

/// <summary>
/// Extension methods for building reusable predicates from <see cref="GenericListParameters"/>.
/// </summary>
public static class ModelSpecGenericListExtensions
{
    /// <summary>
    /// Creates a date-range predicate that filters by <see cref="DomainEntity.CreatedOn"/> or <see cref="DomainEntity.UpdatedOn"/>.
    /// </summary>
    /// <typeparam name="TEntity">The entity type to filter.</typeparam>
    /// <param name="parameters">The list parameters containing optional <c>From</c> and <c>To</c> values.</param>
    /// <returns>
    /// A predicate when both range values are provided; otherwise <see langword="null"/>.
    /// </returns>
    public static Expression<Func<TEntity, bool>>? CreateDateRangePredicate<TEntity>(
        this GenericListParameters parameters)
        where TEntity : DomainEntity
    {
        if (!parameters.From.HasValue || !parameters.To.HasValue)
            return null;

        return x => (x.CreatedOn >= parameters.From && x.CreatedOn <= parameters.To) ||
                    (x.UpdatedOn >= parameters.From && x.UpdatedOn <= parameters.To);
    }
}
