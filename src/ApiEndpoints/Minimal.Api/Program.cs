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
    .AddAppConfig(feature, builder.Configuration);

await builder.Build()
    .UseAppConfig(a => a.UseEndpointConfigs(o =>
    {
        o.RequireAuthorization = feature.RequireAuthorization;
        o.EnableVersioning = feature.EnableVersioning;
        o.ConfigureGroup = (group, _) =>
        {
            group.AddEndpointFilter(async (context, next) =>
            {
                var identity = context.HttpContext.User.Identity;
                var userName = feature.RequireAuthorization
                    ? identity is { IsAuthenticated: true } ? identity.Name : null
                    : SharedConsts.SystemAccount;

                foreach (var argument in context.Arguments)
                    if (argument is RequestBase requestBase)
                        requestBase.ByUser = userName;

                return await next(context);
            });
            group.AddFluentValidationAutoValidation();
        };
    }, typeof(Program).Assembly));

//This Startup endpoint for Unit Tests
namespace Minimal.Api
{
    public class Program;
}