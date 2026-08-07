using System.Diagnostics.CodeAnalysis;
using Minimal.Infra.Contexts;

namespace Minimal.Infra.Extensions;

/// <summary>
///
/// </summary>
[ExcludeFromCodeCoverage]
public static class InfraMigration
{
    #region Methods

    /// <summary>
    /// Migrates the database to the latest version. This method should be called during application startup to ensure that the database schema is up to date before the application starts handling requests.
    /// </summary>
    /// <param name="connectionString"></param>
    public static async Task MigrateDb(string connectionString)
    {
        //Db migration
        await using var db = new CoreDbContext(
            new DbContextOptionsBuilder<CoreDbContext>()
                .UseAutoConfigModel([typeof(CoreDbContext).Assembly, typeof(Sequences).Assembly])
                .UseNpgsqlWithMigration(connectionString)
                .Options);

        await db.Database.MigrateAsync();

        // Data seeding can be added here when needed (IDataSeedingConfiguration has limitations with owned types)
    }

    #endregion
}