namespace Evolith.Ums.Application.Authorization.AssignmentRule.Commands;

public sealed record ReactivateAssignmentRuleCommand(Guid RuleId) : ICommand;
