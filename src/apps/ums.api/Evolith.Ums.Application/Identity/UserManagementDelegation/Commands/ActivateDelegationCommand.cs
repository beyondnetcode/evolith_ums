namespace Evolith.Ums.Application.Identity.UserManagementDelegation.Commands;

public sealed record ActivateDelegationCommand(Guid DelegationId) : ICommand;
