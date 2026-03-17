using DKNet.EfCore.DtoGenerator;

namespace SlimBus.AppServices.Profiles.V1;

[GenerateDto(typeof(CustomerProfile))]
public sealed partial record CustomerProfileDto;