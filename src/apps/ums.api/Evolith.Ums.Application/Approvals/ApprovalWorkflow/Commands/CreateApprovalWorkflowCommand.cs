using Evolith.Ums.Application.Approvals.ApprovalWorkflow.DTOs;

namespace Evolith.Ums.Application.Approvals.ApprovalWorkflow.Commands;

public sealed record CreateApprovalWorkflowCommand(
    Guid TenantId,
    Guid? SystemSuiteId,
    string Code,
    string Name,
    string Description,
    string TargetUserCategory,
    bool RequiresApproval) : ICommand<CreateApprovalWorkflowResponse>;
