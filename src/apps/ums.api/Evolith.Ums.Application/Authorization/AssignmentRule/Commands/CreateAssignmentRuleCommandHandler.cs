namespace Evolith.Ums.Application.Authorization.AssignmentRule.Commands;

using Evolith.Ums.Application.Authorization.AssignmentRule.DTOs;
using Evolith.Ums.Domain.Authorization;
using Evolith.Ums.Domain.Authorization.AssignmentRule;
using Evolith.Ums.Domain.Authorization.Template;

public sealed class CreateAssignmentRuleCommandHandler : ICommandHandler<CreateAssignmentRuleCommand, CreateAssignmentRuleResponse>
{
    private readonly ITemplateAssignmentRuleRepository _ruleRepository;
    private readonly IPermissionTemplateRepository _templateRepository;
    private readonly IUserContext _userContext;
    private readonly ITenantScopePolicy _tenantScopePolicy;

    public CreateAssignmentRuleCommandHandler(
        ITemplateAssignmentRuleRepository ruleRepository,
        IPermissionTemplateRepository templateRepository,
        IUserContext userContext,
        ITenantScopePolicy tenantScopePolicy)
    {
        _ruleRepository = ruleRepository;
        _templateRepository = templateRepository;
        _userContext = userContext;
        _tenantScopePolicy = tenantScopePolicy;
    }

    [AuthorizationAspect(ResourceCode = "manage_roles", ActionCode = "manage")]
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result<CreateAssignmentRuleResponse>> Handle(
        CreateAssignmentRuleCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_userContext.UserId))
        {
            return Result<CreateAssignmentRuleResponse>.Failure("Authenticated user is required to create an assignment rule.");
        }

        var scopeResult = await _tenantScopePolicy.EnsureManagementOwnerScopeAsync(request.TenantId, cancellationToken);
        if (scopeResult.IsFailure)
        {
            return Result<CreateAssignmentRuleResponse>.Failure(scopeResult.Error);
        }

        var template = await _templateRepository.GetByIdAsync(request.TemplateId, cancellationToken);
        if (template is null)
        {
            return Result<CreateAssignmentRuleResponse>.Failure("Permission template was not found.");
        }

        if (template.Status != TemplateStatus.Published)
        {
            return Result<CreateAssignmentRuleResponse>.Failure(DomainErrors.Authorization.TemplateNotPublishedForProfile);
        }

        var priorityExists = await _ruleRepository.ExistsActiveWithPriorityAsync(request.TenantId, request.Priority, cancellationToken);
        if (priorityExists)
        {
            return Result<CreateAssignmentRuleResponse>.Failure(DomainErrors.Authorization.AssignmentRuleDuplicatePriority);
        }

        var ruleResult = TemplateAssignmentRule.Create(
            TenantId.Load(request.TenantId),
            TemplateId.Load(request.TemplateId),
            RoleId.Load(request.RoleId),
            request.Priority,
            ActorId.Create(_userContext.UserId));

        if (ruleResult.IsFailure)
        {
            return Result<CreateAssignmentRuleResponse>.Failure(ruleResult.Error);
        }

        await _ruleRepository.AddAsync(ruleResult.Value, cancellationToken);
        await _ruleRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        return Result<CreateAssignmentRuleResponse>.Success(
            new CreateAssignmentRuleResponse(ruleResult.Value.Props.Id.GetValue()));
    }
}
