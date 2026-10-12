using System.Text;
using System.Xml.Linq;
using Evolith.Ums.Application.Authorization.Graph.Serializers;
using Evolith.Ums.Domain.Authorization.Graph;

namespace Evolith.Ums.Infrastructure.Authorization.Graph;

/// <summary>
/// Serializes AuthorizationGraph to XML.
///
/// <para>Respeta la proyección con el MISMO criterio que <c>AuthGraphPayload</c>: un bloque no
/// pedido se omite como elemento, nunca viaja vacío. Que hoy solo <c>GET /client/graph</c> —que
/// codifica JSON directamente— acepte <c>?include=</c> no autoriza a dejar el XML fuera: en cuanto
/// la proyección alcance a <c>/client/authenticate</c>, donde el formato lo elige el inquilino, un
/// serializador que ignorase la proyección le devolvería a un inquilino en XML el grafo entero que
/// el mismo satélite recibe recortado en JSON. Esa clase de asimetría no se descubre leyendo: se
/// descubre en producción, y por el lado que concede.</para>
///
/// <para>Lo que este cambio NO arregla, y conviene no confundir con lo que sí: la asimetría
/// PREEXISTENTE entre este serializador y la forma canónica —al XML le faltan <c>schemaVersion</c>,
/// <c>onboardingPending</c>, <c>accessState</c>, <c>profiles</c>, <c>settings</c>,
/// <c>requestedSystem</c>, <c>sortOrder</c>, <c>icon</c>, <c>route</c> y tres campos de
/// <c>effectiveConfig</c>—. Es un hueco anterior a la proyección y ajeno a ella; arreglarlo aquí
/// mezclaría dos cambios de contrato en uno.</para>
///
/// <para>PERO ESE HUECO SÍ OBLIGA AL ECO. Si <c>projection</c> repitiera los tokens PEDIDOS, un
/// <c>?include=navigation,settings</c> saldría de aquí diciendo «te di settings» sin mandar
/// <c>settings</c>, porque este serializador no sabe emitirlos. Eso es un fallo MUDO: el cliente
/// compararía su petición con el eco, vería que coinciden y concluiría que la sección llegó vacía
/// cuando en realidad nunca salió. Por eso el eco declara la proyección APLICADA —ver
/// <see cref="TokensHonrados"/>— y lo que no se ha podido honrar se declara aparte, en
/// <c>&lt;unsupported&gt;</c>, en vez de desaparecer sin dejar rastro.</para>
/// </summary>
public sealed class XmlAuthorizationGraphSerializer : IAuthorizationGraphSerializer
{
    /// <summary>
    /// Tokens del vocabulario cuyas secciones este serializador sabe emitir, y por tanto los
    /// únicos que puede HONRAR. <c>profiles</c> y <c>settings</c> no están: el XML no los ha
    /// emitido nunca (el hueco preexistente que documenta la cabecera de la clase).
    ///
    /// <para>La lista se deriva de lo que construye <see cref="Serialize"/>, no de una copia del
    /// vocabulario: el día que el XML aprenda a emitir <c>settings</c>, añadir el token aquí es lo
    /// mismo que declarar que ya se honra.</para>
    /// </summary>
    private static readonly IReadOnlySet<string> TokensHonrados =
        new HashSet<string>(StringComparer.Ordinal)
        {
            GraphProjection.Permisos,
            GraphProjection.Navegacion,
            GraphProjection.Banderas,
            GraphProjection.Config,
        };

    public string ContentType   => "application/xml";
    public string FileExtension => "xml";

