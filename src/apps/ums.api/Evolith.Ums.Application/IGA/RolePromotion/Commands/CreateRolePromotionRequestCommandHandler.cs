namespace Evolith.Ums.Application.IGA.RolePromotion.Commands;

using Evolith.Ums.Application.IGA.Common;
using Evolith.Ums.Application.IGA.DTOs;
using Evolith.Ums.Domain.IGA;
using Evolith.Ums.Domain.IGA.RolePromotionRequest;

public sealed class CreateRolePromotionRequestCommandHandler
    : ICommandHandler<CreateRolePromotionRequestCommand, CreateRolePromotionRequestResponse>
{
    private readonly IRolePromotionRequestRepository _repository;
    private readonly ITenantScopePolicy _tenantScopePolicy;
    private readonly IUserContext _userContext;

    public CreateRolePromotionRequestCommandHandler(
        IRolePromotionRequestRepository repository,
        ITenantScopePolicy tenantScopePolicy,
        IUserContext userContext)
    {
        _repository = repository;
        _tenantScopePolicy = tenantScopePolicy;
        _userContext = userContext;
    }

    [AuthorizationAspect(ResourceCode = "manage_role_promotions", ActionCode = "manage")]
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result<CreateRolePromotionRequestResponse>> Handle(
        CreateRolePromotionRequestCommand request,
        CancellationToken cancellationToken)
    {
        var actor = IgaHandlerGuards.RequireAuthenticatedUser(_userContext);
        if (actor.IsFailure)
        {
            return Result<CreateRolePromotionRequestResponse>.Failure(actor.Error);
        }

        var scope = IgaHandlerGuards.EnsureTenantInScope(_tenantScopePolicy, request.TenantId);
        if (scope.IsFailure)
        {
            return Result<CreateRolePromotionRequestResponse>.Failure(scope.Error);
        }

        // SoD (INV-RPR3): el solicitante no puede promover(se) a sí mismo.
        if (actor.Value == request.TargetUserId)
        {
            return Result<CreateRolePromotionRequestResponse>.Failure(
                "El solicitante no puede ser el usuario objetivo de la promoción (segregación de funciones).");
        }

        var createResult = RolePromotionRequest.Create(
            TenantId.Load(request.TenantId),
            UserId.Load(request.TargetUserId),
            UserId.Load(actor.Value),
            RoleId.Load(request.CurrentRoleId),
            RoleId.Load(request.TargetRoleId),
            ActorId.Create(_userContext.UserId));

        if (createResult.IsFailure)
        {
            return Result<CreateRolePromotionRequestResponse>.Failure(createResult.Error);
        }

        await _repository.AddAsync(createResult.Value, cancellationToken);
        await _repository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        return Result<CreateRolePromotionRequestResponse>.Success(
            new CreateRolePromotionRequestResponse(createResult.Value.GetId().GetValue()));
    }
}
