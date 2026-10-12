namespace Evolith.Ums.Application.Identity.UserAccount.DTOs;

public sealed record UserAccountDto(
    Guid UserAccountId,
    Guid TenantId,
    Guid? BranchId,
    string Email,
    string? DisplayName,
    string Category,
    string Status,
    string? IdentityReference,
    string? IdentityReferenceType,
    bool HasActivePassword,
    DateTime? PasswordUpdatedAtUtc,
    DateTimeOffset? ExpiresAt = null);
