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

await builder.Build().RunAsync();