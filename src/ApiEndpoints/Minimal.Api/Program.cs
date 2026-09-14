using Minimal.Api.Configs;
using Minimal.Api.Configs.AzureAppConfig;
using Minimal.Api.Configs.Jobs;
using SharpGrip.FluentValidation.AutoValidation.Endpoints.Extensions;

var builder = WebApplication.CreateBuilder(args);

// The service decides what to do from its process arguments alone (R1) — no environment check, no configuration
// value. Selected and dispatched before anything that could serve traffic or connect to the message bus (§3 row
// 2), so a job run loads only what the job needs (R4).
var jobSelection = JobSelector.Select(args, JobRegistry.Jobs.Keys.ToArray());
if (jobSelection.HasJobName)
{
    if (!jobSelection.IsRecognized)
    {
        await Console.Error.WriteLineAsync(
            $"Unrecognized job \"{jobSelection.RequestedJobName}\". Known jobs: {string.Join(", ", jobSelection.KnownJobNames)}");
        return 1;
    }

    return await JobRegistry.Jobs[jobSelection.RequestedJobName!](builder.Configuration);
}

// Rebind features after potentially loading from Azure App Configuration
var feature = builder.Configuration.GetSection(FeatureOptions.Name).Get<FeatureOptions>() ?? new FeatureOptions();

builder.AddLogConfig(feature)
    .AddAzureAppConfig(feature)
    .AddFluentValidationConfig();

//Run migration (when configured to) and continue to serve.
await builder.RunMigrationAsync(feature);

// Add services to the container.
builder.Services
    .AddOptions(builder.Configuration)
    .AddAppConfig(feature, builder.Configuration)
    // Populates [FromClaim] members (e.g. ByUser) before validation and before the handler; the fallback below
    // only applies when RequireAuthorization is off, never per-caller.
    .AddContextualRequestPopulation(o => o.SystemAccountFallback = SharedConsts.SystemAccount);

await builder.Build()
    .UseAppConfig(a => a.UseEndpointConfigs(o =>
    {
        o.RequireAuthorization = feature.RequireAuthorization;
        o.EnableVersioning = feature.EnableVersioning;
        o.ConfigureGroup = (group, _) => group.AddFluentValidationAutoValidation();
    }, typeof(Program).Assembly));

return 0;

//This Startup endpoint for Unit Tests
namespace Minimal.Api
{
    public class Program;
}