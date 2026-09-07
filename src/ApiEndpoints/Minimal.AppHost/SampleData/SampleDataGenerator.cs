using Bogus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Minimal.Domains.Features.AutomatedSample.Entities;
using Minimal.Domains.Features.ManualSample.Entities;
using Minimal.Domains.Share;
using Minimal.Infra.Extensions;
using Minimal.Share;

namespace Minimal.AppHost.SampleData;

/// <summary>
/// Populates the two sample entities with randomised dev data after the AppHost's resources are up.
/// Deliberately never fails the host — every failure is caught, logged, and swallowed.
/// </summary>
internal static class SampleDataGenerator
{
    #region Constants

    // PurchaseOrderStaticData seeds exactly 3 reference orders as part of the startup migration.
    private const int StaticPurchaseOrderSeedCount = 3;

    private const int ChunkSize = 1000;
    private static readonly TimeSpan SchemaWaitDeadline = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan SchemaPollInterval = TimeSpan.FromSeconds(1);

    #endregion

    #region Methods

    public static async Task RunAsync(
        string connectionString,
        int recordsPerEntity,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        if (recordsPerEntity <= 0)
        {
            logger.LogInformation(
                "Sample-data generation disabled (SampleData:RecordsPerEntity <= 0) — skipping.");
            return;
        }

        try
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            await using var db = new SampleDataDbContext(
                new DbContextOptionsBuilder<SampleDataDbContext>()
                    .UseNpgsql(connectionString, o => o.MaxBatchSize(1000))
                    .UseAutoConfigModel([typeof(InfraSetup).Assembly, typeof(Sequences).Assembly])
                    .Options);
            db.ChangeTracker.AutoDetectChangesEnabled = false;

            if (!await WaitForSchemaAsync(db, cancellationToken))
            {
                logger.LogWarning(
                    "Sample-data generation skipped: database schema is absent — the API's startup " +
                    "migration (FeatureManagement:RunDbMigrationWhenAppStart) is disabled.");
                return;
            }

            var productsWritten = await GenerateProductsAsync(db, recordsPerEntity, logger, cancellationToken);
            var ordersWritten = await GeneratePurchaseOrdersAsync(db, recordsPerEntity, logger, cancellationToken);

            logger.LogInformation(
                "Sample-data generation complete: {Products} products, {Orders} purchase orders written in {ElapsedMs} ms.",
                productsWritten, ordersWritten, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Sample-data generation failed and was skipped.");
        }
    }

    /// <summary>
    /// Polls until the migration-created <c>sample."Products"</c> table is queryable, or the deadline passes.
    /// A query failure (container still booting, schema not migrated yet) is treated as "not ready", not an error.
    /// </summary>
    private static async Task<bool> WaitForSchemaAsync(SampleDataDbContext db, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + SchemaWaitDeadline;

        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                await db.Set<Product>().AnyAsync(cancellationToken);
                return true;
            }
            catch
            {
                // Not ready yet — Postgres may still be booting, or the schema hasn't been migrated.
            }

            await Task.Delay(SchemaPollInterval, cancellationToken);
        }

        return false;
    }

    private static async Task<int> GenerateProductsAsync(
        SampleDataDbContext db, int count, ILogger logger, CancellationToken cancellationToken)
    {
        if (await db.Set<Product>().CountAsync(cancellationToken) > 0)
        {
            logger.LogInformation(
                "Skipping product generation: existing rows found — not accumulating into a retained database.");
            return 0;
        }

        var faker = new Faker();
        var usedNames = new HashSet<string>(StringComparer.Ordinal);
        var now = DateTimeOffset.UtcNow;

        foreach (var chunkSize in ChunkSizes(count))
        {
            var products = new List<Product>(chunkSize);
            for (var i = 0; i < chunkSize; i++)
                products.Add(new Product(NextUniqueProductName(faker, usedNames), faker.Random.Decimal(0.01m, 10_000m)));

            db.AddRange(products);
            foreach (var product in products)
            {
                var entry = db.Entry(product);
                entry.Property(nameof(Product.CreatedBy)).CurrentValue = SharedConsts.SystemAccount;
                entry.Property(nameof(Product.CreatedOn)).CurrentValue = now;
                entry.Property(nameof(Product.OwnedBy)).CurrentValue = SharedConsts.SystemAccount;
            }

            await db.SaveChangesAsync(cancellationToken);
            db.ChangeTracker.Clear();
        }

        return count;
    }

    private static async Task<int> GeneratePurchaseOrdersAsync(
        SampleDataDbContext db, int count, ILogger logger, CancellationToken cancellationToken)
    {
        if (await db.Set<PurchaseOrder>().CountAsync(cancellationToken) > StaticPurchaseOrderSeedCount)
        {
            logger.LogInformation(
                "Skipping purchase-order generation: existing rows found — not accumulating into a retained database.");
            return 0;
        }

        var faker = new Faker();

        foreach (var chunkSize in ChunkSizes(count))
        {
            var orders = new List<PurchaseOrder>(chunkSize);
            for (var i = 0; i < chunkSize; i++)
            {
                var customerName = Truncate(faker.Company.CompanyName(), 200);
                orders.Add(new PurchaseOrder(customerName, faker.Random.Decimal(0.01m, 100_000m), SharedConsts.SystemAccount));
            }

            db.AddRange(orders);
            await db.SaveChangesAsync(cancellationToken);
            db.ChangeTracker.Clear();
        }

        return count;
    }

    private static IEnumerable<int> ChunkSizes(int total)
    {
        var remaining = total;
        while (remaining > 0)
        {
            var size = Math.Min(ChunkSize, remaining);
            yield return size;
            remaining -= size;
        }
    }

    /// <summary>
    /// Bogus' Commerce.ProductName() collides well before 10 000 draws — dedupe against the batch and
    /// disambiguate with a random token before falling back to a guaranteed-unique id.
    /// </summary>
    private static string NextUniqueProductName(Faker faker, HashSet<string> usedNames)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var candidate = attempt == 0
                ? faker.Commerce.ProductName()
                : $"{faker.Commerce.ProductName()} {faker.Random.AlphaNumeric(6)}";

            candidate = Truncate(candidate, 150);
            if (usedNames.Add(candidate)) return candidate;
        }

        var fallback = Guid.NewGuid().ToString("N")[..20];
        usedNames.Add(fallback);
        return fallback;
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length > maxLength ? value[..maxLength] : value;

    #endregion

    /// <summary>
    /// Bare EF Core context used only to write sample data outside DI: it does not implement
    /// <c>IDataOwnerDbContext</c> and is never given <c>UseAutoDataSeeding</c>, so no data-owner read filter
    /// applies to its writes and no domain-event hook is registered against it — generated rows publish no
    /// notification and are never invisible to the row-level filter.
    /// </summary>
    private sealed class SampleDataDbContext(DbContextOptions<SampleDataDbContext> options) : DbContext(options)
    {
    }
}
