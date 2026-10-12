namespace Evolith.Ums.Application.Authorization.Role.Commands;

public sealed record SetRoleStatusCommand(Guid RoleId, bool IsActive) : ICommand;
