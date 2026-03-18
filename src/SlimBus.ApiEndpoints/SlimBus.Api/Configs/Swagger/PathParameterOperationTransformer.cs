using System.Text.RegularExpressions;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace SlimBus.Api.Configs.Swagger;

/// <summary>
///     Operation transformer that ensures all path parameters in the URI template are defined in the OpenAPI operation.
///     This is required for Azure API Management validation.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed partial class PathParameterOperationTransformer : IOpenApiOperationTransformer
{
    #region Methods

    [GeneratedRegex(@"\{(?<name>[^:}]+)(?::[^}]+)?\}", RegexOptions.Compiled | RegexOptions.ExplicitCapture, 1000)]
    private static partial Regex PathParameterRegex();

    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        var path = context.Description.RelativePath;
        if (string.IsNullOrEmpty(path))
            return Task.CompletedTask;

        // Extract all path parameters from the URI template (e.g., {id}, {id:guid})
        var matches = PathParameterRegex().Matches(path);
        if (matches.Count == 0)
            return Task.CompletedTask;

        // Ensure parameters collection exists
        operation.Parameters ??= [];

        foreach (Match match in matches)
        {
            var parameterName = match.Groups["name"].Value;

            // Skip version parameter as it's handled by API versioning middleware
            if (string.Equals(parameterName, "version", StringComparison.OrdinalIgnoreCase))
                continue;

            // Check if parameter already exists
            var existingParam = operation.Parameters.FirstOrDefault(p =>
                string.Equals(p.Name, parameterName, StringComparison.OrdinalIgnoreCase));

            if (existingParam == null)
                // Add missing path parameter
                operation.Parameters.Add(new OpenApiParameter
                {
                    Name = parameterName,
                    In = ParameterLocation.Path,
                    Required = true,
                    Schema = new OpenApiSchema
                    {
                        Type = JsonSchemaType.String,
                        Format = path.Contains(":guid", StringComparison.OrdinalIgnoreCase)
                            ? "uuid"
                            : null
                    },
                    Description = $"The {parameterName} parameter"
                });
        }

        return Task.CompletedTask;
    }

    #endregion
}