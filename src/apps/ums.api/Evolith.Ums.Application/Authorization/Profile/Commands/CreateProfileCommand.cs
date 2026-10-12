using Evolith.Ums.Application.Authorization.Profile.DTOs;

namespace Evolith.Ums.Application.Authorization.Profile.Commands;

public sealed record CreateProfileCommand(
    Guid TenantId,
    Guid UserId,
    Guid RoleId,
    Guid? BranchId) : ICommand<CreateProfileResponse>;
