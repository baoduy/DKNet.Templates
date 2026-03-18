using DKNet.EfCore.Specifications;
using Minimal.Domains.Share;

namespace Minimal.AppServices.Share.Generics;

/// <summary>
/// A generic specification that filters an entity by its identifier.
/// </summary>
/// <typeparam name="TEntity">The entity type to filter.</typeparam>
/// <typeparam name="TModel">The model type projected by the specification.</typeparam>
public class ModelSpecGenericById<TEntity, TModel> : ModelSpecification<TEntity, TModel>
    where TEntity : DomainEntity where TModel : class
{
    /// <summary>
    /// Initializes a new specification that matches the provided entity identifier.
    /// </summary>
    /// <param name="id">The identifier to match.</param>
    public ModelSpecGenericById(Guid id)
    {
        WithFilter(x => x.Id == id);
    }
}