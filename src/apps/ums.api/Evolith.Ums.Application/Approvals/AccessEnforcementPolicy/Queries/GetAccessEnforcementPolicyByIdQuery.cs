using Evolith.Ums.Application.Approvals.AccessEnforcementPolicy.DTOs;

namespace Evolith.Ums.Application.Approvals.AccessEnforcementPolicy.Queries;

public sealed record GetAccessEnforcementPolicyByIdQuery(Guid AccessEnforcementPolicyId) : IQuery<AccessEnforcementPolicyDto>;
