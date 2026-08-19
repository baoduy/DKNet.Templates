using Microsoft.OpenApi;

namespace Minimal.Api.Configs.Swagger;

[SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters")]
[ExcludeFromCodeCoverage]
internal static class SwaggerConfig
{
    #region Fields

    private static readonly string[] ExcludeFromPublic = ["internal", "static"];

    #endregion

    #region Methods

    public static WebApplication UseOpenApiDoc(this WebApplication app)
    {
        if (!app.Services.IsConfigAdded(nameof(SwaggerConfig))) return app;

        app.MapOpenApi();
        app.MapScalarApiReference("/docs", c =>
            c.WithTitle($"{SharedConsts.ApiName} API")
                .WithTheme(ScalarTheme.Default)
                //.WithOpenApiRoutePattern("{documentName}.json")
                .AddPreferredSecuritySchemes("Bearer")
                .AddHttpAuthentication("Bearer", b => b.Token = "bearer token")
        );

        Console.WriteLine("Swagger enabled.");
        return app;
    }

    #endregion

    extension(IServiceCollection services)
    {
        public IServiceCollection AddOpenApiDoc(FeatureOptions features)
        {
            services.AddOpenApiDocVersion("v1", features.RequireAuthorization, true);

            services.MarkConfigAdded(nameof(SwaggerConfig));
            return services;
        }

        private IServiceCollection AddOpenApiDocVersion(string name,
            bool enableAuthentication, bool includeInternal = false)
        {
            return services.AddOpenApi(name,
                c =>
                {
                    //The OpenAPI version 3.1 is not compatible with APIM yet.
                    c.OpenApiVersion = OpenApiSpecVersion.OpenApi3_0;
                    c.ShouldInclude = description =>
                    {
                        return includeInternal || !ExcludeFromPublic.Any(s =>
                            description.RelativePath!.Contains(s, StringComparison.OrdinalIgnoreCase));
                    };

                    // if (enableAuthentication)
                    //     c.AddDocumentTransformer<BearerSecurityTransformer>();
                    //c.AddOperationTransformer<PathParameterOperationTransformer>();
                    //c.AddSchemaTransformer<JsonStringEnumSchemaTransformer>();
                    //c.AddSchemaTransformer<ExcludeInterfaceSchemaTransformer>();
                    //c.AddSchemaTransformer<DisplayNameSchemaTransformer>();
                    //c.AddDocumentTransformer<DisplayNameSchemaDocumentTransformer>();

                    c.AddDocumentTransformer((doc, _, _) =>
                    {
                        doc.Info.Title = $"{SharedConsts.ApiName} API {name} Version";
                        //doc.Servers!.AddRange();

                        var paths = new OpenApiPaths();
                        foreach (var openApiPath in doc.Paths)
                        {
                            var key = openApiPath.Key.Replace("v{version}", "v1",
                                StringComparison.OrdinalIgnoreCase);
                            paths.Add(key, openApiPath.Value);
                        }

                        doc.Paths = paths;
                        return Task.CompletedTask;
                    });
                });
        }
    }
}