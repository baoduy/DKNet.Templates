using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Minimal.App.TestSupport;
using Minimal.AppHost.SampleData;
using Minimal.Domains.Features.AutomatedSample.Entities;
using Minimal.Domains.Features.ManualSample.Entities;
using Minimal.Domains.Share;
using Minimal.Infra.Contexts;
using Minimal.Infra.Extensions;
using Testcontainers.PostgreSql;

namespace Minimal.App.Tests.Integration.AppHost;

/// <summary>
/// DRK-1135 §3/§6's "retained database" invariant: the default local database is discarded between
/// runs, so accumulation on a retained one only shows up when a developer deliberately keeps the same
/// container across restarts — constructed here by running generation twice against the same schema.
/// Products and purchase orders retain at different thresholds by design (correction on this sub-task's
/// brief): products skip at &gt;= 1 existing row, purchase orders at &gt; 3 (the static seed's own count).
/// Each test also proves the guard does not merely skip *inserting* — it must leave whatever was already
/// there untouched.
/// </summary>
public sealed class SampleDataGeneratorRetentionTests
{
    [Fact]
    public async Task GivenAProductRowAlreadyExists_WhenGenerating_ThenNoProductsAreAddedAndTheExistingRowIsUnchanged()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
        await postgres.StartAsync();
        // Ephemeral containers can reuse a torn-down container's host port; clear Npgsql's
        // connection pools so a fresh container is never handed a stale pooled physical connection.
        Npgsql.NpgsqlConnection.ClearAllPools();
        var connectionString = postgres.GetConnectionString();
        await InfraMigration.MigrateDb(connectionString);

        // A hand-created row simulating a developer's own local data — owned by someone other than
        // the generator's SharedConsts.SystemAccount, so a false "it's just our own seed" pass is ruled out.
        await using (var db = NewCoreDbContext(connectionString))
        {
            var handMade = new Product("Hand-Crafted Widget", 42.00m);
            db.Add(handMade);
            var entry = db.Entry(handMade);
            entry.Property(nameof(Product.CreatedBy)).CurrentValue = "a-real-developer";
            entry.Property(nameof(Product.CreatedOn)).CurrentValue = DateTimeOffset.UtcNow;
            entry.Property(nameof(Product.OwnedBy)).CurrentValue = "a-real-developer";
            await db.SaveChangesAsync();
        }

        // A capturing logger, not an empty one: "count stayed at 1" alone would also hold if generation
        // crashed outright before ever reaching the retention check (exactly what DRK-1146 was) — the log
        // line is the only signal that distinguishes "the guard fired" from "nothing ran at all".
        var logCapture = new TestLogCapture();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(logCapture));
        await SampleDataGenerator.RunAsync(connectionString, 50, loggerFactory.CreateLogger("SampleDataGenerator"), CancellationToken.None);

        logCapture.Messages.ShouldContain(m =>
            m.Contains("Skipping product generation: existing rows found", StringComparison.Ordinal));
        logCapture.Messages.ShouldNotContain(m =>
            m.Contains("Sample-data generation failed and was skipped", StringComparison.Ordinal),
            "a swallowed exception must never be mistaken for the retention guard firing");

        await using var verifyDb = NewCoreDbContext(connectionString);
        var products = await verifyDb.Set<Product>().IgnoreQueryFilters().ToListAsync();

        products.Count.ShouldBe(1, "an existing product row must block generation entirely, not merely cap it");
        products[0].Name.ShouldBe("Hand-Crafted Widget");
        products[0].Price.ShouldBe(42.00m);
        products[0].OwnedBy.ShouldBe("a-real-developer", "the guard must not touch the pre-existing row's own columns");
    }

    [Fact]
    public async Task GivenARetainedDatabase_WhenGeneratingTwice_ThenPurchaseOrdersStopGrowingAfterTheFirstRunAndAllRowsSurvive()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
        await postgres.StartAsync();
        // Ephemeral containers can reuse a torn-down container's host port; clear Npgsql's
        // connection pools so a fresh container is never handed a stale pooled physical connection.
        Npgsql.NpgsqlConnection.ClearAllPools();
        var connectionString = postgres.GetConnectionString();
        // Seeds exactly the 3 PurchaseOrderStaticData reference orders — the count the ">3" threshold is defined against.
        await InfraMigration.MigrateDb(connectionString);

        var logCapture = new TestLogCapture();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(logCapture));
        var logger = loggerFactory.CreateLogger("SampleDataGenerator");

        // First run: 3 existing rows is not > 3, so generation proceeds and adds 50 more.
        await SampleDataGenerator.RunAsync(connectionString, 50, logger, CancellationToken.None);
        var afterFirstRun = await GetOrderIdsAsync(connectionString);
        afterFirstRun.Count.ShouldBe(53);
        logCapture.Messages.ShouldNotContain(m =>
            m.Contains("Sample-data generation failed and was skipped", StringComparison.Ordinal));

        // Second run against the same (retained) database: 53 existing rows is > 3, so it must skip entirely.
        // A capturing logger, not an empty one: "count stayed at 53" alone would also hold if this second
        // call crashed outright before reaching the retention check — the log line is what distinguishes
        // "the guard fired" from "nothing ran at all" (exactly the gap DRK-1146's silent model-build failure
        // exposed).
        logCapture.Clear();
        await SampleDataGenerator.RunAsync(connectionString, 50, logger, CancellationToken.None);
        var afterSecondRun = await GetOrderIdsAsync(connectionString);

        logCapture.Messages.ShouldContain(m =>
            m.Contains("Skipping purchase-order generation: existing rows found", StringComparison.Ordinal));
        logCapture.Messages.ShouldNotContain(m =>
            m.Contains("Sample-data generation failed and was skipped", StringComparison.Ordinal),
            "a swallowed exception must never be mistaken for the retention guard firing");

        afterSecondRun.Count.ShouldBe(53, "the retained-database guard must stop accumulation on a second run");
        afterSecondRun.ShouldBe(afterFirstRun, ignoreOrder: true,
            "every row from the first run — including the 3 static reference orders — must survive untouched");
    }

    private static async Task<List<Guid>> GetOrderIdsAsync(string connectionString)
    {
        await using var db = NewCoreDbContext(connectionString);
        return await db.Set<PurchaseOrder>().IgnoreQueryFilters().Select(o => o.Id).ToListAsync();
    }

    private static CoreDbContext NewCoreDbContext(string connectionString) => new(
        new DbContextOptionsBuilder<CoreDbContext>()
            .UseAutoConfigModel([typeof(CoreDbContext).Assembly, typeof(Sequences).Assembly])
            .UseNpgsql(connectionString)
            .Options);
}
