using System.Text;
using System.Text.Json;
using Evolith.Ums.Application.Authorization.Profile.DTOs;
using Evolith.Ums.Application.Authorization.Profile.Exporters;

namespace Evolith.Ums.Infrastructure.Persistence.Authorization.Exporters;

public sealed class ProfileYamlExporter : IProfileExporter
{
    public string ContentType => "application/x-yaml";
    public string FileExtension => "yaml";

    public string Export(ProfileDto profile, ExportConfiguration? configuration = null)
    {
        var config = configuration ?? ExportConfiguration.Default;
        var sb = new StringBuilder();

        sb.AppendLine("# Profile Authorization Graph Export");
        sb.AppendLine($"# Generated: {DateTime.UtcNow:O}");
        sb.AppendLine();

        if (config.IncludeTechnicalMetadata)
        {
            sb.AppendLine("tenant:");
            sb.AppendLine($"  id: {(config.MaskGuids ? "****-****-****-****" : profile.TenantId)}");
            sb.AppendLine($"  code: {profile.TenantCode}");
            sb.AppendLine($"  name: {profile.TenantName}");
            sb.AppendLine($"  status: Active");
            sb.AppendLine();
            sb.AppendLine("system:");
            sb.AppendLine($"  id: {(config.MaskGuids ? "****-****-****-****" : profile.SystemSuiteId)}");
            sb.AppendLine($"  code: {profile.SystemSuiteCode}");
            sb.AppendLine($"  name: {profile.SystemSuiteName}");
            sb.AppendLine($"  status: Active");
            sb.AppendLine();
            sb.AppendLine("user:");
            sb.AppendLine($"  id: {(config.MaskGuids ? "****-****-****-****" : profile.UserId)}");
            sb.AppendLine($"  email: {profile.UserEmail}");
            sb.AppendLine($"  status: Active");
            sb.AppendLine();
        }

        sb.AppendLine("profile:");
        sb.AppendLine($"  id: {(config.MaskGuids ? "****-****-****-****" : profile.ProfileId)}");
        sb.AppendLine($"  role: {profile.RoleCode}");
        sb.AppendLine($"  name: {profile.RoleName}");
        sb.AppendLine($"  status: {(profile.IsActive ? "Active" : "Inactive")}");
        sb.AppendLine($"  scope: {profile.Scope}");
        sb.AppendLine($"  permissionCount: {profile.PermissionCount}");
        sb.AppendLine();

        sb.AppendLine("authorizationGraph:");

        var navigationPermissions = profile.Permissions
            .Where(p => p.TargetType == "Module" || p.TargetType == "Submodule" || p.TargetType == "Page")
            .ToList();

        if (navigationPermissions.Any())
        {
            sb.AppendLine("  modules:");
            foreach (var p in navigationPermissions)
            {
                sb.AppendLine("    - id: " + (config.MaskGuids ? "****" : p.PermissionId));
                sb.AppendLine($"      targetType: {p.TargetType}");
                sb.AppendLine($"      targetName: {p.TargetName}");
                sb.AppendLine($"      actionName: {p.ActionName}");
                sb.AppendLine($"      effect: {(ProfileExporterBase.GetEffect(p.IsAllowed, p.IsDenied))}");
                sb.AppendLine($"      isActive: {p.IsActive}");
                sb.AppendLine($"      isOverride: {p.IsOverride}");
            }
        }

        var domainResources = profile.Permissions
            .Where(p => p.TargetType == "DomainResource" || p.TargetType == "Aggregate" || p.TargetType == "Entity")
            .ToList();

        if (domainResources.Any())
        {
            sb.AppendLine("  domainResources:");
            foreach (var p in domainResources)
            {
                sb.AppendLine("    - id: " + (config.MaskGuids ? "****" : p.PermissionId));
                sb.AppendLine($"      targetType: {p.TargetType}");
                sb.AppendLine($"      targetName: {p.TargetName}");
                sb.AppendLine($"      actionName: {p.ActionName}");
                sb.AppendLine($"      effect: {(ProfileExporterBase.GetEffect(p.IsAllowed, p.IsDenied))}");
                sb.AppendLine($"      isActive: {p.IsActive}");
                sb.AppendLine($"      isOverride: {p.IsOverride}");
            }
        }

        var systemActions = profile.Permissions
            .Where(p => p.TargetType == "SystemAction")
            .ToList();

        if (systemActions.Any())
        {
            sb.AppendLine("  systemActions:");
            foreach (var p in systemActions)
            {
                sb.AppendLine("    - id: " + (config.MaskGuids ? "****" : p.PermissionId));
                sb.AppendLine($"      targetType: {p.TargetType}");
                sb.AppendLine($"      targetName: {p.TargetName}");
                sb.AppendLine($"      actionName: {p.ActionName}");
                sb.AppendLine($"      effect: {(ProfileExporterBase.GetEffect(p.IsAllowed, p.IsDenied))}");
                sb.AppendLine($"      isActive: {p.IsActive}");
                sb.AppendLine($"      isOverride: {p.IsOverride}");
            }
        }

        if (config.IncludeEffectivePermissionsSummary)
        {
            var totalPermissions = profile.Permissions.Count;
            var allowedPermissions = profile.Permissions.Count(p => p.IsAllowed);
            var deniedPermissions = profile.Permissions.Count(p => p.IsDenied);
            var activePermissions = profile.Permissions.Count(p => p.IsActive);
            var overriddenPermissions = profile.Permissions.Count(p => p.IsOverride);

            sb.AppendLine("  effectivePermissionsSummary:");
            sb.AppendLine($"    totalPermissions: {totalPermissions}");
            sb.AppendLine($"    allowedPermissions: {allowedPermissions}");
            sb.AppendLine($"    deniedPermissions: {deniedPermissions}");
            sb.AppendLine($"    neutralPermissions: {totalPermissions - allowedPermissions - deniedPermissions}");
            sb.AppendLine($"    activePermissions: {activePermissions}");
            sb.AppendLine($"    overriddenPermissions: {overriddenPermissions}");
        }

        if (config.IncludeTechnicalMetadata)
        {
            sb.AppendLine();
            sb.AppendLine("session:");
            sb.AppendLine($"  generatedAt: {DateTime.UtcNow:O}");
            sb.AppendLine($"  generatedBy: System");
            sb.AppendLine($"  tenantId: {(config.MaskGuids ? "****" : profile.TenantId)}");
            sb.AppendLine($"  sessionTrackingId: {Guid.NewGuid()}");
            sb.AppendLine($"  correlationId: {Guid.NewGuid()}");
            sb.AppendLine("  language: en");
            sb.AppendLine("  environment: Production");
        }

        return sb.ToString();
    }
}