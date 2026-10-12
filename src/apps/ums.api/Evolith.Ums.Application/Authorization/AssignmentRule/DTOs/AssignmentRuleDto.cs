namespace Evolith.Ums.Application.Authorization.AssignmentRule.DTOs;

public sealed record AssignmentRuleDto(
    Guid RuleId,
    Guid TenantId,
    Guid TemplateId,
    Guid RoleId,
    int Priority,
    string Status);
