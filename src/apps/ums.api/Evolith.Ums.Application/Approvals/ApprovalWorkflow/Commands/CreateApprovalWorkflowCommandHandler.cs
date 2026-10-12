using Evolith.Ums.Application.Approvals.ApprovalWorkflow.DTOs;

namespace Evolith.Ums.Application.Approvals.ApprovalWorkflow.Commands;

using Evolith.Ums.Domain;
using Evolith.Ums.Domain.Approvals;
using Evolith.Ums.Domain.Approvals.ApprovalWorkflow;
using Evolith.Ums.Domain.Enums;

public sealed class CreateApprovalWorkflowCommandHandler : ICommandHandler<CreateApprovalWorkflowCommand, CreateApprovalWorkflowResponse>
{
    private readonly IApprovalWorkflowRepository _repository;
    private readonly IUserContext _userContext;

    public CreateApprovalWorkflowCommandHandler(IApprovalWorkflowRepository repository, IUserContext userContext)
    {
        _repository = repository;
        _userContext = userContext;
    }

    [AuthorizationAspect(ResourceCode = "manage_approval_rules", ActionCode = "manage")]
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result<CreateApprovalWorkflowResponse>> Handle(CreateApprovalWorkflowCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_userContext.UserId))
            return Result<CreateApprovalWorkflowResponse>.Failure("Authenticated user is required.");

        var category = DomainEnumerationParser.FromName<UserCategory>(request.TargetUserCategory)!;

        var result = ApprovalWorkflow.Create(
            TenantId.Load(request.TenantId),
            Code.Create(request.Code),
            Name.Create(request.Name),
            Description.Create(request.Description),
            category,
            request.RequiresApproval,
            request.SystemSuiteId.HasValue ? SystemSuiteId.Load(request.SystemSuiteId.Value) : null,
            ActorId.Create(_userContext.UserId),
            request.RequiresApproval ? 1 : 0);

        if (result.IsFailure)
            return Result<CreateApprovalWorkflowResponse>.Failure(result.Error);

        await _repository.AddAsync(result.Value, cancellationToken);
        await _repository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        return Result<CreateApprovalWorkflowResponse>.Success(new CreateApprovalWorkflowResponse(result.Value.Props.Id.GetValue()));
    }
}
