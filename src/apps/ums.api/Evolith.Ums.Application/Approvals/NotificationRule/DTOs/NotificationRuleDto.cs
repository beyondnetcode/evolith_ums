namespace Evolith.Ums.Application.Approvals.NotificationRule.DTOs;

public sealed record NotificationRuleDto(
    Guid NotificationRuleId,
    Guid TenantId,
    string Channel,
    string Recipient,
    bool IsActive);
