namespace Evolith.Ums.Application.Authorization.AssignmentRule.Commands;

public sealed record DeactivateAssignmentRuleCommand(Guid RuleId) : ICommand;
