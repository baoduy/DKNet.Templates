using DKNet.EfCore.Specifications;
using DKNet.EfCore.Specifications.Extensions;
using SlimBus.App.Tests.Integration.Support;
using SlimBus.AppServices.CustomerProfiles.V1;
using SlimBus.AppServices.CustomerProfiles.V1.Actions;
using SlimBus.AppServices.CustomerProfiles.V1.Specs;
using SlimBus.Domains.Features.Profiles.Entities;
using SlimMessageBus;

namespace SlimBus.App.Tests.Integration.Profiles.V1;

public sealed class CustomerProfileActionsIntegrationTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    #region Methods

    [Fact]
    public void Test_CustomerProvide_Mapping()
    {
        //create dummy CustomerProfile
        var profile = new AutoFaker<CustomerProfile>()
            .CustomInstantiator(f => new CustomerProfile(
                f.Name.FullName(),
                f.Random.Replace("MS-####"),
                f.Internet.Email(),
                f.Phone.PhoneNumber("+65#########"),
                f.Internet.UserName()))
            .Generate();

        var mapper = fixture.Services.GetRequiredService<IMapper>();
        var dto = mapper.Map<CustomerProfileDto>(profile);
        dto.ShouldNotBeNull();
    }

    [Fact]
    public async Task CreateActionShouldResolveFromDiAndPersistProfile()
    {
        await fixture.ResetDatabaseAsync();

        using var scope = fixture.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var repository = scope.ServiceProvider.GetRequiredService<IRepositorySpec>();
        scope.ServiceProvider.GetRequiredService<IMapper>().ShouldNotBeNull();

        var request = new CreateProfileRequest
        {
            Email = "integration.create@example.com",
            Name = "Integration Create",
            Phone = "+6512345678",
            ByUser = "integration-test"
        };

        var result = await bus.Send(request);

        result.IsSuccess.ShouldBeTrue();
        request.MembershipNo.ShouldStartWith("TEST-MEM-");
        await repository.SaveChangesAsync(CancellationToken.None);

        var created = await repository.FirstOrDefaultAsync(
            new SpecGetCustomerProfile(byEmail: request.Email),
            CancellationToken.None);

        created.ShouldNotBeNull();
        created.Name.ShouldBe(request.Name);
        created.Phone.ShouldBe(request.Phone);
    }

    [Fact]
    public async Task CreateActionShouldFailWhenEmailAlreadyExists()
    {
        await fixture.ResetDatabaseAsync();

        using var scope = fixture.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var repository = scope.ServiceProvider.GetRequiredService<IRepositorySpec>();

        await repository.AddAsync(
            new CustomerProfile("Existing", "MS-0001", "integration.dup@example.com", "+6599988877", "seed"),
            CancellationToken.None);
        await repository.SaveChangesAsync(CancellationToken.None);

        var request = new CreateProfileRequest
        {
            Email = "integration.dup@example.com",
            Name = "Duplicate",
            Phone = "+6599911122",
            ByUser = "integration-test"
        };

        var result = await bus.Send(request);

        result.IsFailed.ShouldBeTrue();
        result.Errors.Select(x => x.Message).ShouldContain("Email integration.dup@example.com is already existed.");
    }

    [Fact]
    public async Task UpdateActionShouldResolveFromDiAndUpdateEntity()
    {
        await fixture.ResetDatabaseAsync();

        using var scope = fixture.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var repository = scope.ServiceProvider.GetRequiredService<IRepositorySpec>();

        var profile = new CustomerProfile("Before", "MS-0100", "integration.update@example.com", "+6500000001", "seed");
        await repository.AddAsync(profile, CancellationToken.None);
        await repository.SaveChangesAsync(CancellationToken.None);

        var request = new UpdateProfileRequest
        {
            Id = profile.Id,
            Name = "After",
            Phone = "+6500000002",
            ByUser = "integration-test"
        };

        var result = await bus.Send(request);

        result.IsSuccess.ShouldBeTrue();

        var updated = await repository.FirstOrDefaultAsync(
            new SpecGetCustomerProfile(profile.Id),
            CancellationToken.None);

        updated.ShouldNotBeNull();
        updated.Name.ShouldBe("After");
        updated.Phone.ShouldBe("+6500000002");
    }

    [Fact]
    public async Task DeleteActionShouldResolveFromDiAndDeleteEntity()
    {
        await fixture.ResetDatabaseAsync();

        using var scope = fixture.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var repository = scope.ServiceProvider.GetRequiredService<IRepositorySpec>();

        var profile = new CustomerProfile("To Delete", "MS-0200", "integration.delete@example.com", "+6500000003", "seed");
        await repository.AddAsync(profile, CancellationToken.None);
        await repository.SaveChangesAsync(CancellationToken.None);

        var result = await bus.Send(new DeleteProfileRequest { Id = profile.Id });

        result.IsSuccess.ShouldBeTrue();
        await repository.SaveChangesAsync(CancellationToken.None);

        var deleted = await repository.FirstOrDefaultAsync(
            new SpecGetCustomerProfile(profile.Id),
            CancellationToken.None);

        deleted.ShouldBeNull();
    }

    [Fact]
    public async Task DeleteActionShouldFailWhenIdIsEmpty()
    {
        await fixture.ResetDatabaseAsync();

        using var scope = fixture.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var result = await bus.Send(new DeleteProfileRequest { Id = Guid.Empty });

        result.IsFailed.ShouldBeTrue();
        result.Errors.Select(x => x.Message).ShouldContain("The Id is in valid.");
    }

    [Fact]
    public async Task UpdateActionShouldFailWhenProfileIsMissing()
    {
        await fixture.ResetDatabaseAsync();

        using var scope = fixture.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var missingId = Guid.NewGuid();

        var result = await bus.Send(
            new UpdateProfileRequest
            {
                Id = missingId,
                Name = "No Profile",
                Phone = "+6500000004",
                ByUser = "integration-test"
            });

        result.IsFailed.ShouldBeTrue();
        result.Errors.Select(x => x.Message).ShouldContain($"The Profile {missingId} is not found.");
    }

    #endregion
}


