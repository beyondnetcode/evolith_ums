using Evolith.Ums.Application.Configuration.IdpConfiguration.DTOs;
using Evolith.Ums.Application.Configuration.IdpConfiguration.Services;

namespace Evolith.Ums.Application.Configuration.IdpConfiguration.Queries;

public sealed class ResolveIdpConfigurationQueryHandler : IQueryHandler<ResolveIdpConfigurationQuery, ResolvedIdpConfigurationDto>
{
    private readonly IIdpConfigurationResolver _resolver;

    public ResolveIdpConfigurationQueryHandler(IIdpConfigurationResolver resolver)
    {
        _resolver = resolver;
    }

    [AuthorizationAspect(ResourceCode = "view_config", ActionCode = "view")]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public Task<Result<ResolvedIdpConfigurationDto>> Handle(ResolveIdpConfigurationQuery request, CancellationToken cancellationToken)
        => _resolver.ResolveAsync(request.TenantId, request.SystemSuiteId, request.EmailDomain, request.ProviderType, cancellationToken);
}
