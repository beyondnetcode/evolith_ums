namespace Evolith.Ums.Application.Authorization.Graph.Serializers;

/// <summary>
/// Selection criteria used by Shell.Factory to resolve the correct
/// IAuthorizationGraphSerializer implementation at runtime.
/// </summary>
public sealed record GraphSerializationCriteria(string Format); // "JSON" | "XML" | "YAML" | "CSV"
