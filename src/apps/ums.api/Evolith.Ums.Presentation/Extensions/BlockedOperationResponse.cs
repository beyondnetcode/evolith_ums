namespace Evolith.Ums.Presentation.Extensions;

using Evolith.Ums.Domain.Kernel;

/// <summary>
/// Structured API response returned when an operation is blocked by a dependency guard.
/// HTTP 409 Conflict.
/// </summary>
public sealed record BlockedOperationResponse(
    string ErrorCode,
    string Message,
    string BrokenRule,
    IReadOnlyList<BlockingDependency> BlockingDependencies);

/// <summary>
/// Maps error codes from DomainErrors to user-facing messages and broken-rule descriptions.
/// </summary>
internal static class BlockedOperationMessages
{
    private static readonly Dictionary<string, (string Message, string Rule)> _map = new()
    {
        [DomainErrors.Tenant.HasActiveUsers] = (
            "No se puede suspender el tenant porque tiene usuarios activos asociados.",
            "A tenant cannot be suspended while active users exist."),

        [DomainErrors.Tenant.HasActiveBranches] = (
            "No se puede suspender el tenant porque tiene sucursales activas.",
            "A tenant cannot be suspended while active branches exist."),

        // ADR-0164 §2.2: guarda de cascada del CIERRE DEFINITIVO de una sucursal. El desglose que
        // acompaña a este código dice cuántas cuentas y cuántos perfiles bloquean; el mensaje solo
        // tiene que decir qué hacer con ellos.
        [DomainErrors.Tenant.BranchHasLiveReferences] = (
            "No se puede cerrar la sucursal porque todavía tiene usuarios o perfiles activos asignados. Reasígnelos o desactívelos primero.",
            "A branch cannot be closed while active user accounts or profiles still reference it."),

        [DomainErrors.Tenant.HasActiveIdpConfig] = (
            "No se puede suspender el tenant porque tiene proveedores de identidad activos.",
            "A tenant cannot be suspended while active identity providers exist."),

        [DomainErrors.UserAccount.HasActiveProfiles] = (
            "No se puede bloquear o eliminar el usuario porque tiene perfiles activos asignados.",
            "A user cannot be blocked or deleted while active profiles are assigned."),

        [DomainErrors.Authorization.RoleHasActiveProfiles] = (
            "No se puede desactivar el rol porque tiene perfiles activos asignados.",
            "A role cannot be deactivated while active profiles are assigned to it."),

        [DomainErrors.Authorization.RoleHasActiveChildRoles] = (
            "No se puede desactivar el rol porque tiene roles hijos activos.",
            "A role cannot be deactivated while active child roles depend on it."),

        [DomainErrors.Authorization.TemplateHasActiveProfiles] = (
            "No se puede deprecar ni eliminar el template porque tiene perfiles activos asociados.",
            "A permission template cannot be deprecated or deleted while active profiles are linked to it."),

        // Guardia de cascada del borrado lógico de parámetros de inquilino: un parámetro activo es un
        // vínculo vivo de la configuración; hay que desactivarlo antes de poder eliminarlo.
        [DomainErrors.TenantParameter.HasActiveBinding] = (
            "No se puede eliminar el parámetro porque sigue activo en la configuración del inquilino; desactívelo primero.",
            "A tenant parameter cannot be deleted while it is still active in the tenant configuration."),

        [DomainErrors.Authorization.DomainResourceHasTemplateItems] = (
            "No se puede eliminar el recurso de dominio porque está referenciado en uno o más templates.",
            "A domain resource cannot be removed while active permission template items reference it."),

        [DomainErrors.Authorization.ModuleHasActiveMenus] = (
            "No se puede eliminar el módulo porque tiene menús activos configurados.",
            "A module cannot be removed while active menus are configured within it."),

        [DomainErrors.Authorization.SystemSuiteHasDependents] = (
            "No se puede eliminar el sistema porque todavía hay elementos vivos que dependen de él. Elimínelos primero.",
            "A system suite cannot be deleted while live roles, permission templates, feature flags, configurations, approval workflows or tenant defaults still reference it."),

        [DomainErrors.Configuration.ParameterHasActiveValues] = (
            "No se puede eliminar la definición de parámetro porque tiene valores globales o de inquilino asociados.",
            "A parameter definition cannot be deleted while global or tenant values reference it."),
    };

    public static string GetMessage(string errorCode)
        => _map.TryGetValue(errorCode, out var v) ? v.Message : "La operación fue bloqueada por una dependencia activa.";

    public static string GetRule(string errorCode)
        => _map.TryGetValue(errorCode, out var v) ? v.Rule : "Operation blocked by active dependency.";
}
