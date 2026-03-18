using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace SlimBus.Api.Configs.Swagger;

[ExcludeFromCodeCoverage]
internal sealed class JsonStringEnumSchemaTransformer : IOpenApiSchemaTransformer
{
    #region Methods

    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        var type = context.JsonTypeInfo.Type;

        // Handle direct enum types
        if (type.IsEnum)
        {
            schema.Type = JsonSchemaType.String;
            schema.Enum = [];

            var names = Enum.GetNames(type);
            foreach (var name in names)
                schema.Enum.Add(JsonValue.Create(name.ToLowerInvariant()));

            return Task.CompletedTask;
        }

        // Handle arrays with enum element type
        if (type.IsArray && type.GetElementType() is { IsEnum: true } arrayElemType)
        {
            schema.Type = JsonSchemaType.Array;
            schema.Items = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Enum = []
            };

            var names = Enum.GetNames(arrayElemType);
            foreach (var name in names)
                schema.Items.Enum!.Add(JsonValue.Create(name.ToLowerInvariant()));

            return Task.CompletedTask;
        }

        // Handle generic collections (e.g., IEnumerable<Enum>, List<Enum>)
        if (type.IsGenericType)
        {
            var genDef = type.GetGenericTypeDefinition();
            var interfaces = type.GetInterfaces();
            var isEnumerable = genDef == typeof(IEnumerable<>)
                               || interfaces.Any(i =>
                                   i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

            if (isEnumerable)
            {
                var enumElemType = type.GetGenericArguments()[0];
                if (enumElemType.IsEnum)
                {
                    schema.Type = JsonSchemaType.Array;
                    schema.Items = new OpenApiSchema
                    {
                        Type = JsonSchemaType.String,
                        Enum = []
                    };

                    var names = Enum.GetNames(enumElemType);
                    foreach (var name in names.Where(n =>
                                 !string.Equals(n, "None", StringComparison.OrdinalIgnoreCase)))
                        schema.Items.Enum!.Add(JsonValue.Create(name.ToLowerInvariant()));

                    return Task.CompletedTask;
                }
            }
        }

        return Task.CompletedTask;
    }

    #endregion
}