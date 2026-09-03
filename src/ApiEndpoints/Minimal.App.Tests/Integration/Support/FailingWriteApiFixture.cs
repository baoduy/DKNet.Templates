using DKNet.EfCore.Hooks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Minimal.App.TestSupport;
using Minimal.Domains.Services;
using Minimal.Infra.Contexts;

namespace Minimal.App.Tests.Integration.Support;

/// <summary>
/// <see cref="ApiFixture" /> variant whose store fails on every write, via
/// <see cref="ThrowingSaveChangesInterceptor" /> added directly on the <c>DbContextOptionsBuilder</c> (not
/// through DI auto-discovery, which needs the internal/external service-provider wiring
/// <c>AddDbContextWithHook</c> may or may not use) — so a create request reliably reaches
/// <c>GlobalExceptionHandler</c> as a genuine unhandled exception, for the "security headers survive a 500"
/// scenario.
/// </summary>
public sealed class FailingWriteApiFixture : TestApiFactoryBase, IAsyncLifetime
{
    private readonly string _dbName = $"failing-write-{Guid.NewGuid():N}";

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.RemoveAll<IDbContextOptionsConfiguration<CoreDbContext>>();
        services.RemoveAll<IConfigureOptions<DbContextOptions<CoreDbContext>>>();
        services.RemoveAll<IPostConfigureOptions<DbContextOptions<CoreDbContext>>>();
        services.RemoveAll<DbContextOptions<CoreDbContext>>();
        services.RemoveAll<CoreDbContext>();

        services.AddDbContextWithHook<CoreDbContext>((_, options) => options
            .UseInMemoryDatabase(_dbName)
            .UseAutoConfigModel([typeof(CoreDbContext).Assembly])
            .AddInterceptors(new ThrowingSaveChangesInterceptor()));

        services.RemoveAll<IMembershipService>();
        services.AddSingleton<IMembershipService, TestMembershipService>();
    }

    public async Task InitializeAsync()
    {
        _ = CreateClient();
        await ResetDatabaseAsync();
    }

    Task IAsyncLifetime.DisposeAsync() => Task.CompletedTask;
}
