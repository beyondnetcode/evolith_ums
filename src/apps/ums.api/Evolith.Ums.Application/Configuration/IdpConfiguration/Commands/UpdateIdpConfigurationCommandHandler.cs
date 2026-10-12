
namespace Evolith.Ums.Application.Configuration.IdpConfiguration.Commands;

using Evolith.Ums.Domain.Configuration;

public sealed class UpdateIdpConfigurationCommandHandler : ICommandHandler<UpdateIdpConfigurationCommand>
{
    private readonly IIdpConfigurationRepository _repository;
    private readonly IUserContext _userContext;

    public UpdateIdpConfigurationCommandHandler(IIdpConfigurationRepository repository, IUserContext userContext)
    {
        _repository = repository;
        _userContext = userContext;
    }

    [AuthorizationAspect(ResourceCode = "manage_config", ActionCode = "manage")]
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result> Handle(UpdateIdpConfigurationCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_userContext.UserId))
        {
            return Result.Failure("Authenticated user is required.");
        }

        var idpConfiguration = await _repository.GetByIdAsync(request.IdpConfigurationId, cancellationToken);
        if (idpConfiguration is null)
        {
            return Result.Failure("IdP configuration was not found.");
        }

        var result = idpConfiguration.Update(
            request.ConfigPayload.Trim(),
            request.SecretRef.Trim(),
            request.DomainHints.Where(hint => !string.IsNullOrWhiteSpace(hint)).Select(hint => hint.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            ActorId.Create(_userContext.UserId));

        if (result.IsFailure)
        {
            return result;
        }

        await _repository.UpdateAsync(idpConfiguration, cancellationToken);
        await _repository.UnitOfWork.SaveEntitiesAsync(cancellationToken);
        return Result.Success();
    }
}
