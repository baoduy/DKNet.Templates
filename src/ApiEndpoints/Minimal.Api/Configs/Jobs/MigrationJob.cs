namespace Minimal.Api.Configs.Jobs;

/// <summary>
///     The "migration" job (§5): migrates the database via <see cref="InfraMigration.MigrateDb" /> and reports
///     success or failure by exit code alone (R5) — no host is built, no HTTP listener is bound, no message bus
///     connection is opened (R4).
/// </summary>
internal static class MigrationJob
{
    [SuppressMessage("Design", "CA1031:Do not catch general exception types",
        Justification = "This is a process job boundary: any failure must become a non-zero exit code with the " +
                         "failure visible on output (R5), not an unhandled crash.")]
    public static async Task<int> RunAsync(IConfiguration configuration)
    {
        try
        {
            Console.WriteLine("Running Db migration...");
            var connectionString = configuration.GetConnectionString(SharedConsts.DbConnectionString);
            await InfraMigration.MigrateDb(connectionString!);
            Console.WriteLine("Db migration is completed");
            return 0;
        }
        catch (Exception ex)
        {
            // Keep ex.Message verbatim (e.g. Npgsql's "Failed to connect to ...") — it is what tells an operator
            // what actually failed (R5), not just that something did.
            await Console.Error.WriteLineAsync($"Db migration failed: {ex.Message}");
            return 1;
        }
    }
}
