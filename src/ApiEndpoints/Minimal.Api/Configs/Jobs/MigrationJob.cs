namespace Minimal.Api.Configs.Jobs;

/// <summary>
///     The "migration" job (§5): migrates the database via <see cref="InfraMigration.MigrateDb" /> and reports
///     success or failure by exit code alone (R5) — no host is built, no HTTP listener is bound, no message bus
///     connection is opened (R4).
/// </summary>
internal static class MigrationJob
{
    public static Task<int> RunAsync(IConfiguration configuration)
    {
        throw new NotImplementedException();
    }
}
