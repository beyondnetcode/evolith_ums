using System.Text;
using Evolith.Ums.Application.Authorization.Graph.Serializers;
using Evolith.Ums.Domain.Authorization.Graph;

namespace Evolith.Ums.Infrastructure.Authorization.Graph;

/// <summary>Serializes AuthorizationGraph to YAML.</summary>
public sealed class YamlAuthorizationGraphSerializer : IAuthorizationGraphSerializer
{
    public string ContentType   => "application/x-yaml";
    public string FileExtension => "yaml";

    public string Serialize(AuthorizationGraph graph, GraphSerializationOptions? options = null)
    {
        var opts = options ?? GraphSerializationOptions.Default;
        var sb   = new StringBuilder();

        sb.AppendLine("# UMS Authorization Graph");
        sb.AppendLine($"# Generated: {graph.GeneratedAt:O}");
        sb.AppendLine($"# Valid Until: {graph.ValidUntil:O}");
        sb.AppendLine();
        sb.AppendLine($"schemaVersion: {graph.SchemaVersion}");
        sb.AppendLine();

        sb.AppendLine("context:");
        sb.AppendLine("  user:");
        if (opts.IncludeTechnicalMetadata) sb.AppendLine($"    id: {graph.Context.User.Id}");
        sb.AppendLine($"    email: {graph.Context.User.Email}");
        sb.AppendLine($"    username: {graph.Context.User.Username}");
        sb.AppendLine($"    value: {graph.Context.User.DisplayName}");
        sb.AppendLine($"    status: {graph.Context.User.Status}");
        sb.AppendLine("  tenant:");
        if (opts.IncludeTechnicalMetadata) sb.AppendLine($"    id: {graph.Context.Tenant.Id}");
        sb.AppendLine($"    code: {graph.Context.Tenant.Code}");
        sb.AppendLine($"    value: {graph.Context.Tenant.Name}");
        sb.AppendLine($"    status: {graph.Context.Tenant.Status}");
        sb.AppendLine($"    isManagementOwner: {graph.Context.Tenant.IsManagementOwner.ToString().ToLower()}");
        sb.AppendLine("  systemSuite:");
        if (opts.IncludeTechnicalMetadata) sb.AppendLine($"    id: {graph.Context.SystemSuite.Id}");
        sb.AppendLine($"    code: {graph.Context.SystemSuite.Code}");
        sb.AppendLine($"    value: {graph.Context.SystemSuite.Name}");
        sb.AppendLine($"    status: {graph.Context.SystemSuite.Status}");
        sb.AppendLine("  role:");
        if (opts.IncludeTechnicalMetadata) sb.AppendLine($"    id: {graph.Context.Role.Id}");
        sb.AppendLine($"    code: {graph.Context.Role.Code}");
        sb.AppendLine($"    value: {graph.Context.Role.Name}");
        sb.AppendLine($"    hierarchyLevel: {graph.Context.Role.HierarchyLevel}");
        sb.AppendLine("  profile:");
        if (opts.IncludeTechnicalMetadata) sb.AppendLine($"    id: {graph.Context.Profile.Id}");
        sb.AppendLine($"    scope: {graph.Context.Profile.Scope}");
        sb.AppendLine($"    isActive: {graph.Context.Profile.IsActive.ToString().ToLower()}");
        if (graph.Context.Branch is not null)
        {
            sb.AppendLine("  branch:");
            if (opts.IncludeTechnicalMetadata) sb.AppendLine($"    id: {graph.Context.Branch.Id}");
            sb.AppendLine($"    code: {graph.Context.Branch.Code}");
            sb.AppendLine($"    value: {graph.Context.Branch.Name}");
        }

        sb.AppendLine();
        sb.AppendLine("authentication:");
        sb.AppendLine($"  method: {graph.Authentication.Method}");
        sb.AppendLine($"  mfaRequired: {graph.Authentication.MfaRequired.ToString().ToLower()}");
        sb.AppendLine($"  issuedAt: {graph.Authentication.IssuedAt:O}");
        sb.AppendLine($"  sessionExpiresAt: {graph.Authentication.SessionExpiresAt:O}");
        if (graph.Authentication.Provider is not null)
        {
            sb.AppendLine("  provider:");
            if (opts.IncludeTechnicalMetadata) sb.AppendLine($"    id: {graph.Authentication.Provider.Id}");
            sb.AppendLine($"    code: {graph.Authentication.Provider.Code}");
            sb.AppendLine($"    name: {graph.Authentication.Provider.Name}");
            sb.AppendLine($"    value: {graph.Authentication.Provider.Strategy}");
        }

        sb.AppendLine();
        sb.AppendLine("actions:");
        foreach (var a in graph.Actions)
        {
            sb.AppendLine($"  - code: {a.Code}");
            sb.AppendLine($"    value: {a.Name}");
        }

        sb.AppendLine();
        sb.AppendLine("menuAccess:");
        foreach (var module in graph.MenuAccess)
        {
            sb.AppendLine($"  - code: {module.Code}");
            sb.AppendLine($"    value: {module.Name}");
            sb.AppendLine($"    status: {module.Status}");
            sb.AppendLine("    nodes:");
            EscribirNodos(sb, module.Nodes, "      ");
        }

        sb.AppendLine();
        sb.AppendLine("domainPermissions:");
        foreach (var res in graph.DomainPermissions)
        {
            sb.AppendLine("  -");
            if (opts.IncludeTechnicalMetadata) sb.AppendLine($"    id: {res.ResourceId}");
            if (opts.IncludeTechnicalMetadata && res.ModuleId.HasValue)
                sb.AppendLine($"    moduleId: {res.ModuleId.Value}");
            if (opts.IncludeTechnicalMetadata && res.ParentResourceId.HasValue)
                sb.AppendLine($"    parentResourceId: {res.ParentResourceId.Value}");
            sb.AppendLine($"    type: {res.ResourceType}");
            sb.AppendLine($"    code: {res.ResourceCode}");
            sb.AppendLine($"    value: {res.ResourceName}");
            sb.AppendLine("    actions:");
            foreach (var act in res.Actions)
            {
                sb.AppendLine("      -");
                if (opts.IncludeTechnicalMetadata) sb.AppendLine($"        id: {act.ActionId}");
                sb.AppendLine($"        code: {act.ActionCode}");
                sb.AppendLine($"        value: {act.ActionName}");
                sb.AppendLine($"        effect: {act.Effect}");
                sb.AppendLine($"        source: {act.Source}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("featureFlags:");
        foreach (var f in graph.FeatureFlags)
        {
            sb.AppendLine("  -");
            sb.AppendLine($"    flagCode: {f.FlagCode}");
            sb.AppendLine($"    systemSuiteId: {f.SystemSuiteId}");
            sb.AppendLine($"    isEnabled: {f.IsEnabled.ToString().ToLower()}");
            if (!string.IsNullOrWhiteSpace(f.MatchedCriteriaType))
                sb.AppendLine($"    matchedCriteriaType: {f.MatchedCriteriaType}");
        }

        sb.AppendLine();
        sb.AppendLine("effectiveConfig:");
        sb.AppendLine($"  sessionTimeoutMinutes: {graph.EffectiveConfig.SessionTimeoutMinutes}");
        sb.AppendLine($"  maxLoginAttempts: {graph.EffectiveConfig.MaxLoginAttempts}");
        sb.AppendLine($"  minPasswordLength: {graph.EffectiveConfig.MinPasswordLength}");
        sb.AppendLine($"  mfaRequiredForAdmin: {graph.EffectiveConfig.MfaRequiredForAdmin.ToString().ToLower()}");
        sb.AppendLine($"  accessTokenDurationMs: {graph.EffectiveConfig.AccessTokenDurationMs}");
        sb.AppendLine($"  authUseExternalIdp: {graph.EffectiveConfig.AuthUseExternalIdp.ToString().ToLower()}");

        sb.AppendLine();
        sb.AppendLine("scopes:");
        foreach (var scope in graph.Scopes)
            sb.AppendLine($"  - {scope}");

        return sb.ToString();
    }

    /// <summary>
    /// Escribe el árbol de navegación con sangría creciente. Recursivo porque el árbol no tiene
    /// profundidad fija (ADR-0090): la versión anterior tenía tres bucles anidados y perdía en
    /// silencio cualquier nodo que no encajara en ellos.
    /// </summary>
    private static void EscribirNodos(
        System.Text.StringBuilder sb,
        IEnumerable<Evolith.Ums.Domain.Authorization.Graph.GraphNavigationNode> nodos,
        string sangria)
    {
        foreach (var nodo in nodos)
        {
            sb.AppendLine($"{sangria}- code: {nodo.Code}");
            sb.AppendLine($"{sangria}  value: {nodo.Name}");
            sb.AppendLine($"{sangria}  kind: {nodo.Kind}");

            if (nodo.Actions.Count > 0)
            {
                sb.AppendLine($"{sangria}  actions:");
                foreach (var accion in nodo.Actions)
                {
                    sb.AppendLine($"{sangria}    - actionCode: {accion.ActionCode}");
                    sb.AppendLine($"{sangria}      effect: {accion.Effect}");
                    sb.AppendLine($"{sangria}      source: {accion.Source}");
                }
            }

            if (nodo.Children.Count > 0)
            {
                sb.AppendLine($"{sangria}  children:");
                EscribirNodos(sb, nodo.Children, sangria + "    ");
            }
        }
    }
}
