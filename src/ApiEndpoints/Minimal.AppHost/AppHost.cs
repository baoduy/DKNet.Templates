using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Minimal.AppHost.SampleData;
using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedis("Redis");
var postgres = builder.AddPostgres("Postgres");

var apDb = postgres
    .AddDatabase("AppDb");

builder.AddProject<Minimal_Api>("Api")
    .WithReference(cache, "Redis")
    .WithReference(apDb, "AppDb")

    //.WaitFor(bus)
    .WaitFor(cache)
    .WaitFor(apDb);

var recordsPerEntity = builder.Configuration.GetValue("SampleData:RecordsPerEntity", 10000);

builder.Eventing.Subscribe<AfterResourcesCreatedEvent>(async (@event, cancellationToken) =>
{
    var logger = @event.Services.GetRequiredService<ILoggerFactory>().CreateLogger("SampleDataGenerator");

    var connectionString = await apDb.Resource.ConnectionStringExpression.GetValueAsync(cancellationToken);
    if (string.IsNullOrEmpty(connectionString))
    {
        logger.LogWarning("Sample-data generation skipped: no connection string was resolved for the AppDb resource.");
        return;
    }

    await SampleDataGenerator.RunAsync(connectionString, recordsPerEntity, logger, cancellationToken);
});

await builder.Build().RunAsync();