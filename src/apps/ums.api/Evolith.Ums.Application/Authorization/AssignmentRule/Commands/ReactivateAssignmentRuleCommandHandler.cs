namespace Evolith.Ums.Application.Authorization.AssignmentRule.Commands;

using Evolith.Ums.Domain.Authorization;

public sealed class ReactivateAssignmentRuleCommandHandler : ICommandHandler<ReactivateAssignmentRuleCommand>
{
    private readonly ITemplateAssignmentRuleRepository _ruleRepository;
    private readonly IUserContext _userContext;

    public ReactivateAssignmentRuleCommandHandler(
        ITemplateAssignmentRuleRepository ruleRepository,
        IUserContext userContext)
    {
        _ruleRepository = ruleRepository;
        _userContext = userContext;
    }

    [AuthorizationAspect(ResourceCode = "manage_roles", ActionCode = "manage")]
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result> Handle(ReactivateAssignmentRuleCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_userContext.UserId))
        {
            return Result.Failure("Authenticated user is required to reactivate an assignment rule.");
        }

        var rule = await _ruleRepository.GetByIdAsync(request.RuleId, cancellationToken);
        if (rule is null)
        {
            return Result.Failure("Assignment rule was not found.");
        }

        var result = rule.Reactivate(ActorId.Create(_userContext.UserId));
        if (result.IsFailure)
        {
            return result;
        }

        await _ruleRepository.UpdateAsync(rule, cancellationToken);
        await _ruleRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        return Result.Success();
    }
}
