using Minimal.Api.Configs;
using Minimal.Api.Configs.AzureAppConfig;
using Minimal.Api.Extensions;

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
    .AddAppConfig(feature, builder.Configuration);

await builder.Build()
    .UseAppConfig(a => a.UseEndpointConfigs());

//This Startup endpoint for Unit Tests
namespace Minimal.Api
{
    public class Program;
}