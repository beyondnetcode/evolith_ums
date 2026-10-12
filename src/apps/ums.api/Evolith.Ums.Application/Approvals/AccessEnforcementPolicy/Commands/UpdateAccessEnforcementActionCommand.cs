namespace Evolith.Ums.Application.Approvals.AccessEnforcementPolicy.Commands;
public sealed record UpdateAccessEnforcementActionCommand(Guid PolicyId, string NewAction) : ICommand;
