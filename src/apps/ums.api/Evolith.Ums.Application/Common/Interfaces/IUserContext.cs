namespace Evolith.Ums.Application.Common.Interfaces;

/// <summary>
/// Domain-agnostic port to access the current user context.
/// Implemented by the Infrastructure layer via Scoped lifecycle.
/// </summary>
public interface IUserContext
{
    string? UserId { get; }
    string? UserName { get; }
    string? TenantId { get; }
    bool IsAuthenticated { get; }
    bool HasPermission(string permission);
}
