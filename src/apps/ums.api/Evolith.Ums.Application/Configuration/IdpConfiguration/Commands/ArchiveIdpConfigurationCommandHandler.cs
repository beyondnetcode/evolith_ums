namespace Evolith.Ums.Application.Configuration.IdpConfiguration.Commands;

using Evolith.Ums.Domain.Configuration;

public sealed class ArchiveIdpConfigurationCommandHandler(
    IIdpConfigurationRepository repository,
    IUserContext userContext)
    : ICommandHandler<ArchiveIdpConfigurationCommand>
{
    [AuthorizationAspect(ResourceCode = "manage_config", ActionCode = "manage")]
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result> Handle(ArchiveIdpConfigurationCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userContext.UserId))
            return Result.Failure("Authenticated user is required.");

        var idpConfig = await repository.GetByIdAsync(request.IdpConfigurationId, cancellationToken);
        if (idpConfig is null)
            return Result.Failure("IdP configuration was not found.");

        var result = idpConfig.Archive(ActorId.Create(userContext.UserId));
        if (result.IsFailure) return result;

        await repository.UpdateAsync(idpConfig, cancellationToken);
        await repository.UnitOfWork.SaveEntitiesAsync(cancellationToken);
        return Result.Success();
    }
}
