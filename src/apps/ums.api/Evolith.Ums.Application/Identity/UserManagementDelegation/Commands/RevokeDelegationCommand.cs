namespace Evolith.Ums.Application.Identity.UserManagementDelegation.Commands;

public sealed record RevokeDelegationCommand(Guid DelegationId, string Reason) : ICommand;
