using DKNet.EfCore.DtoGenerator;
using Minimal.Domains.Features.AutomatedSample.Entities;

namespace Minimal.AppServices.AutomatedSample.V1;

/// <summary>DTO returned by the generated <see cref="Product"/> CRUD endpoints.</summary>
[GenerateDto(typeof(Product))]
public sealed partial record ProductDto;
