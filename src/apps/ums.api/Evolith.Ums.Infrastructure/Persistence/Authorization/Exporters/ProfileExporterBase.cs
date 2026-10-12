using Evolith.Ums.Application.Authorization.Profile.DTOs;
using Evolith.Ums.Application.Authorization.Profile.Exporters;

namespace Evolith.Ums.Infrastructure.Persistence.Authorization.Exporters;

public abstract class ProfileExporterBase
{
    public static string GetEffect(bool isAllowed, bool isDenied)
    {
        if (isAllowed) return "Allow";
        if (isDenied) return "Deny";
        return "Neutral";
    }

    public static string FormatId(Guid id, ExportConfiguration config)
    {
        return config.MaskGuids ? MaskGuid(id) : id.ToString();
    }

    public static string? FormatId(Guid? id, ExportConfiguration config)
    {
        if (!id.HasValue) return null;
        return config.MaskGuids ? MaskGuid(id.Value) : id.Value.ToString();
    }

    protected static object MapPermission(ProfilePermissionDto p, ExportConfiguration config)
    {
        return new
        {
            id = FormatId(p.PermissionId, config),
            targetId = FormatId(p.TargetId, config),
            targetType = p.TargetType,
            targetName = p.TargetName,
            actionId = FormatId(p.ActionId, config),
            actionName = p.ActionName,
            effect = GetEffect(p.IsAllowed, p.IsDenied),
            isActive = p.IsActive,
            isOverride = p.IsOverride,
            originalFromTemplate = p.OriginalFromTemplate != null ? new
            {
                id = FormatId(p.OriginalFromTemplate.ItemId, config),
                targetId = FormatId(p.OriginalFromTemplate.TargetId, config),
                targetType = p.OriginalFromTemplate.TargetType,
                targetName = p.OriginalFromTemplate.TargetName,
                effect = GetEffect(p.OriginalFromTemplate.IsAllowed, p.OriginalFromTemplate.IsDenied),
                isActive = p.OriginalFromTemplate.IsActive
            } : null
        };
    }

    protected static object BuildEffectivePermissionsSummary(ProfileDto profile, ExportConfiguration config)
    {
        var totalPermissions = profile.Permissions.Count;
        var allowedPermissions = profile.Permissions.Count(p => p.IsAllowed);
        var deniedPermissions = profile.Permissions.Count(p => p.IsDenied);
        var activePermissions = profile.Permissions.Count(p => p.IsActive);
        var overriddenPermissions = profile.Permissions.Count(p => p.IsOverride);

        return new
        {
            totalPermissions,
            allowedPermissions,
            deniedPermissions,
            neutralPermissions = totalPermissions - allowedPermissions - deniedPermissions,
            activePermissions,
            overriddenPermissions
        };
    }

    protected static string MaskGuid(Guid guid) => "****-****-****-****";

    protected static List<object> FilterNavigationPermissions(IEnumerable<ProfilePermissionDto> permissions, ExportConfiguration config)
        => permissions.Where(p => PermissionTargetTypes.IsNavigationPermission(p.TargetType))
                      .Select(p => MapPermission(p, config))
                      .ToList();

    protected static List<object> FilterDomainResources(IEnumerable<ProfilePermissionDto> permissions, ExportConfiguration config)
        => permissions.Where(p => PermissionTargetTypes.IsDomainResource(p.TargetType))
                      .Select(p => MapPermission(p, config))
                      .ToList();

    protected static List<object> FilterSystemActions(IEnumerable<ProfilePermissionDto> permissions, ExportConfiguration config)
        => permissions.Where(p => PermissionTargetTypes.IsSystemAction(p.TargetType))
                      .Select(p => MapPermission(p, config))
                      .ToList();
}