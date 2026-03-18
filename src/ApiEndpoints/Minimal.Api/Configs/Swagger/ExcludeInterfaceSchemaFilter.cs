using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Minimal.Api.Configs.Swagger;

[ExcludeFromCodeCoverage]
internal sealed class ExcludeInterfaceSchemaTransformer : IOpenApiSchemaTransformer
{
    #region Fields

    private static readonly string[] ExcludedSchemaNames = ["IFlowResult"];

    #endregion

    #region Methods

    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        var typeName = context.JsonTypeInfo.Type.Name;

        // Skip any schemas matching excluded interface names
        if (ExcludedSchemaNames.Contains(typeName))
        {
            // Clear discriminator and schema details; leave as generic object
            schema.Discriminator = null;
            schema.OneOf?.Clear();
            schema.Properties?.Clear();
        }

        return Task.CompletedTask;
    }

    #endregion
}



