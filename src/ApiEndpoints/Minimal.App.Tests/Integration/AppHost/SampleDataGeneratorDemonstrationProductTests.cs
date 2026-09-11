using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Minimal.App.TestSupport;
using Minimal.AppHost.SampleData;
using Minimal.Domains.Features.AutomatedSample.Entities;
using Minimal.Domains.Share;
using Minimal.Infra.Contexts;
using Minimal.Infra.Extensions;
using Testcontainers.PostgreSql;

namespace Minimal.App.Tests.Integration.AppHost;

/// <summary>
/// DRK-1219 R4/R5: a developer calling <c>GET /v1/products</c> against the Aspire AppHost must be able to
/// see both role-gated <c>[SensitiveData]</c> properties on at least one seeded row — before this change
/// every generated product left <c>SupplierCostPrice</c>/<c>SupplierReferenceCode</c> null. Pins that
/// exactly one seeded product carries both, and that the demonstration row still counts toward (never adds
/// to) the requested <c>recordsPerEntity</c> total.
/// </summary>
public sealed class SampleDataGeneratorDemonstrationProductTests
{
    [Fact]
    public async Task GivenEmptyDatabase_WhenGeneratingTwentyFiveRecords_ThenExactlyOneProductHasBothSensitiveValuesAndTotalIsTwentyFive()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
        await postgres.StartAsync();
        Npgsql.NpgsqlConnection.ClearAllPools();
        await InfraMigration.MigrateDb(postgres.GetConnectionString());

        var logCapture = new TestLogCapture();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(logCapture));
        var logger = loggerFactory.CreateLogger("SampleDataGenerator");

        await SampleDataGenerator.RunAsync(postgres.GetConnectionString(), 25, logger, CancellationToken.None);

        await using var verifyDb = new CoreDbContext(
            new DbContextOptionsBuilder<CoreDbContext>()
                .UseAutoConfigModel([typeof(CoreDbContext).Assembly, typeof(Sequences).Assembly])
                .UseNpgsql(postgres.GetConnectionString())
                .Options);
        var products = await verifyDb.Set<Product>().IgnoreQueryFilters().ToListAsync();

        products.Count.ShouldBe(25, "the demonstration product counts toward recordsPerEntity, not in addition to it");

        var withBothSensitiveValues = products
            .Where(p => p.SupplierCostPrice is not null && p.SupplierReferenceCode is not null)
            .ToList();
        withBothSensitiveValues.Count.ShouldBe(1,
            "exactly one seeded product must demonstrate both role-gated sensitive properties together");
        withBothSensitiveValues[0].SupplierReferenceCode.ShouldBe(SampleDataGenerator.DemonstrationProductSupplierReferenceCode);
    }

    [Fact]
    public async Task GivenEmptyDatabase_WhenGeneratingOneRecord_ThenTheSingleProductIsTheDemonstrationProduct()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
        await postgres.StartAsync();
        Npgsql.NpgsqlConnection.ClearAllPools();
        await InfraMigration.MigrateDb(postgres.GetConnectionString());

        var logCapture = new TestLogCapture();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(logCapture));
        var logger = loggerFactory.CreateLogger("SampleDataGenerator");

        await SampleDataGenerator.RunAsync(postgres.GetConnectionString(), 1, logger, CancellationToken.None);

        await using var verifyDb = new CoreDbContext(
            new DbContextOptionsBuilder<CoreDbContext>()
                .UseAutoConfigModel([typeof(CoreDbContext).Assembly, typeof(Sequences).Assembly])
                .UseNpgsql(postgres.GetConnectionString())
                .Options);
        var products = await verifyDb.Set<Product>().IgnoreQueryFilters().ToListAsync();

        products.Count.ShouldBe(1);
        products[0].Name.ShouldBe(SampleDataGenerator.DemonstrationProductName);
        products[0].SupplierCostPrice.ShouldNotBeNull();
        products[0].SupplierReferenceCode.ShouldBe(SampleDataGenerator.DemonstrationProductSupplierReferenceCode);
    }
}
