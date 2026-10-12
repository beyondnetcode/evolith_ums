using System.Xml.Linq;
using Evolith.Ums.Application.Authorization.Profile.DTOs;
using Evolith.Ums.Application.Authorization.Profile.Exporters;

namespace Evolith.Ums.Infrastructure.Persistence.Authorization.Exporters;

public sealed class ProfileXmlExporter : IProfileExporter
{
    public string ContentType => "application/xml";
    public string FileExtension => "xml";

    public string Export(ProfileDto profile, ExportConfiguration? configuration = null)
    {
        var config = configuration ?? ExportConfiguration.Default;

        var permissionsElement = new XElement("Permissions");
        foreach (var p in profile.Permissions)
        {
            var permissionElement = new XElement("Permission",
                new XAttribute("Id", config.MaskGuids ? "****" : p.PermissionId.ToString()),
                new XElement("TargetType", p.TargetType),
                new XElement("TargetName", p.TargetName),
                new XElement("ActionName", p.ActionName),
                new XElement("Effect", ProfileExporterBase.GetEffect(p.IsAllowed, p.IsDenied)),
                new XElement("IsActive", p.IsActive),
                new XElement("IsOverride", p.IsOverride)
            );
            permissionsElement.Add(permissionElement);
        }

        var root = new XElement("ProfileAuthorization");

        if (config.IncludeTechnicalMetadata)
        {
            root.Add(new XElement("Tenant",
                new XElement("Id", config.MaskGuids ? "****" : profile.TenantId.ToString()),
                new XElement("Code", profile.TenantCode),
                new XElement("Name", profile.TenantName)));
            root.Add(new XElement("System",
                new XElement("Id", config.MaskGuids ? "****" : profile.SystemSuiteId.ToString()),
                new XElement("Code", profile.SystemSuiteCode),
                new XElement("Name", profile.SystemSuiteName)));
            root.Add(new XElement("User",
                new XElement("Id", config.MaskGuids ? "****" : profile.UserId.ToString()),
                new XElement("Email", profile.UserEmail)));
        }

        root.Add(new XElement("Profile",
            new XElement("Id", config.MaskGuids ? "****" : profile.ProfileId.ToString()),
            new XElement("Role", profile.RoleCode),
            new XElement("Name", profile.RoleName),
            new XElement("Status", profile.IsActive ? "Active" : "Inactive"),
            new XElement("Scope", profile.Scope),
            new XElement("PermissionCount", profile.PermissionCount)));

        var graphElement = new XElement("AuthorizationGraph");

        var modulesElement = new XElement("Modules");
        foreach (var p in profile.Permissions.Where(p => p.TargetType == "Module" || p.TargetType == "Submodule" || p.TargetType == "Page"))
        {
            modulesElement.Add(new XElement("Module",
                new XElement("TargetName", p.TargetName),
                new XElement("ActionName", p.ActionName),
                new XElement("Effect", ProfileExporterBase.GetEffect(p.IsAllowed, p.IsDenied))));
        }
        graphElement.Add(modulesElement);

        var domainElement = new XElement("DomainResources");
        foreach (var p in profile.Permissions.Where(p => p.TargetType == "DomainResource" || p.TargetType == "Aggregate" || p.TargetType == "Entity"))
        {
            domainElement.Add(new XElement("Resource",
                new XElement("TargetName", p.TargetName),
                new XElement("ActionName", p.ActionName),
                new XElement("Effect", ProfileExporterBase.GetEffect(p.IsAllowed, p.IsDenied))));
        }
        graphElement.Add(domainElement);

        var actionsElement = new XElement("SystemActions");
        foreach (var p in profile.Permissions.Where(p => p.TargetType == "SystemAction"))
        {
            actionsElement.Add(new XElement("Action",
                new XElement("TargetName", p.TargetName),
                new XElement("ActionName", p.ActionName),
                new XElement("Effect", ProfileExporterBase.GetEffect(p.IsAllowed, p.IsDenied))));
        }
        graphElement.Add(actionsElement);

        if (config.IncludeEffectivePermissionsSummary)
        {
            var totalPermissions = profile.Permissions.Count;
            var allowedPermissions = profile.Permissions.Count(p => p.IsAllowed);
            var deniedPermissions = profile.Permissions.Count(p => p.IsDenied);

            graphElement.Add(new XElement("EffectivePermissionsSummary",
                new XElement("TotalPermissions", totalPermissions),
                new XElement("AllowedPermissions", allowedPermissions),
                new XElement("DeniedPermissions", deniedPermissions),
                new XElement("NeutralPermissions", totalPermissions - allowedPermissions - deniedPermissions)));
        }

        root.Add(graphElement);

        if (config.IncludeTechnicalMetadata)
        {
            root.Add(new XElement("Session",
                new XElement("GeneratedAt", DateTime.UtcNow.ToString("O")),
                new XElement("GeneratedBy", "System"),
                new XElement("TenantId", config.MaskGuids ? "****" : profile.TenantId.ToString()),
                new XElement("SessionTrackingId", Guid.NewGuid().ToString()),
                new XElement("CorrelationId", Guid.NewGuid().ToString()),
                new XElement("Language", "en"),
                new XElement("Environment", "Production")));
        }

        return new XDocument(root).ToString();
    }
}
