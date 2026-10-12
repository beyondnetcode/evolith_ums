
namespace Evolith.Ums.Application.Configuration.AppConfiguration.Commands;

using Evolith.Ums.Application.Configuration.Services;
using Evolith.Ums.Domain.Configuration;

public sealed class DeleteAppConfigurationCommandHandler : ICommandHandler<DeleteAppConfigurationCommand>
{
    private readonly IAppConfigurationRepository _repository;
    private readonly IUserContext _userContext;
    private readonly IConfigurationProvider _configProvider;

    public DeleteAppConfigurationCommandHandler(
        IAppConfigurationRepository repository,
        IUserContext userContext,
        IConfigurationProvider configProvider)
    {
        _repository = repository;
        _userContext = userContext;
        _configProvider = configProvider;
    }

    [AuthorizationAspect(ResourceCode = "manage_params", ActionCode = "manage")]
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result> Handle(DeleteAppConfigurationCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_userContext.UserId))
        {
            return Result.Failure("Authenticated user is required.");
        }

        // Las lecturas del repositorio ya ocultan lo eliminado: si la configuración ya se borró,
        // esto devuelve null y el DELETE responde «no encontrada» (mismo 404 que un id inexistente).
        var appConfiguration = await _repository.GetByIdAsync(request.AppConfigurationId, cancellationToken);
        if (appConfiguration is null)
        {
            return Result.Failure("App configuration was not found.");
        }

        // Borrado LÓGICO: la fila NO se elimina, pasa al estado terminal Deleted. AppConfiguration es
        // un agregado hoja (sin valores hijos), así que no hay dependientes que exigir eliminados antes.
        var result = appConfiguration.Delete(ActorId.Create(_userContext.UserId));
        if (result.IsFailure)
        {
            return result;
        }

        await _repository.UpdateAsync(appConfiguration, cancellationToken);
        await _repository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        // La configuración deja de resolver aunque su fila siga ahí: la caché tiene que enterarse igual.
        var tenantId = appConfiguration.Props.TenantId?.GetValue();
        if (tenantId.HasValue)
            await _configProvider.ReloadTenantAsync(tenantId.Value, cancellationToken);
        else
            await _configProvider.ReloadAsync(cancellationToken);

        return Result.Success();
    }
}
