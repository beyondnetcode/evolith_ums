namespace Evolith.Ums.Application.Approvals.ApprovalWorkflow.Commands;
using Evolith.Ums.Domain.Approvals;
public sealed class RemoveRequiredDocumentCommandHandler : ICommandHandler<RemoveRequiredDocumentCommand>
{
    private readonly IApprovalWorkflowRepository _repository;
    private readonly IUserContext _userContext;
    public RemoveRequiredDocumentCommandHandler(IApprovalWorkflowRepository repository, IUserContext userContext) { _repository = repository; _userContext = userContext; }
    [AuthorizationAspect(ResourceCode = "manage_approval_rules", ActionCode = "manage")]
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result> Handle(RemoveRequiredDocumentCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_userContext.UserId)) return Result.Failure("Authenticated user is required.");
        var workflow = await _repository.GetByIdAsync(request.WorkflowId, cancellationToken);
        if (workflow is null) return Result.Failure("Approval workflow not found.");
        var result = workflow.RemoveRequiredDocument(IdValueObject.Load(request.DocumentId), ActorId.Create(_userContext.UserId));
        if (result.IsFailure) return result;
        await _repository.UpdateAsync(workflow, cancellationToken);
        await _repository.UnitOfWork.SaveEntitiesAsync(cancellationToken);
        return Result.Success();
    }
}
