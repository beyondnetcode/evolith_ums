namespace Evolith.Ums.Application.Authorization.Profile.DTOs;

public static class PermissionTargetTypes
{
    public const string Module = "Module";
    public const string Submodule = "Submodule";
    public const string Page = "Page";
    public const string DomainResource = "DomainResource";
    public const string Aggregate = "Aggregate";
    public const string Entity = "Entity";
    public const string SystemAction = "SystemAction";

    public static bool IsNavigationPermission(string targetType)
        => targetType == Module || targetType == Submodule || targetType == Page;

    public static bool IsDomainResource(string targetType)
        => targetType == DomainResource || targetType == Aggregate || targetType == Entity;

    public static bool IsSystemAction(string targetType)
        => targetType == SystemAction;
}