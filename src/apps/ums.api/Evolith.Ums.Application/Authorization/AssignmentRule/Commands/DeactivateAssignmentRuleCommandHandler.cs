namespace Evolith.Ums.Application.Authorization.AssignmentRule.Commands;

using Evolith.Ums.Domain.Authorization;

public sealed class DeactivateAssignmentRuleCommandHandler : ICommandHandler<DeactivateAssignmentRuleCommand>
{
    private readonly ITemplateAssignmentRuleRepository _ruleRepository;
    private readonly IUserContext _userContext;

    public DeactivateAssignmentRuleCommandHandler(
        ITemplateAssignmentRuleRepository ruleRepository,
        IUserContext userContext)
    {
        _ruleRepository = ruleRepository;
        _userContext = userContext;
    }

    [AuthorizationAspect(ResourceCode = "manage_roles", ActionCode = "manage")]
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result> Handle(DeactivateAssignmentRuleCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_userContext.UserId))
        {
            return Result.Failure("Authenticated user is required to deactivate an assignment rule.");
        }

        var rule = await _ruleRepository.GetByIdAsync(request.RuleId, cancellationToken);
        if (rule is null)
        {
            return Result.Failure("Assignment rule was not found.");
        }

        var result = rule.Deactivate(ActorId.Create(_userContext.UserId));
        if (result.IsFailure)
        {
            return result;
        }

        await _ruleRepository.UpdateAsync(rule, cancellationToken);
        await _ruleRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        return Result.Success();
    }
}
