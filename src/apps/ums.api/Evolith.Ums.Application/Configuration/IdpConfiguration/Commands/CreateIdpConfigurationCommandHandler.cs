using Evolith.Ums.Application.Configuration.IdpConfiguration.DTOs;

namespace Evolith.Ums.Application.Configuration.IdpConfiguration.Commands;

using Evolith.Ums.Application.Common.Aop;
using Evolith.Ums.Domain.Configuration;
using Evolith.Ums.Domain.Enums;

public sealed class CreateIdpConfigurationCommandHandler : ICommandHandler<CreateIdpConfigurationCommand, CreateIdpConfigurationResponse>
{
    private readonly IIdpConfigurationRepository _repository;
    private readonly IUserContext _userContext;

    public CreateIdpConfigurationCommandHandler(IIdpConfigurationRepository repository, IUserContext userContext)
    {
        _repository = repository;
        _userContext = userContext;
    }

    // EN EL MÉTODO, NO EN LA CLASE: `PointCut.CanApply` solo mira los atributos del MÉTODO
    // objetivo, así que con el atributo en la clase este aspecto no se ejecutaba nunca y crear una
    // configuración de proveedor de identidad no tenía control de permisos. Este endpoint, además,
    // ni siquiera lleva `RequireAuthorization` en su declaración.
    //
    // `manage_config` y no `idpconfiguration`, que era el código anterior y no existe en ninguna
    // parte: el catálogo de la suite UMS declara dieciocho scopes con la forma `view_X.view` /
    // `manage_X.manage`, y la configuración de IdP cae bajo `manage_config`. El rol `ADMIN_UMS` lo
    // tiene concedido, de modo que el gate encendido no le quita nada a quien debe poder.
    [AuthorizationAspect(ResourceCode = "manage_config", ActionCode = "manage")]
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result<CreateIdpConfigurationResponse>> Handle(CreateIdpConfigurationCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_userContext.UserId))
        {
            return Result<CreateIdpConfigurationResponse>.Failure("Authenticated user is required.");
        }

        var providerType = DomainEnumerationParser.FromName<ProviderType>(request.ProviderType);
        if (providerType is null)
        {
            return Result<CreateIdpConfigurationResponse>.Failure("Invalid identity provider type.");
        }

        // Integridad referencial: un FallbackToId debe apuntar a una IdpConfiguration existente
        // del mismo TenantId/SystemSuiteId; de lo contrario se rechaza la referencia colgante.
        if (request.FallbackToId is Guid fallbackToId)
        {
            var fallback = await _repository.GetByIdAsync(fallbackToId, cancellationToken);
            if (fallback is null
                || fallback.Props.TenantId.GetValue() != request.TenantId
                || fallback.Props.SystemSuiteId.GetValue() != request.SystemSuiteId)
            {
                return Result<CreateIdpConfigurationResponse>.Failure(DomainErrors.Configuration.IdpFallbackNotFound);
            }
        }

        var result = Evolith.Ums.Domain.Configuration.IdpConfiguration.IdpConfiguration.Create(
            TenantId.Load(request.TenantId),
            SystemSuiteId.Load(request.SystemSuiteId),
            providerType,
            request.DomainHints.Where(hint => !string.IsNullOrWhiteSpace(hint)).Select(hint => hint.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            request.ConfigPayload.Trim(),
            request.SecretRef.Trim(),
            request.ResolutionPriority,
            request.FallbackToId,
            ActorId.Create(_userContext.UserId));

        if (result.IsFailure)
        {
            return Result<CreateIdpConfigurationResponse>.Failure(result.Error);
        }

        await _repository.AddAsync(result.Value, cancellationToken);
        await _repository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        return Result<CreateIdpConfigurationResponse>.Success(new CreateIdpConfigurationResponse(result.Value.Props.Id.GetValue()));
    }
}
