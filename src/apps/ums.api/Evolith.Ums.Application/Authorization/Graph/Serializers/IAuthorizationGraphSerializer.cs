using Evolith.Ums.Domain.Authorization.Graph;

namespace Evolith.Ums.Application.Authorization.Graph.Serializers;

/// <summary>
/// Serializes an AuthorizationGraph to a specific wire format.
///
/// Concrete implementations (JSON, XML, YAML, CSV) are registered via
/// Shell.Factory (AuthorizationGraphSerializerFactorySetup) and resolved
/// at runtime based on the tenant's AUTH_GRAPH_DEFAULT_FORMAT parameter
/// or the caller's explicit format override.
/// </summary>
public interface IAuthorizationGraphSerializer
{
    string Serialize(AuthorizationGraph graph, GraphSerializationOptions? options = null);

    /// <summary>MIME type for the HTTP response Content-Type header.</summary>
    string ContentType { get; }

    /// <summary>File extension without dot (e.g. "json", "xml", "yaml", "csv").</summary>
    string FileExtension { get; }
}
