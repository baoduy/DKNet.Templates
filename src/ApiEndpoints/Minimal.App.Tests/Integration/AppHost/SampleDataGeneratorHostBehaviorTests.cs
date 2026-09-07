using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Minimal.AppHost.SampleData;
using Minimal.AppServices.AutomatedSample.V1;
using Minimal.Domains.Features.AutomatedSample.Entities;
using Minimal.Infra.Contexts;
using Minimal.Share;

namespace Minimal.App.Tests.Integration.AppHost;

/// <summary>
/// The two DRK-1135 invariants that only show up once a real host is watching the same database
/// <see cref="SampleDataGenerator"/> writes to: row-level visibility (<c>DataOwnerAuthQuery</c>'s read
/// filter, per §6) and the absence of domain notifications for generated rows (§6). One generation run
/// backs both assertions — visibility and non-notification are two outcomes of the same act, exactly
/// the shape <see cref="Minimal.App.Tests.Integration.AutomatedSample.V1.ProductOwnershipIsolationTests"/>
/// already uses for this same row-filter boundary.
/// </summary>
public sealed class SampleDataGeneratorHostBehaviorTests(SampleDataApiFixture fixture) : IClassFixture<SampleDataApiFixture>
{
    [Fact]
    public async Task GeneratedProducts_AreVisibleToAnUnauthenticatedCallerThroughTheOwnershipFilter_AndPublishNoDomainNotification()
    {
        const int recordsPerEntity = 30;
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        await SampleDataGenerator.RunAsync(
            fixture.ConnectionString, recordsPerEntity, loggerFactory.CreateLogger("SampleDataGenerator"), CancellationToken.None);

        // Visibility: rows can exist and still be invisible under row-level isolation — an unauthenticated
        // local request resolves to ownership key "System" (PrincipalProvider), the same key the generator
        // stamps, so every generated product must come back through the real filter, not just exist in the DB.
        var client = fixture.CreateClient();
        var response = await client.GetAsync("/v1/products?pageSize=100");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var envelope = await response.Content.ReadFromJsonAsync<ProductListEnvelope>();
        envelope.ShouldNotBeNull();
        envelope!.Items.Count.ShouldBe(recordsPerEntity,
            "generated products must be readable through the row-level filter, not merely present in the table");

        // Bypass the filter (IgnoreQueryFilters — a raw EF Core flag, unaffected by the filter itself) to
        // confirm the stored columns actually carry System ownership, not just that the filter happens to
        // let them through — either check alone can hide a hole (see ProductOwnershipIsolationTests).
        using var scope = fixture.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        var stored = await dbContext.Set<Product>().IgnoreQueryFilters().ToListAsync();
        stored.Count.ShouldBe(recordsPerEntity);
        stored.ShouldAllBe(p => p.OwnedBy == SharedConsts.SystemAccount);
        stored.ShouldAllBe(p => p.CreatedBy == SharedConsts.SystemAccount);

        // No domain notification: Product declares [RaisesEvent(EventOperations.Created, ...)], raised by
        // the DKNet events hook on SaveChanges — but SampleDataGenerator writes through a bare DbContext
        // never wired with that hook (see its own remarks), so even with the real host's
        // ProductCreatedEventHandler alive and logging every invocation ("AutomatedSample product created:
        // {ProductId}"), 30 new product rows must produce zero such log lines.
        fixture.LogCapture.Messages.ShouldNotContain(m =>
            m.Contains("AutomatedSample product created", StringComparison.Ordinal));
    }

    private sealed record ProductListEnvelope(List<ProductDto> Items);
}
