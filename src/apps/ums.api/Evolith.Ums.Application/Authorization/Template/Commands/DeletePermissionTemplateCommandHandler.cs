namespace Evolith.Ums.Application.Authorization.Template.Commands;

using Evolith.Ums.Domain.Authorization;
using Evolith.Ums.Domain.Authorization.Template;
using Evolith.Ums.Domain.Kernel;

/// <summary>
/// Borrado LÓGICO de una plantilla de permisos. El contrato HTTP no cambia —DELETE sigue devolviendo
/// 204, 404 o 409 igual que antes—; lo que cambia es que la fila NO se elimina de la base.
///
/// Orden de ejecución (importa):
/// 1. Carga. El repositorio ya oculta lo eliminado, así que un segundo DELETE da 404.
/// 2. Guardia de cascada: perfiles VIVOS que referencian la plantilla → 409 con dependencias
///    estructuradas. Los perfiles ya desactivados no cuentan: su referencia también está
///    lógicamente eliminada, que es exactamente la regla que pide la política.
/// 3. <c>PermissionTemplate.Delete</c> valida el estado (solo Draft/Deprecated) y transiciona al
///    estado terminal Deleted.
/// 4. <c>UpdateAsync</c> persiste el nuevo StatusId y la auditoría (quién y cuándo lo eliminó): sin
///    esta llamada el rastro de la eliminación se perdería, que es justo lo que la política protege.
/// </summary>
public sealed class DeletePermissionTemplateCommandHandler : ICommandHandler<DeletePermissionTemplateCommand>
{
    private readonly IPermissionTemplateRepository _templateRepository;
    private readonly IProfileRepository _profileRepository;
    private readonly IUserContext _userContext;

    public DeletePermissionTemplateCommandHandler(
        IPermissionTemplateRepository templateRepository,
        IProfileRepository profileRepository,
        IUserContext userContext)
    {
        _templateRepository = templateRepository;
        _profileRepository = profileRepository;
        _userContext = userContext;
    }

    [AuthorizationAspect(ResourceCode = "manage_templates", ActionCode = "manage")]
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result> Handle(DeletePermissionTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await _templateRepository.GetByIdAsync(request.TemplateId, cancellationToken);
        if (template is null)
        {
            return Result.Failure("Template was not found.");
        }

        if (string.IsNullOrWhiteSpace(_userContext.UserId))
        {
            return Result.Failure("Authenticated user is required.");
        }

        // ── Guardia de cascada: perfiles vivos que usan la plantilla ──────────
        // Análoga a un ON DELETE RESTRICT: no se elimina lógicamente algo con referencias reales
        // que no hayan sido eliminadas lógicamente primero. Mismo conteo y mismo código de error
        // que la depreciación (DeprecatePermissionTemplateCommandHandler), que ya lo aplica.
        var activeProfileCount = await _profileRepository.CountActiveByTemplateAsync(
            request.TemplateId, cancellationToken);

        if (activeProfileCount > 0)
        {
            var deps = new List<BlockingDependency>
            {
                new("Profile", "Active", activeProfileCount),
            };
            return Result.Failure(BlockedOperationError.Encode(
                DomainErrors.Authorization.TemplateHasActiveProfiles, deps));
        }

        var deleteResult = template.Delete(ActorId.Create(_userContext.UserId), activeProfileCount);
        if (deleteResult.IsFailure)
        {
            return deleteResult;
        }

        await _templateRepository.UpdateAsync(template, cancellationToken);
        await _templateRepository.UnitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
