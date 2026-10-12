using Evolith.Ums.Application.Approvals.ApprovalWorkflow.DTOs;
using Evolith.Ums.Domain.Approvals;
using Evolith.Ums.Domain.Approvals.ApprovalWorkflow;

namespace Evolith.Ums.Application.Approvals.ApprovalWorkflow.Queries;

public sealed class GetApprovalWorkflowByIdQueryHandler : IQueryHandler<GetApprovalWorkflowByIdQuery, ApprovalWorkflowDto>
{
    private readonly IApprovalWorkflowRepository _repository;

    public GetApprovalWorkflowByIdQueryHandler(IApprovalWorkflowRepository repository) => _repository = repository;

    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]

    [AuthorizationAspect(ResourceCode = "view_approval_rules", ActionCode = "view")]
    public async Task<Result<ApprovalWorkflowDto>> Handle(GetApprovalWorkflowByIdQuery request, CancellationToken cancellationToken)
    {
        var workflow = await _repository.GetByIdAsync(request.ApprovalWorkflowId, cancellationToken);
        if (workflow is null) return Result<ApprovalWorkflowDto>.Failure("Approval workflow not found.");

        return Result<ApprovalWorkflowDto>.Success(new ApprovalWorkflowDto(
            workflow.Props.Id.GetValue(), workflow.Props.TenantId.GetValue(), workflow.Props.SystemSuiteId?.GetValue(),
            workflow.Props.Code.GetValue(), workflow.Props.Name.GetValue(), workflow.Props.Description.GetValue(),
            workflow.Props.TargetUserCategory.ToString(), workflow.Props.RequiresApproval,
            // WF2/WF3 (G-118): checklist observable con id (para el DELETE por id).
            workflow.RequiredDocuments.Select(d => new RequiredDocumentDto(
                d.GetId().GetValue(), d.DocumentTypeId.GetValue(), d.IsMandatory)).ToList()));
    }
}
