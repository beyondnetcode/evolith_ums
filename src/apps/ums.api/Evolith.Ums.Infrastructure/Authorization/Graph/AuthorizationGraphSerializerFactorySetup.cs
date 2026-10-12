using BeyondNetCode.Shell.Factory.Impl;
using Evolith.Ums.Application.Authorization.Graph.Serializers;

namespace Evolith.Ums.Infrastructure.Authorization.Graph;

/// <summary>
/// Shell.Factory registration for IAuthorizationGraphSerializer.
/// Resolved at runtime based on the tenant's AUTH_GRAPH_DEFAULT_FORMAT parameter.
/// Clients can also override by passing ?format=xml or Accept: application/xml.
///
/// Pattern: identical to ProfileExportFactorySetup.
/// Add new formats by registering additional .Create&lt;TImpl&gt;().When(...) entries.
/// </summary>
internal sealed class AuthorizationGraphSerializerFactorySetup : AbstractFactorySetupSource
{
    public AuthorizationGraphSerializerFactorySetup()
    {
        For<GraphSerializationCriteria, IAuthorizationGraphSerializer>()
            .Create<JsonAuthorizationGraphSerializer>().When(x => x.Format == "JSON");

        For<GraphSerializationCriteria, IAuthorizationGraphSerializer>()
            .Create<XmlAuthorizationGraphSerializer>().When(x => x.Format == "XML");

        For<GraphSerializationCriteria, IAuthorizationGraphSerializer>()
            .Create<YamlAuthorizationGraphSerializer>().When(x => x.Format == "YAML");

        For<GraphSerializationCriteria, IAuthorizationGraphSerializer>()
            .Create<CsvAuthorizationGraphSerializer>().When(x => x.Format == "CSV");
    }
}
