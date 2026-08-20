using Minimal.Api.Configs;
using Minimal.Api.Configs.AzureAppConfig;
using Minimal.Api.Extensions;
using SharpGrip.FluentValidation.AutoValidation.Endpoints.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Rebind features after potentially loading from Azure App Configuration
var feature = builder.Configuration.Bind<FeatureOptions>(FeatureOptions.Name);

builder.AddLogConfig(feature)
    .AddAzureAppConfig(feature)
    .AddFluentValidationConfig();

//Run migration and exit the app if needed.
await builder.RunMigrationAsync(feature, args);

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

//This Startup endpoint for Unit Tests
namespace Minimal.Api
{
    public class Program;
}