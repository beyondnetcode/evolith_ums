using Evolith.Ums.Application.Configuration.IdpConfiguration.DTOs;

namespace Evolith.Ums.Application.Configuration.IdpConfiguration.Queries;

using Evolith.Ums.Domain.Configuration;

public sealed class GetIdpConfigurationByIdQueryHandler : IQueryHandler<GetIdpConfigurationByIdQuery, IdpConfigurationDto>
{
    private readonly IIdpConfigurationRepository _repository;

    public GetIdpConfigurationByIdQueryHandler(IIdpConfigurationRepository repository) => _repository = repository;

    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]

    [AuthorizationAspect(ResourceCode = "view_config", ActionCode = "view")]
    public async Task<Result<IdpConfigurationDto>> Handle(GetIdpConfigurationByIdQuery request, CancellationToken cancellationToken)
    {
        var idpConfiguration = await _repository.GetByIdAsync(request.IdpConfigurationId, cancellationToken);
        if (idpConfiguration is null)
        {
            return Result<IdpConfigurationDto>.Failure("IdP configuration was not found.");
        }

        return Result<IdpConfigurationDto>.Success(new IdpConfigurationDto(
            idpConfiguration.Props.Id.GetValue(),
            idpConfiguration.Props.TenantId.GetValue(),
            idpConfiguration.Props.SystemSuiteId.GetValue(),
            idpConfiguration.Props.ProviderType.Name,
            idpConfiguration.Props.DomainHints,
            idpConfiguration.Props.ConfigPayload,
            idpConfiguration.Props.SecretRef,
            idpConfiguration.Props.Status.Name,
            idpConfiguration.Props.ResolutionPriority,
            idpConfiguration.Props.FallbackToId,
            idpConfiguration.Props.Version));
    }
}
