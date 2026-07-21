namespace Minimal.Infra.Contexts;

internal class CoreDbContext(DbContextOptions options) : DbContext(options)
{
    #region Methods

    /// <summary>
    /// DKNet.EfCore.Extensions 10.0.30's auto-config only registers `[SqlSequence]` sequences
    /// (see <c>Sequences</c>) when the provider is SQL Server — the Npgsql gate was never added.
    /// This mirrors that registration for Npgsql until the gap is fixed upstream; names/settings
    /// must stay in sync with the <c>Sequences</c> enum.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        if (!this.IsNpgsql()) return;

        modelBuilder.HasSequence<int>("Seq_None", "seq").IsCyclic();
        modelBuilder.HasSequence<int>("Seq_Membership", "seq").HasMax(99999).IsCyclic();
    }

    #endregion
}