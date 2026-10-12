namespace Evolith.Ums.Application.Authorization.Profile.Commands;

public sealed record OverrideProfilePermissionCommand(Guid ProfileId, Guid PermissionId, string Effect) : ICommand;
