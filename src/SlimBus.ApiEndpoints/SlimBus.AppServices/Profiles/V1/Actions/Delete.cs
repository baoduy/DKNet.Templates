using DKNet.EfCore.Specifications;
using DKNet.EfCore.Specifications.Extensions;
using SlimBus.AppServices.Profiles.V1.Specs;

namespace SlimBus.AppServices.Profiles.V1.Actions;

public record DeleteProfileCommand : BaseCommand, Fluents.Requests.INoResponse
{
    #region Properties

    public required Guid Id { get; init; }

    #endregion
}

internal sealed class DeleteProfileCommandHandler(IRepositorySpec repository)
    : Fluents.Requests.IHandler<DeleteProfileCommand>
{
    #region Methods

    public async Task<IResultBase> OnHandle(DeleteProfileCommand request, CancellationToken cancellationToken)
    {
        if (request.Id == Guid.Empty)
        {
            return Result.Fail("The Id is in valid.")
                .WithError(new Error("The Id is in valid.") { Metadata = { ["field"] = nameof(request.Id) } });
        }

        var profile = await repository.FirstOrDefaultAsync(new SpecGetCustomerProfile(request.Id), cancellationToken);

        if (profile == null)
        {
            return Result.Fail($"The Profile {request.Id} is not found.");
        }

        repository.Delete(profile);

        return Result.Ok();
    }

    #endregion
}