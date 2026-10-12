using System.Text;
using Evolith.Ums.Application.Authorization.Graph.Serializers;
using Evolith.Ums.Domain.Authorization.Graph;

namespace Evolith.Ums.Infrastructure.Authorization.Graph;

/// <summary>
/// Serializes AuthorizationGraph to CSV.
/// Produces a flat table of permissions (one row per option/action).
/// The first block is menu permissions, the second is domain resource permissions.
/// </summary>
public sealed class CsvAuthorizationGraphSerializer : IAuthorizationGraphSerializer
{
    public string ContentType   => "text/csv";
    public string FileExtension => "csv";

    public string Serialize(AuthorizationGraph graph, GraphSerializationOptions? options = null)
    {
        var opts = options ?? GraphSerializationOptions.Default;
        var sb   = new StringBuilder();

        sb.AppendLine($"# UMS Authorization Graph — {graph.Context.Tenant.Code} / {graph.Context.SystemSuite.Code} / {graph.Context.Role.Code}");
        sb.AppendLine($"# Generated: {graph.GeneratedAt:O}  ValidUntil: {graph.ValidUntil:O}");
        sb.AppendLine($"# Auth: {graph.Authentication.Method}  MFA: {graph.Authentication.MfaRequired}");
        sb.AppendLine();

        // El árbol admite cualquier profundidad, así que la columna que lo identifica es la RUTA
        // completa (`MENU/SUBMENU/OPCION`), no tres columnas fijas que se quedarían cortas.
        sb.AppendLine("Section,ModuleCode,ModuleValue,NodePath,NodeCode,NodeValue,NodeKind,ActionCode,Effect,Source");
        foreach (var module in graph.MenuAccess)
        {
            foreach (var (nodo, ruta) in Aplanar(module.Nodes, string.Empty))
            {
                foreach (var accion in nodo.Actions)
                {
                    sb.AppendLine(string.Join(",",
                        "Menu",
                        Esc(module.Code), Esc(module.Name),
                        Esc(ruta), Esc(nodo.Code), Esc(nodo.Name), Esc(nodo.Kind),
                        Esc(accion.ActionCode),
                        accion.Effect.ToString(),
                        accion.Source.ToString()));
                }
            }
        }

        sb.AppendLine();

        sb.AppendLine("Section,ResourceType,ResourceCode,ResourceValue,ActionCode,ActionValue,Effect,Source");
        foreach (var (res, act) in graph.DomainPermissions
                     .SelectMany(res => res.Actions, (res, act) => (res, act)))
        {
            sb.AppendLine(string.Join(",",
                "Domain",
                Esc(res.ResourceType), Esc(res.ResourceCode), Esc(res.ResourceName),
                Esc(act.ActionCode), Esc(act.ActionName),
                act.Effect.ToString(), act.Source.ToString()));
        }

        sb.AppendLine();

        sb.AppendLine("Section,FlagCode,IsEnabled,MatchedCriteria");
        foreach (var f in graph.FeatureFlags)
        {
            sb.AppendLine(string.Join(",",
                "Feature",
                Esc(f.FlagCode),
                f.IsEnabled.ToString().ToLower(),
                Esc(f.MatchedCriteriaType ?? "")));
        }

        sb.AppendLine();

        sb.AppendLine("Section,Scope");
        foreach (var s in graph.Scopes)
            sb.AppendLine($"Scope,{Esc(s)}");

        if (opts.IncludeTechnicalMetadata)
        {
            sb.AppendLine();
            sb.AppendLine("Section,TechnicalField,Value");
            sb.AppendLine($"Technical,UserId,{Esc(graph.Context.User.Id.ToString())}");
            sb.AppendLine($"Technical,TenantId,{Esc(graph.Context.Tenant.Id.ToString())}");
            sb.AppendLine($"Technical,SystemSuiteId,{Esc(graph.Context.SystemSuite.Id.ToString())}");
            sb.AppendLine($"Technical,RoleId,{Esc(graph.Context.Role.Id.ToString())}");
            if (graph.Context.Branch is not null)
                sb.AppendLine($"Technical,BranchId,{Esc(graph.Context.Branch.Id.ToString())}");
        }

        return sb.ToString();
    }

    /// <summary>Recorre el árbol devolviendo cada nodo con su ruta de códigos.</summary>
    private static IEnumerable<(Evolith.Ums.Domain.Authorization.Graph.GraphNavigationNode Nodo, string Ruta)> Aplanar(
        IEnumerable<Evolith.Ums.Domain.Authorization.Graph.GraphNavigationNode> nodos, string prefijo)
    {
        foreach (var nodo in nodos)
        {
            var ruta = string.IsNullOrEmpty(prefijo) ? nodo.Code : $"{prefijo}/{nodo.Code}";
            yield return (nodo, ruta);
            foreach (var hijo in Aplanar(nodo.Children, ruta)) yield return hijo;
        }
    }

    private static string Esc(string? v)
    {
        if (v is null) return "";
        if (v.Contains(',') || v.Contains('"') || v.Contains('\n'))
            return $"\"{v.Replace("\"", "\"\"")}\"";
        return v;
    }
}
