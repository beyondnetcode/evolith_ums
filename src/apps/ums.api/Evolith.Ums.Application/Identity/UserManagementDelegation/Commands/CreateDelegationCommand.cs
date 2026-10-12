using Evolith.Ums.Application.Identity.UserManagementDelegation.DTOs;

namespace Evolith.Ums.Application.Identity.UserManagementDelegation.Commands;

public sealed record CreateDelegationCommand(
    Guid TenantId,
    Guid DelegatingAdminId,
    Guid DelegatedAdminId,
    string ScopeType,
    Guid? ScopeId,
    IReadOnlyList<string> AllowedActions,
    DateTimeOffset ValidFrom,
    DateTimeOffset ValidUntil,
    int? MaxDurationDays,
    bool RequiresApproval) : ICommand<CreateDelegationResponse>;
