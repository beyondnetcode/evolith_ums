using System.Text;
using Evolith.Ums.Application.Authorization.Profile.DTOs;
using Evolith.Ums.Application.Authorization.Profile.Exporters;

namespace Evolith.Ums.Infrastructure.Persistence.Authorization.Exporters;

public sealed class ProfileCsvExporter : IProfileExporter
{
    public string ContentType => "text/csv";
    public string FileExtension => "csv";

    public string Export(ProfileDto profile, ExportConfiguration? configuration = null)
    {
        var config = configuration ?? ExportConfiguration.Default;
        var sb = new StringBuilder();

        if (config.IncludeTechnicalMetadata)
        {
            sb.AppendLine("# Profile Authorization Graph Export - CSV Format");
            sb.AppendLine($"# Generated: {DateTime.UtcNow:O}");
            sb.AppendLine($"# Tenant: {profile.TenantCode} ({profile.TenantId})");
            sb.AppendLine($"# Profile: {profile.RoleCode} ({profile.ProfileId})");
            sb.AppendLine();
        }

        sb.AppendLine("TargetType,TargetName,ActionName,Effect,IsActive,IsOverride");

        foreach (var p in profile.Permissions)
        {
            var targetNameEscaped = p.TargetName.Replace("\"", "\"\"");
            var actionNameEscaped = p.ActionName.Replace("\"", "\"\"");
            var effect = ProfileExporterBase.GetEffect(p.IsAllowed, p.IsDenied);

            sb.AppendLine($"{p.TargetType},\"{targetNameEscaped}\",\"{actionNameEscaped}\",{effect},{p.IsActive},{p.IsOverride}");
        }

        if (config.IncludeEffectivePermissionsSummary)
        {
            sb.AppendLine();
            sb.AppendLine("# Effective Permissions Summary");
            var totalPermissions = profile.Permissions.Count;
            var allowedPermissions = profile.Permissions.Count(p => p.IsAllowed);
            var deniedPermissions = profile.Permissions.Count(p => p.IsDenied);
            var activePermissions = profile.Permissions.Count(p => p.IsActive);

            sb.AppendLine($"TotalPermissions,{totalPermissions}");
            sb.AppendLine($"AllowedPermissions,{allowedPermissions}");
            sb.AppendLine($"DeniedPermissions,{deniedPermissions}");
            sb.AppendLine($"NeutralPermissions,{totalPermissions - allowedPermissions - deniedPermissions}");
            sb.AppendLine($"ActivePermissions,{activePermissions}");
        }

        return sb.ToString();
    }
}
