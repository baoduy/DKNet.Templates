using DKNet.EfCore.Abstractions.Entities;
using DKNet.EfCore.DtoGenerator;
using Minimal.Domains.Features.AutomatedSample.Entities;

namespace Minimal.AppServices.AutomatedSample.V1;

/// <summary>DTO returned by the generated <see cref="Product"/> CRUD endpoints.</summary>
/// <remarks>
/// <see cref="Product.OwnedBy"/> is the same ownership key already exposed as <c>CreatedBy</c> —
/// excluded here so the API surface doesn't carry it twice.
/// <para>
/// <c>LastModifiedBy</c>/<c>LastModifiedOn</c> are excluded too: on <see cref="AuditedEntity{TKey}"/> they
/// are computed conveniences (the updated value, or the created one if never modified), not mapped columns.
/// Left on the DTO they break the generic list route — its filter/search/order build EF predicates against
/// the entity by property name, and an unmapped member makes the whole query fail to translate. They
/// duplicate <c>UpdatedBy</c>/<c>UpdatedOn</c>, which stay.
/// </para>
/// </remarks>
[GenerateDto(typeof(Product),
    Exclude = [nameof(Product.OwnedBy), nameof(AuditedEntity<Guid>.LastModifiedBy), nameof(AuditedEntity<Guid>.LastModifiedOn)])]
public sealed partial record ProductDto;
