namespace Evolith.Ums.Infrastructure.Approvals.ApprovalRequest;

using ApprovalRequestAggregate = Evolith.Ums.Domain.Approvals.ApprovalRequest.ApprovalRequest;

internal interface IApprovalRequestCreationStrategy
{
    Result<ApprovalRequestAggregate> Create(ApprovalRequestCreationContext context);
}
