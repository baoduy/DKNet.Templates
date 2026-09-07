using System.Diagnostics;
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
/// Pins DRK-1135 §4's regression bound and §3's schema-absent invariant directly against
/// <see cref="SampleDataGenerator.RunAsync"/> — the generation seam <c>AppHost.cs</c> calls from
/// <c>AfterResourcesCreatedEvent</c>. <see cref="SampleDataGenerator"/> is internal to
/// <c>Minimal.AppHost</c>; reachable here only via that project's test-only
/// <c>InternalsVisibleTo(Minimal.App.Tests)</c> (mirrors every other project in this solution).
/// </summary>
public sealed class SampleDataGeneratorBoundsTests
{
    /// <summary>
    /// The "10 seconds for 10 000 records" figure in DRK-1135 §4 is a requirement on the generator's
    /// design (batched writes), not a promise about any one machine — a wall-clock assertion here would
    /// flake on a slow CI runner or a cold container and, worse, would pass again once "flaked" into a
    /// wider limit even after a real regression to one-row-per-round-trip. Instead this asserts the
    /// structural property that a per-record <c>SaveChangesAsync</c> regression actually violates: the
    /// number of SQL commands EF Core issues for 20 000 rows (10 000 products + 10 000 purchase orders)
    /// stays in the tens (chunked batches of 1 000 plus a handful of guard/poll queries), never anywhere
    /// near one-per-row. Counted via the process-wide EF Core <see cref="DiagnosticListener"/> feed
    /// (<c>Microsoft.EntityFrameworkCore.Database.Command.CommandExecuted</c>) — the only way to observe
    /// commands issued by a <c>DbContext</c> <see cref="SampleDataGenerator"/> constructs internally,
    /// with no seam to inject an interceptor from outside. The 1 000 ceiling leaves roughly 20x headroom
    /// both below the ~20-30 commands this implementation actually issues and above whatever incidental
    /// EF Core traffic other tests running in parallel in this process might add to the shared listener.
    /// </summary>
    [Fact]
    public async Task GivenMigratedSchema_WhenGeneratingTenThousandRecordsPerEntity_ThenIssuesFarFewerDbCommandsThanRecordsWritten()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
        await postgres.StartAsync();
        // Ephemeral containers can reuse a torn-down container's host port; clear Npgsql's
        // connection pools so a fresh container is never handed a stale pooled physical connection.
        Npgsql.NpgsqlConnection.ClearAllPools();
        await InfraMigration.MigrateDb(postgres.GetConnectionString());

