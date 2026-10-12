namespace Evolith.Ums.Presentation.Extensions;

using Evolith.Ums.Domain.Kernel;
using Microsoft.AspNetCore.Http;

internal static class DomainErrorStatusMapper
{
    public static (int Status, string Title) Map(string error)
    {
        if (string.IsNullOrEmpty(error))
        {
            return (StatusCodes.Status400BadRequest, "Bad Request");
        }

        if (error.StartsWith("Validation.Failed:", StringComparison.OrdinalIgnoreCase))
        {
            return (StatusCodes.Status400BadRequest, "Validation Error");
        }

        // ADR-UMS-095: cuenta con bloqueo temporal por intentos fallidos → 423 Locked.
        // AUTH_017 (AUTH_012 ya está tomado con el significado «no IDP adapter registered»).
        if (error.StartsWith("AUTH_017", StringComparison.OrdinalIgnoreCase))
        {
            return (StatusCodes.Status423Locked, "Locked");
        }

        // Detección de «no encontrado» → 404 por CÓDIGO de dominio estable e idioma-agnóstico
        // (DomainErrors.*), no por el texto del mensaje. G-100: el brazo de substring en inglés de
        // abajo no clasificaba los errores que el dominio devuelve en español (p. ej. IGA:
        // «No se encontró la solicitud de promoción de rol.»), colapsándolos al 400 por defecto.
        // Los handlers deben devolver el código; el mapeo depende de él, no del idioma del mensaje.
        //
        // G-104: se añaden los códigos de fin en not_found que direccionan un recurso por id o clave y
        // que hoy caían al 400 por defecto porque ni estaban en esta lista ni contenían la frase inglesa
        // «not found». Son CriteriaNotFound —al quitar un criterio de feature flag inexistente—,
        // MfaEnrollmentNotFound —al revocar una inscripción de MFA inexistente— y TenantParameter.NotFound
        // —parámetro de inquilino direccionado por su código—. Los dos endpoints reales ya declaraban 404
        // en su contrato OpenAPI; el mapeo por código lo hace efectivo en lugar de contradecirlo con 400.
        //
        // G-104, EXCLUIDOS DELIBERADAMENTE, NO son 404 y no deben re-cablearse: IdpFallbackNotFound y
        // ParentResourceNotFound NO direccionan el recurso de la URI, sino que validan una referencia
        // colgante en el cuerpo de un alta —un identificador de fallback o de recurso padre que apunta a
        // algo inexistente—. El recurso objetivo del alta sí existe o se está creando; lo que falla es la
        // integridad referencial del payload, que es validación 400 —semánticamente un 422—, nunca 404.
        // Sus endpoints declaran 400 y no 404 en el contrato, así que se dejan en el 400 por defecto.
        if (ContainsAny(error, DomainErrors.Common.NotFound, DomainErrors.Tenant.NotFound, DomainErrors.Tenant.BranchNotFound, DomainErrors.Tenant.IdpNotFound, DomainErrors.SystemSuite.ConfigurationKeyNotFound, DomainErrors.Authorization.PermissionNotFound, DomainErrors.IGA.RolePromotionRequestNotFound, DomainErrors.Configuration.CriteriaNotFound, DomainErrors.UserAccount.MfaEnrollmentNotFound, DomainErrors.TenantParameter.NotFound))
        {
            return (StatusCodes.Status404NotFound, "Not Found");
        }

        // Fallback heredado para handlers que aún incrustan la frase en inglés «not found» en el mensaje
        // (Tenant/UserAccount/Configuration…). Es dependiente del idioma y sólo se mantiene por
        // compatibilidad; la vía correcta es el código de dominio del bloque anterior (G-100).
        if (error.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return (StatusCodes.Status404NotFound, "Not Found");
        }

        // G-246: eliminar lógicamente un sistema que sigue vigente, que aún tiene referencias vivas o
        // que ya está eliminado es un conflicto con el estado actual del recurso → 409. Lo mismo para
        // los dos intentos de esquivar la guarda por `PUT /status` (entrar a «eliminado» o salir de
        // él). SystemSuiteHasDependents sale normalmente por la vía del BlockedOperationResponse (409
        // con el desglose de qué bloquea); se lista aquí para que el camino sin desglose no lo
        // degrade a 400.
        if (ContainsAny(
                error,
                DomainErrors.Authorization.SystemSuiteNotDeprecated,
                DomainErrors.Authorization.SystemSuiteHasDependents,
                DomainErrors.Authorization.SystemSuiteAlreadyDeleted,
                DomainErrors.Authorization.SystemSuiteDeletedNotSettable,
                DomainErrors.Authorization.SystemSuiteDeletedIsTerminal))
        {
            return (StatusCodes.Status409Conflict, "Conflict");
        }

        // ── Borrado lógico: guardias de cascada y estados terminales → 409 ────
        // Solo existe borrado lógico. Dos familias de conflicto salen de ahí y ambas son 409, no 400:
        //  · Guardia de cascada: se intenta eliminar algo con referencias VIVAS (perfiles activos que
        //    usan la plantilla, vínculo activo del parámetro de inquilino). Es el análogo de un
        //    ON DELETE RESTRICT y sale además enriquecido con BlockedOperationResponse cuando el
        //    handler adjunta las dependencias; este brazo cubre el caso sin dependencias adjuntas.
        //  · Estado terminal ya alcanzado: reintentar el borrado de algo ya eliminado es un conflicto
        //    con el estado actual del recurso, igual que Tenant.AlreadySuspended.
        //
        // ADR-0164 aplicado a SUCURSALES: cerrar una con referencias vivas (BranchHasLiveReferences),
        // reintentar el cierre de una ya cerrada (BranchAlreadyClosed) e intentar desactivar o
        // reactivar una cerrada (BranchClosed) son las tres conflictos con el estado actual del
        // recurso → 409. `BranchClosed` importa especialmente: sin él, «no se puede reactivar lo
        // cerrado» caería al 400 por defecto y se confundiría con un error de validación del cliente.
        // `TemplateNotDeletable` y `ParameterAlreadyDeleted` faltaban y caían al 400 por omisión, en
        // contra del `ProducesProblem(409)` que declaran sus propios endpoints. La primera dice que el
        // estado de la plantilla no admite el borrado —conflicto con el estado actual, no un error de
        // validación del cliente— y la segunda es el reintento sobre algo ya eliminado, exactamente el
        // caso que la línea de al lado ya cubría para plantillas y parámetros de inquilino.
        if (ContainsAny(error, DomainErrors.Authorization.TemplateHasActiveProfiles, DomainErrors.Authorization.TemplateAlreadyDeleted, DomainErrors.Authorization.TemplateNotDeletable, DomainErrors.Configuration.ParameterAlreadyDeleted, DomainErrors.TenantParameter.HasActiveBinding, DomainErrors.TenantParameter.AlreadyDeleted, DomainErrors.Tenant.BranchHasLiveReferences, DomainErrors.Tenant.BranchAlreadyClosed, DomainErrors.Tenant.BranchClosed))
        {
            return (StatusCodes.Status409Conflict, "Conflict");
        }

        // Conflictos de estado idempotentes del ciclo de vida del tenant: reintentar una transición
        // hacia el estado ya vigente (suspender lo ya suspendido, activar lo ya activo) es un conflicto
        // con el estado actual del recurso → 409, coherente con el resto de duplicados/"already". Antes
        // caían al default 400 (G-014 residual: SuspendTenant_AlreadySuspended esperaba 409).
        // ManagementOwnerAlreadyExists (G-037/G-045): segundo owner de gestión → 409.
        // G-121: los conflictos de TRANSICIÓN de estado del ciclo de vida de documentos y solicitudes
        // (aprobar una request no-Pending, validar/expirar un documento en estado incompatible) son
        // conflictos con el estado actual del recurso → 409, coherente con DocumentAlreadyExpired y con
        // el ProducesProblem(409) que declaran sus endpoints (antes caían mezclados a 400).
        // ADR-0164: TemplateItemTargetRetired es el mismo tipo de conflicto —la clave natural del ítem
        // sigue ocupada por una concesión retirada— y por eso comparte 409; se mantiene como código
        // propio para que el cliente pueda proponer reactivar en vez de repetir el alta.
        if (ContainsAny(error, DomainErrors.Common.Duplicate, DomainErrors.Tenant.SignupRequestAlreadyExists, DomainErrors.Tenant.BranchCodeNotUnique, DomainErrors.Tenant.IdpCodeNotUnique, DomainErrors.Tenant.AlreadyActive, DomainErrors.Tenant.AlreadySuspended, DomainErrors.Tenant.ManagementOwnerAlreadyExists, DomainErrors.UserAccount.EmailNotUnique, DomainErrors.SystemSuite.OptionCodeNotUnique, DomainErrors.SystemSuite.SubMenuCodeNotUnique, DomainErrors.SystemSuite.MenuCodeNotUnique, DomainErrors.SystemSuite.ModuleCodeNotUnique, DomainErrors.SystemSuite.ConfigurationKeyAlreadyExists, DomainErrors.Configuration.ParameterCodeNotUnique, DomainErrors.Configuration.ParameterHasActiveValues, DomainErrors.Authorization.TemplateItemTargetAlreadyExists, DomainErrors.Authorization.TemplateItemTargetRetired, DomainErrors.Authorization.PermissionAlreadyExists, DomainErrors.Compliance.DocumentAlreadyExpired, DomainErrors.Approvals.DocumentTypeAlreadyRequired, DomainErrors.Compliance.DocumentNotPendingReview, DomainErrors.Compliance.DocumentCannotTransition, DomainErrors.Approvals.RequestNotPending))
        {
            return (StatusCodes.Status409Conflict, "Conflict");
        }

        if (error.Contains("already exists", StringComparison.OrdinalIgnoreCase))
        {
            return (StatusCodes.Status409Conflict, "Conflict");
        }

        if (ContainsAny(error, DomainErrors.Common.Required, DomainErrors.Tenant.Required, DomainErrors.ValueObject.PropertyRequired, DomainErrors.ValueObject.EmailRequired, DomainErrors.UserAccount.PasswordHashRequired, DomainErrors.Audit.WhatChangedRequired, DomainErrors.Audit.AffectedEntityRequired))
        {
            return (StatusCodes.Status400BadRequest, "Validation Error");
        }

        if (ContainsAny(error, DomainErrors.Common.Invalid, DomainErrors.UserAccount.InvalidEmail, DomainErrors.Tenant.SignupRequestNotPending, DomainErrors.Tenant.SignupRequestAlreadyProcessed, DomainErrors.Configuration.IdpConfigPayloadInvalid, DomainErrors.Configuration.FlagPercentageOutOfRange, DomainErrors.Configuration.AppConfigNotDraft, DomainErrors.Configuration.AppConfigNotPublished, DomainErrors.Configuration.FlagArchivedCannotChange, DomainErrors.Configuration.AppConfigAlreadyArchived, DomainErrors.Compliance.ExpirationBeforeIssueDate, DomainErrors.Approvals.PolicyInactiveCannotUpdate, DomainErrors.ValueObject.DateRangeInvalid))
        {
            return (StatusCodes.Status400BadRequest, "Validation Error");
        }

        if (error.Contains("unauthorized", StringComparison.OrdinalIgnoreCase) || error.Contains("forbidden", StringComparison.OrdinalIgnoreCase))
        {
            return (StatusCodes.Status403Forbidden, "Forbidden");
        }

        if (error.Contains("authenticated user is required", StringComparison.OrdinalIgnoreCase))
        {
            return (StatusCodes.Status401Unauthorized, "Unauthorized");
        }

        return (StatusCodes.Status400BadRequest, "Bad Request");
    }

    private static bool ContainsAny(string error, params string[] values)
        => values.Any(value => error.Contains(value, StringComparison.OrdinalIgnoreCase));
}
