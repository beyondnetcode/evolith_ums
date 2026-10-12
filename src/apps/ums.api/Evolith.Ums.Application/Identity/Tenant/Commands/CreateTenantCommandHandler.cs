using Evolith.Ums.Application.Identity.Tenant.DTOs;

namespace Evolith.Ums.Application.Identity.Tenant.Commands;

using Evolith.Ums.Domain.Identity.Tenant;

public sealed class CreateTenantCommandHandler : ICommandHandler<CreateTenantCommand, CreateTenantResponse>
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IUserContext _userContext;

    public CreateTenantCommandHandler(
        ITenantRepository tenantRepository,
        IUserContext userContext)
    {
        _tenantRepository = tenantRepository;
        _userContext = userContext;
    }

    [AuthorizationAspect(ResourceCode = "manage_tenants", ActionCode = "manage")]
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result<CreateTenantResponse>> Handle(
        CreateTenantCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_userContext.UserId))
        {
            return Result<CreateTenantResponse>.Failure("Authenticated user is required to create a tenant.");
        }

        var code = Code.Create(request.Code);
        var existingTenant = await _tenantRepository.GetByCodeAsync(code.GetValue(), cancellationToken);
        if (existingTenant is not null)
        {
            return Result<CreateTenantResponse>.Failure("Tenant code already exists.");
        }

        // G-045/G-037: la propiedad de gestión (management owner) es única en todo el sistema.
        // Existe un índice parcial único en BD (IX_Tenants_SingleManagementOwner). Sin este
        // chequeo explícito, crear un segundo tenant con IsManagementOwner=true provoca una
        // violación de índice en SaveEntitiesAsync que termina en 500. Verificamos la unicidad
        // ANTES de persistir y devolvemos un Result.Failure que el mapeo de presentación
        // resuelve a 409 (análogo a SetManagementOwnerCommandHandler).
        if (request.IsManagementOwner)
        {
            var allTenants = await _tenantRepository.GetAllAsync(null, cancellationToken);
            var existingOwner = allTenants.FirstOrDefault(candidate => candidate.IsManagementOwner);
            if (existingOwner is not null)
            {
                return Result<CreateTenantResponse>.Failure(DomainErrors.Tenant.ManagementOwnerAlreadyExists);
            }
        }

        // G-045: red de defensa en profundidad. El validador ya rechaza un OrganizationType
        // desconocido, pero no debemos depender de que el pipeline de validación esté cableado:
        // un enum inválido debe resolver Result.Failure (→ 400) y nunca propagar null hasta una
        // NullReferenceException que colapsaría a 500 (sin excepciones para control de flujo).
        var type = DomainEnumerationParser.FromName<OrganizationType>(request.Type);
        if (type is null)
        {
            return Result<CreateTenantResponse>.Failure(DomainErrors.Common.Invalid);
        }

        var idpStrategy = DomainEnumerationParser.FromName<IdpStrategy>(request.IdpStrategy) ?? IdpStrategy.InternalBcrypt;
        var companyReference = string.IsNullOrWhiteSpace(request.CompanyReference)
            ? null
            : CompanyReference.Create(request.CompanyReference);
        // G-046: a tenant created through this endpoint is a top-level tenant with no parent.
        // Never fabricate a random parent id — that produced a dangling, non-existent reference.
        TenantId? parentTenantId = null;

        var tenantResult = Tenant.Create(
            code,
            Name.Create(request.Name),
            type,
            ActorId.Create(_userContext.UserId),
            idpStrategy,
            companyReference,
            parentTenantId,
            tenantId: null,
            isManagementOwner: request.IsManagementOwner);

        if (tenantResult.IsFailure)
        {
            return Result<CreateTenantResponse>.Failure(tenantResult.Error);
        }

        await _tenantRepository.AddAsync(tenantResult.Value, cancellationToken);
        await _tenantRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        return Result<CreateTenantResponse>.Success(
            new CreateTenantResponse(tenantResult.Value.Props.Id.GetValue()));
    }
}
