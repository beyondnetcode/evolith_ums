namespace Evolith.Ums.Application.Authorization.Profile.Commands;

public sealed record SetProfilePermissionStatusCommand(Guid ProfileId, Guid PermissionId, bool IsActive) : ICommand;
