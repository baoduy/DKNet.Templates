// ReSharper disable once CheckNamespace

namespace Microsoft.AspNetCore.Builder;

internal interface IEndpointConfig
{
    string? AuthPolicy => null;
    string GroupEndpoint { get; }
    string Tag => GroupEndpoint.Replace("/", "-", StringComparison.OrdinalIgnoreCase).TrimStart('-');
    int Version { get; }

    void Map(RouteGroupBuilder group);
}