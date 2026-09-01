using DKNet.EfCore.DtoGenerator;
using Minimal.Domains.Features.AutomatedSample.Entities;

namespace Minimal.AppServices.AutomatedSample.V1;

/// <summary>DTO returned by the generated <see cref="Product"/> CRUD endpoints.</summary>
/// <remarks><see cref="Product.OwnedBy"/> is the same ownership key already exposed as <c>CreatedBy</c> —
/// excluded here so the API surface doesn't carry it twice.</remarks>
[GenerateDto(typeof(Product), Exclude = [nameof(Product.OwnedBy)])]
public sealed partial record ProductDto;
