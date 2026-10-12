namespace Evolith.Ums.Application.Authorization.SystemSuite.Commands;

using Evolith.Ums.Domain.Authorization;
using Evolith.Ums.Domain.Kernel;

/// <summary>
/// Elimina lógicamente un sistema del catálogo (G-246).
///
/// Orden deliberado: primero se comprueba el ámbito de gestión —dar de baja un sistema es al menos
/// tan sensible como darlo de alta, así que exige el mismo permiso que <c>CreateSystemSuite</c>—,
/// luego se cuentan las referencias vivas y solo entonces se pregunta al dominio. El dominio dicta
/// la regla; esta capa se limita a traducir su negativa al 409 estructurado que ya usan el resto de
/// guardas de dependencias.
///
/// La escritura sale por <c>UpdateAsync</c>, igual que un cambio de estado cualquiera, porque eso es
/// exactamente lo que es: la fila se queda, con su estado en <c>Deleted</c>. No hay —ni debe haber—
/// una llamada de borrado en el repositorio.
/// </summary>
public sealed class DeleteSystemSuiteCommandHandler : ICommandHandler<DeleteSystemSuiteCommand>
{
    private readonly ISystemSuiteRepository _systemSuiteRepository;
    private readonly IUserContext _userContext;
    private readonly ITenantScopePolicy _tenantScopePolicy;

    public DeleteSystemSuiteCommandHandler(
        ISystemSuiteRepository systemSuiteRepository,
        IUserContext userContext,
        ITenantScopePolicy tenantScopePolicy)
    {
        _systemSuiteRepository = systemSuiteRepository;
        _userContext = userContext;
        _tenantScopePolicy = tenantScopePolicy;
    }

    [AuthorizationAspect(ResourceCode = "manage_suites", ActionCode = "manage")]
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result> Handle(DeleteSystemSuiteCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_userContext.UserId))
        {
            return Result.Failure("Authenticated user is required to delete a system suite.");
        }

        // GetByIdAsync ya no ve los sistemas eliminados (filtro global): repetir el DELETE sobre uno
        // ya eliminado devuelve 404, que es lo coherente con que haya desaparecido de las lecturas.
        var systemSuite = await _systemSuiteRepository.GetByIdAsync(request.SystemSuiteId, cancellationToken);
        if (systemSuite is null)
        {
            return Result.Failure(DomainErrors.Common.NotFound);
        }

        var scopeResult = await _tenantScopePolicy.EnsureManagementOwnerScopeAsync(
            systemSuite.TenantId.GetValue(), cancellationToken);
        if (scopeResult.IsFailure)
        {
            return scopeResult;
        }

        var dependents = await _systemSuiteRepository.GetDependentsAsync(request.SystemSuiteId, cancellationToken);

        var result = systemSuite.Delete(dependents, ActorId.Create(_userContext.UserId));
        if (result.IsFailure)
        {
            // El detalle de QUÉ bloquea es formato de respuesta, no regla de negocio: el dominio
            // devuelve el código con nombre y aquí se le adjunta el desglose que verá el llamador.
            return result.Error == DomainErrors.Authorization.SystemSuiteHasDependents
                ? Result.Failure(BlockedOperationError.Encode(result.Error, dependents.ToBlockingDependencies()))
                : result;
        }

        await _systemSuiteRepository.UpdateAsync(systemSuite, cancellationToken);
        await _systemSuiteRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        return Result.Success();
    }
}
