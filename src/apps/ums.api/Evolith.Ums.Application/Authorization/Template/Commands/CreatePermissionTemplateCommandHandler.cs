using Evolith.Ums.Application.Authorization.Template.DTOs;

namespace Evolith.Ums.Application.Authorization.Template.Commands;

using Evolith.Ums.Domain.Authorization;
using Evolith.Ums.Domain.Authorization.Template;

public sealed class CreatePermissionTemplateCommandHandler : ICommandHandler<CreatePermissionTemplateCommand, CreatePermissionTemplateResponse>
{
    private readonly IPermissionTemplateRepository _templateRepository;
    private readonly IUserContext _userContext;
    private readonly ITenantScopePolicy _tenantScopePolicy;

    public CreatePermissionTemplateCommandHandler(
        IPermissionTemplateRepository templateRepository,
        IUserContext userContext,
        ITenantScopePolicy tenantScopePolicy)
    {
        _templateRepository = templateRepository;
        _userContext = userContext;
        _tenantScopePolicy = tenantScopePolicy;
    }

    [AuthorizationAspect(ResourceCode = "manage_templates", ActionCode = "manage")]
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result<CreatePermissionTemplateResponse>> Handle(
        CreatePermissionTemplateCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_userContext.UserId))
        {
            return Result<CreatePermissionTemplateResponse>.Failure("Authenticated user is required to create a permission template.");
        }

        var scopeResult = await _tenantScopePolicy.EnsureManagementOwnerScopeAsync(request.TenantId, cancellationToken);
        if (scopeResult.IsFailure)
        {
            return Result<CreatePermissionTemplateResponse>.Failure(scopeResult.Error);
        }

        // G-140: resuelve la versión SIGUIENTE a partir de las plantillas existentes para la terna
        // (tenant, rol, suite). Antes se asignaba siempre 0.1.0 y el alta sobre un rol ya plantillado
        // colisionaba con el índice único → DbUpdateException no controlada → 500 opaco.
        var existing = await _templateRepository.GetByTenantRoleSuiteAsync(
            request.TenantId, request.RoleId, request.SystemSuiteId, cancellationToken);

        var templateResult = PermissionTemplate.CreateNextVersion(
            TenantId.Load(request.TenantId),
            RoleId.Load(request.RoleId),
            SystemSuiteId.Load(request.SystemSuiteId),
            ActorId.Create(_userContext.UserId),
            (existing ?? []).Select(t => t.Version));

        if (templateResult.IsFailure)
        {
            return Result<CreatePermissionTemplateResponse>.Failure(templateResult.Error);
        }

        await _templateRepository.AddAsync(templateResult.Value, cancellationToken);
        await _templateRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        return Result<CreatePermissionTemplateResponse>.Success(
            new CreatePermissionTemplateResponse(templateResult.Value.Props.Id.GetValue()));
    }
}
