namespace Evolith.Ums.Application.Authorization.Graph.Serializers;

/// <summary>
/// Controls what is included in the serialized graph output.
/// Defaults are tenant-configured via TenantParameterCodes.AUTH_GRAPH_*.
/// </summary>
public sealed record GraphSerializationOptions(
    bool IncludeTechnicalMetadata = false,   // IDs, timestamps, source fields
    bool PrettyPrint              = true)
{
    public static readonly GraphSerializationOptions Default = new();
}
