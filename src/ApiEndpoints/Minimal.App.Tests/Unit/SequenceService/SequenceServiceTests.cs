using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Minimal.Infra.Contexts;
using Minimal.Infra.Services;

namespace Minimal.App.Tests.Unit.SequenceService;

public class SequenceServiceTests
{
    [Fact]
    public async Task MembershipService_WhenInMemory_ShouldReturnGuid()
    {
        var services = new ServiceCollection();
        var dbName = $"seq-{Guid.NewGuid():N}";

        services.AddDbContext<CoreDbContext>(options =>
            options.UseInMemoryDatabase(dbName));

        var provider = services.BuildServiceProvider();
        await using var dbContext = provider.GetRequiredService<CoreDbContext>();
        await dbContext.Database.EnsureCreatedAsync();

        var service = new MembershipService(dbContext);
        var result = await service.NextValueAsync();

        result.ShouldNotBeNull();
        result.ShouldNotBeEmpty();

        Guid.TryParse(result, out var parsed).ShouldBeTrue(
            $"InMemory fallback should return a GUID, got: {result}");
    }

    [Fact]
    public async Task MembershipService_WhenInMemory_ShouldReturnUniqueValues()
    {
        var services = new ServiceCollection();
        var dbName = $"seq-{Guid.NewGuid():N}";

        services.AddDbContext<CoreDbContext>(options =>
            options.UseInMemoryDatabase(dbName));

        var provider = services.BuildServiceProvider();
        await using var dbContext = provider.GetRequiredService<CoreDbContext>();
        await dbContext.Database.EnsureCreatedAsync();

        var service = new MembershipService(dbContext);

        var values = new HashSet<string>();
        for (var i = 0; i < 10; i++)
        {
            var result = await service.NextValueAsync();
            values.Add(result);
        }

        values.Count.ShouldBe(10, "Each NextValueAsync call should return a unique GUID.");
    }

    [Fact]
    public async Task MembershipService_WhenInMemory_ShouldNotThrow()
    {
        var services = new ServiceCollection();
        var dbName = $"seq-{Guid.NewGuid():N}";

        services.AddDbContext<CoreDbContext>(options =>
            options.UseInMemoryDatabase(dbName));

        var provider = services.BuildServiceProvider();
        await using var dbContext = provider.GetRequiredService<CoreDbContext>();
        await dbContext.Database.EnsureCreatedAsync();

        var service = new MembershipService(dbContext);

        await Should.NotThrowAsync(async () => await service.NextValueAsync());
    }
}