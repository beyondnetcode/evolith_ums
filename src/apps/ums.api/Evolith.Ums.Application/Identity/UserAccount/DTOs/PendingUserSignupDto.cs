namespace Evolith.Ums.Application.Identity.UserAccount.DTOs;

public sealed record PendingUserSignupDto(
    Guid UserAccountId,
    Guid TenantId,
    string Email,
    string? DisplayName,
    string Category,
    DateTime RequestedAt);
