namespace Evolith.Ums.Application.IGA.Common;

/// <summary>
/// Guardas compartidas por los handlers del contexto acotado IGA (ADR-UMS-093):
/// autenticación, acotación por inquilino y segregación de funciones (SoD) a nivel de aplicación.
/// La SoD también es invariante de dominio (INV-RPR3); aquí se hace cumplir <em>antes</em> de tocar
/// el agregado para devolver un fallo temprano y legible, cerrando el hueco que la auditoría halló
/// en Aprobaciones (el actor no puede aprobar/ejecutar/verificar su propia promoción).
/// </summary>
internal static class IgaHandlerGuards
{
    /// <summary>Exige un usuario autenticado y devuelve su identificador como <see cref="Guid"/>.</summary>
    public static Result<Guid> RequireAuthenticatedUser(IUserContext userContext)
    {
        if (string.IsNullOrWhiteSpace(userContext.UserId))
        {
            return Result<Guid>.Failure("Se requiere un usuario autenticado para operar sobre una promoción de rol.");
        }

        if (!Guid.TryParse(userContext.UserId, out var userId))
        {
            return Result<Guid>.Failure("El identificador del usuario autenticado no es válido.");
        }

        return Result<Guid>.Success(userId);
    }

    /// <summary>
    /// Verifica que el inquilino de la solicitud esté dentro del alcance visible del solicitante.
    /// Un administrador interno (alcance nulo = multi-inquilino) puede operar sobre cualquier inquilino;
    /// un usuario regular sólo sobre el suyo.
    /// </summary>
    public static Result EnsureTenantInScope(ITenantScopePolicy tenantScopePolicy, Guid tenantId)
    {
        var scope = tenantScopePolicy.ResolveQueryScope();
        if (scope is null || scope.Value == tenantId)
        {
            return Result.Success();
        }

        return Result.Failure("La solicitud de promoción pertenece a otro inquilino y está fuera de su alcance.");
    }

    /// <summary>SoD: el actor no puede coincidir con ninguno de los sujetos indicados (objetivo, aprobador, etc.).</summary>
    public static Result EnsureSegregation(Guid actorId, params (Guid? Subject, string Message)[] conflicts)
    {
        foreach (var (subject, message) in conflicts)
        {
            if (subject is not null && subject.Value == actorId)
            {
                return Result.Failure(message);
            }
        }

        return Result.Success();
    }
}
