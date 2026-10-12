using Evolith.Ums.Application.Approvals.AccessEnforcementPolicy.DTOs;

namespace Evolith.Ums.Application.Approvals.AccessEnforcementPolicy.Commands;

public sealed record DeactivateAccessEnforcementPolicyCommand(Guid AccessEnforcementPolicyId) : ICommand;