        // A capturing logger, not an empty one: a run that dies after two commands would still satisfy
        // the command-count assertion below — the log line is what proves the run actually completed
        // rather than silently failing (SampleDataGenerator.RunAsync swallows every exception by design).
        var logCapture = new TestLogCapture();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(logCapture));
        var logger = loggerFactory.CreateLogger("SampleDataGenerator");

        using var commandCounter = new EfCoreCommandCounter();
        using (commandCounter.Subscribe())
        {
            await SampleDataGenerator.RunAsync(postgres.GetConnectionString(), 10_000, logger, CancellationToken.None);
        }

        commandCounter.CommandCount.ShouldBeLessThan(1_000,
            $"20 000 generated rows should take tens of batched SQL commands, not {commandCounter.CommandCount} — " +
            "that count is the signature of a per-record round trip, exactly the regression this bound exists to catch.");

        logCapture.Messages.ShouldNotContain(m =>
            m.Contains("Sample-data generation failed and was skipped", StringComparison.Ordinal),
            "a swallowed exception must never be mistaken for a completed run at this volume");

        await using var verifyDb = new CoreDbContext(
            new DbContextOptionsBuilder<CoreDbContext>()
                .UseAutoConfigModel([typeof(CoreDbContext).Assembly, typeof(Sequences).Assembly])
                .UseNpgsql(postgres.GetConnectionString())
                .Options);
        var productCount = await verifyDb.Set<Product>().IgnoreQueryFilters().CountAsync();
        var orderCount = await verifyDb.Set<PurchaseOrder>().IgnoreQueryFilters().CountAsync();

        productCount.ShouldBe(10_000, "every requested product row must actually be written, not just cheaply counted in commands");
        orderCount.ShouldBe(10_003, "10 000 generated orders plus the 3 static reference orders the migration seeds");
    }

    /// <summary>
    /// DRK-1135 §5's on/off-switch scenario: <c>recordsPerEntity &lt;= 0</c> is the documented way to get an
    /// empty database, and the guard returns before any <c>DbContext</c> is even constructed — so this needs
    /// no container.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GivenRecordsPerEntityIsZeroOrNegative_WhenGenerating_ThenLogsDisabledAndWritesNothing(int recordsPerEntity)
    {
        var logCapture = new TestLogCapture();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(logCapture));
        var logger = loggerFactory.CreateLogger("SampleDataGenerator");

        await SampleDataGenerator.RunAsync(
            "Host=unreachable;Database=none", recordsPerEntity, logger, CancellationToken.None);

        logCapture.Messages.ShouldContain(m =>
            m.Contains("Sample-data generation disabled", StringComparison.Ordinal));
        logCapture.Messages.ShouldNotContain(m =>
            m.Contains("Sample-data generation failed and was skipped", StringComparison.Ordinal),
            "the disabled guard must return before ever touching the database");
    }

    /// <summary>
    /// This round added a dedicated <c>catch (OperationCanceledException)</c> ahead of the general handler
    /// so a clean cancellation (e.g. host shutdown mid-generation) is never misreported as a generation
    /// failure. The risk cutting the other way — a genuine failure being swallowed as "just a cancellation"
    /// — is already ruled out: <see cref="GivenSchemaAbsent_WhenGenerating_ThenSkipsWithoutThrowingAndLogsTheCause"/>
    /// and every retention/visibility test in this suite exercise real, non-cancellation exceptions and all
    /// still assert the "failed and was skipped" line, which only the general <c>catch (Exception)</c> logs.
    /// This test is the other half: prove an *actually cancelled* token takes the new branch instead — an
    /// already-cancelled token makes <c>WaitForSchemaAsync</c>'s first <c>AnyAsync</c> throw before any
    /// network I/O, so no container is needed.
    /// </summary>
    [Fact]
    public async Task GivenAnAlreadyCancelledToken_WhenGenerating_ThenLogsCancelledNotFailed()
    {
        var logCapture = new TestLogCapture();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(logCapture));
        var logger = loggerFactory.CreateLogger("SampleDataGenerator");
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await SampleDataGenerator.RunAsync(
            "Host=unreachable;Database=none", 100, logger, cts.Token);

        logCapture.Messages.ShouldContain(m =>
            m.Contains("Sample-data generation cancelled", StringComparison.Ordinal));
        logCapture.Messages.ShouldNotContain(m =>
            m.Contains("Sample-data generation failed and was skipped", StringComparison.Ordinal),
            "a clean cancellation must never be logged as a generation failure");
    }

    /// <summary>
    /// DRK-1135 §3's hard invariant: with the API's startup migration disabled, the schema never exists,
    /// and generation must still leave the host standing — <see cref="SampleDataGenerator.RunAsync"/>
    /// "deliberately never fails the host" (see its own remarks) by catching and logging every failure.
    /// Proven here by three assertions on one run: the call returns normally (an unhandled exception here
    /// would be the crash this invariant forbids), the specific "schema is absent" warning is the log the
    /// developer actually sees (not the generic catch-all failure message), and nothing was created —
    /// checked structurally via <c>information_schema</c> rather than assumed, so a future change that
    /// adds any DDL/EnsureCreated call to the generator would fail this test.
    /// Runs against the generator's real 60 s schema-poll deadline (an internal, non-configurable
    /// constant) — this test is slow by design, not flaky.
    /// </summary>
    [Fact]
    public async Task GivenSchemaAbsent_WhenGenerating_ThenSkipsWithoutThrowingAndLogsTheCause()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
        await postgres.StartAsync();
        // Ephemeral containers can reuse a torn-down container's host port; clear Npgsql's
        // connection pools so a fresh container is never handed a stale pooled physical connection.
        Npgsql.NpgsqlConnection.ClearAllPools();
        // Deliberately never migrated — the schema InfraMigration.MigrateDb would create never exists.

        var logCapture = new TestLogCapture();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(logCapture));
        var logger = loggerFactory.CreateLogger("SampleDataGenerator");

        await SampleDataGenerator.RunAsync(postgres.GetConnectionString(), 100, logger, CancellationToken.None);

        logCapture.Messages.ShouldContain(m =>
            m.Contains("schema is absent", StringComparison.OrdinalIgnoreCase));

        await using var connection = new Npgsql.NpgsqlConnection(postgres.GetConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT count(*) FROM information_schema.tables WHERE table_schema IN ('sample', 'manual_sample')";
        var tableCount = (long)(await command.ExecuteScalarAsync())!;
        tableCount.ShouldBe(0L, "generation must create nothing when the schema is absent, not merely skip inserts");
    }

    /// <summary>
    /// Subscribes to every <see cref="DiagnosticListener"/> in the process and counts EF Core's
    /// command-executed events — the only externally observable signal for a <c>DbContext</c> a callee
    /// constructs and disposes internally, with no injectable interceptor seam.
    /// </summary>
    private sealed class EfCoreCommandCounter : IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>, IDisposable
    {
        private const string EfCoreListenerName = "Microsoft.EntityFrameworkCore";
        private const string CommandExecutedEventName = "Microsoft.EntityFrameworkCore.Database.Command.CommandExecuted";
        private readonly List<IDisposable> _listenerSubscriptions = [];
        private int _commandCount;

        public int CommandCount => _commandCount;

        public IDisposable Subscribe() => DiagnosticListener.AllListeners.Subscribe(this);

        public void OnNext(DiagnosticListener listener)
        {
            if (listener.Name == EfCoreListenerName)
            {
                _listenerSubscriptions.Add(listener.Subscribe(this));
            }
        }

        public void OnNext(KeyValuePair<string, object?> value)
        {
            if (value.Key == CommandExecutedEventName)
            {
                Interlocked.Increment(ref _commandCount);
            }
        }

        public void OnError(Exception error)
        {
        }

        public void OnCompleted()
        {
        }

        public void Dispose()
        {
            foreach (var subscription in _listenerSubscriptions) subscription.Dispose();
            _listenerSubscriptions.Clear();
        }
    }
}