    public string Serialize(AuthorizationGraph graph, GraphSerializationOptions? options = null)
    {
        var opts = options ?? GraphSerializationOptions.Default;
        var g = graph;
        var proy = graph.Projection;
        XAttribute? FormatId(Guid id) =>
            opts.IncludeTechnicalMetadata ? new XAttribute("id", id) : null;

        XElement? BuildBranch()
        {
            if (g.Context.Branch is null)
                return null;

            return new XElement("branch",
                FormatId(g.Context.Branch.Id),
                new XAttribute("code", g.Context.Branch.Code),
                new XAttribute("value", g.Context.Branch.Name));
        }

        XElement? BuildProvider()
        {
            if (g.Authentication.Provider is null)
                return null;

            return new XElement("provider",
                FormatId(g.Authentication.Provider.Id),
                new XAttribute("code", g.Authentication.Provider.Code),
                new XAttribute("name", g.Authentication.Provider.Name),
                new XAttribute("value", g.Authentication.Provider.Strategy));
        }

        XElement BuildContext()
            => new("context",
                new XElement("user",
                    FormatId(g.Context.User.Id),
                    new XAttribute("email", g.Context.User.Email),
                    new XAttribute("username", g.Context.User.Username),
                    new XAttribute("value", g.Context.User.DisplayName),
                    new XAttribute("status", g.Context.User.Status)),
                new XElement("tenant",
                    FormatId(g.Context.Tenant.Id),
                    new XAttribute("code", g.Context.Tenant.Code),
                    new XAttribute("value", g.Context.Tenant.Name),
                    new XAttribute("status", g.Context.Tenant.Status),
                    new XAttribute("isManagementOwner", g.Context.Tenant.IsManagementOwner)),
                // G-043: en el grafo lobby SystemSuite/Role/Profile son null → se omiten los elementos.
                g.Context.SystemSuite is null ? null : new XElement("systemSuite",
                    FormatId(g.Context.SystemSuite.Id),
                    new XAttribute("code", g.Context.SystemSuite.Code),
                    new XAttribute("value", g.Context.SystemSuite.Name),
                    new XAttribute("status", g.Context.SystemSuite.Status)),
                g.Context.Role is null ? null : new XElement("role",
                    FormatId(g.Context.Role.Id),
                    new XAttribute("code", g.Context.Role.Code),
                    new XAttribute("value", g.Context.Role.Name),
                    new XAttribute("level", g.Context.Role.HierarchyLevel)),
                g.Context.Profile is null ? null : new XElement("profile",
                    FormatId(g.Context.Profile.Id),
                    new XAttribute("scope", g.Context.Profile.Scope),
                    new XAttribute("isActive", g.Context.Profile.IsActive)),
                BuildBranch());

        XElement BuildAuthentication()
            => new("authentication",
                new XAttribute("method", g.Authentication.Method),
                new XAttribute("mfaRequired", g.Authentication.MfaRequired),
                new XAttribute("issuedAt", g.Authentication.IssuedAt.ToString("O")),
                new XAttribute("sessionExpiresAt", g.Authentication.SessionExpiresAt.ToString("O")),
                BuildProvider());

        XElement BuildActions()
            => new("actions",
                g.Actions.Select(a => new XElement("action",
                    new XAttribute("code", a.Code),
                    new XAttribute("value", a.Name))));

        // Un solo elemento `node` recursivo: el árbol no tiene profundidad fija.
        XElement BuildNode(Evolith.Ums.Domain.Authorization.Graph.GraphNavigationNode n)
            => new("node",
                new XAttribute("code", n.Code),
                new XAttribute("value", n.Name),
                new XAttribute("kind", n.Kind),
                n.Actions.Select(a => new XElement("action",
                    new XAttribute("actionCode", a.ActionCode),
                    new XAttribute("effect", a.Effect.ToString()),
                    new XAttribute("source", a.Source.ToString()))),
                n.Children.Select(BuildNode));

        XElement BuildMenuAccess()
            => new("menuAccess",
                g.MenuAccess.Select(m => new XElement("module",
                    new XAttribute("code", m.Code),
                    new XAttribute("value", m.Name),
                    new XAttribute("status", m.Status),
                    m.Nodes.Select(BuildNode))));

        XElement BuildDomainPermissions()
            => new("domainPermissions",
                g.DomainPermissions.Select(r => new XElement("resource",
                    FormatId(r.ResourceId),
                    opts.IncludeTechnicalMetadata && r.ModuleId.HasValue ? new XAttribute("moduleId", r.ModuleId.Value) : null,
                    opts.IncludeTechnicalMetadata && r.ParentResourceId.HasValue ? new XAttribute("parentResourceId", r.ParentResourceId.Value) : null,
                    new XAttribute("type", r.ResourceType),
                    new XAttribute("code", r.ResourceCode),
                    new XAttribute("value", r.ResourceName),
                    r.Actions.Select(a => new XElement("action",
                        new XAttribute("code", a.ActionCode),
                        new XAttribute("value", a.ActionName),
                        new XAttribute("effect", a.Effect.ToString()),
                        new XAttribute("source", a.Source.ToString()))))));

        XElement BuildFeatureFlags()
            => new("featureFlags",
                g.FeatureFlags.Select(f => new XElement("flag",
                    new XAttribute("code", f.FlagCode),
                    new XAttribute("isEnabled", f.IsEnabled),
                    f.MatchedCriteriaType is null ? null
                        : new XAttribute("matchedCriteria", f.MatchedCriteriaType))));

        // Eco de la proyección APLICADA —no de la pedida—, presente si y solo si se pidió una.
        //
        // La distinción no es sutileza: este serializador no emite `profiles` ni `settings`, así que
        // repetir los tokens pedidos sería anunciar bloques que no viajan. El cliente compararía su
        // petición contra el eco, los vería coincidir y leería la ausencia como «la sección llegó
        // vacía» — el fallo mudo, y encima por el lado que concede.
        //
        // Lo no honrado NO se calla: va en `<unsupported>`. Omitirlo sin más dejaría al cliente con
        // un eco más corto que su petición y sin saber por qué; decirlo convierte un hueco del
        // serializador en un dato comprobable en la propia respuesta.
        XElement? BuildProjection()
        {
            if (proy is null) return null;

            var honrados   = proy.Tokens.Where(TokensHonrados.Contains).ToList();
            var sinHonrar  = proy.Tokens.Where(t => !TokensHonrados.Contains(t)).ToList();

            return new XElement("projection",
                honrados.Select(t => new XElement("include", t)),
                sinHonrar.Count == 0 ? null : new XElement("unsupported",
                    sinHonrar.Select(t => new XElement("include", t))));
        }

        var doc = new XDocument(new XDeclaration("1.0", "utf-8", null),
            new XElement("authorizationGraph",
                new XAttribute("generatedAt", g.GeneratedAt.ToString("O")),
                new XAttribute("validUntil", g.ValidUntil.ToString("O")),
                BuildProjection(),
                BuildContext(),
                BuildAuthentication(),
                // `actions` acompaña a la navegación: sin el catálogo, los `actionCode` del árbol
                // no tienen nombre legible.
                proy.LlevaNavegacion() ? BuildActions() : null,
                proy.LlevaNavegacion() ? BuildMenuAccess() : null,
                // ATOMICIDAD DE `permissions`: `domainPermissions` y `scopes` comparten condición,
                // aquí y en la forma canónica. Separarlas dejaría emitir los scopes sin las
                // denegaciones explícitas de dominio, y el evaluador que las combina con deny-wins
                // lee la lista ausente como «no hay denegaciones»: concedería lo que UMS denegó.
                proy.LlevaPermisos() ? BuildDomainPermissions() : null,
                proy.LlevaBanderas() ? BuildFeatureFlags() : null,
                !proy.LlevaConfig() ? null : new XElement("effectiveConfig",
                    new XAttribute("sessionTimeoutMinutes", g.EffectiveConfig.SessionTimeoutMinutes),
                    new XAttribute("maxLoginAttempts", g.EffectiveConfig.MaxLoginAttempts),
                    new XAttribute("mfaRequiredForAdmin", g.EffectiveConfig.MfaRequiredForAdmin),
                    new XAttribute("mfaAllowedMethods", string.Join(",", g.EffectiveConfig.MfaAllowedMethods))),
                !proy.LlevaPermisos() ? null : new XElement("scopes",
                    g.Scopes.Select(s => new XElement("scope", s)))));

        var sb = new StringBuilder();
        doc.Save(new System.IO.StringWriter(sb));
        return sb.ToString();
    }
}
