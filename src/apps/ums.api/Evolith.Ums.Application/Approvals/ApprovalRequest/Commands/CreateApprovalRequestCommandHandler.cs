using Evolith.Ums.Application.Approvals.ApprovalRequest.DTOs;

namespace Evolith.Ums.Application.Approvals.ApprovalRequest.Commands;

using Evolith.Ums.Application.Approvals.ApprovalRequest.Services;
using Evolith.Ums.Domain.Approvals;
using Evolith.Ums.Domain.Approvals.ApprovalRequest;
using Evolith.Ums.Domain.Enums;
using Evolith.Ums.Domain.Identity;
using Evolith.Ums.Domain.Kernel.ValueObjects;

public sealed class CreateApprovalRequestCommandHandler : ICommandHandler<CreateApprovalRequestCommand, CreateApprovalRequestResponse>
{
    private readonly IApprovalRequestRepository _repository;
    private readonly IApprovalWorkflowRepository _workflowRepository;
    private readonly IApprovalRequestCreationPolicyResolver _creationPolicyResolver;
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IUserContext _userContext;

    public CreateApprovalRequestCommandHandler(
        IApprovalRequestRepository repository,
        IApprovalWorkflowRepository workflowRepository,
        IApprovalRequestCreationPolicyResolver creationPolicyResolver,
        IUserAccountRepository userAccountRepository,
        IUserContext userContext)
    {
        _repository = repository;
        _workflowRepository = workflowRepository;
        _creationPolicyResolver = creationPolicyResolver;
        _userAccountRepository = userAccountRepository;
        _userContext = userContext;
    }

    [AuthorizationAspect(ResourceCode = "manage_approvals", ActionCode = "manage")]
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result<CreateApprovalRequestResponse>> Handle(CreateApprovalRequestCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_userContext.UserId))
            return Result<CreateApprovalRequestResponse>.Failure("Authenticated user is required.");

        var workflow = await _workflowRepository.GetByIdAsync(request.WorkflowId, cancellationToken);
        if (workflow is null)
            return Result<CreateApprovalRequestResponse>.Failure("Approval workflow not found.");

        var targetUser = await _userAccountRepository.GetByIdAsync(request.TargetUserId, cancellationToken);
        if (targetUser is null)
            return Result<CreateApprovalRequestResponse>.Failure("Target user not found.");

        if (workflow.TargetUserCategory == UserCategory.Internal && targetUser.Category != UserCategory.Internal)
            return Result<CreateApprovalRequestResponse>.Failure(DomainErrors.Approvals.WorkflowNotAllowedForUserCategory);

        var duplicatePending = await _repository.ExistsPendingForScopeAsync(
            request.TargetUserId, request.RequestedSystemId, request.RequestedBranchId, cancellationToken);
        if (duplicatePending)
            return Result<CreateApprovalRequestResponse>.Failure(
                "A pending profile request already exists for this user, system, and branch.");

        var result = _creationPolicyResolver.Create(
            workflow,
            UserId.Load(request.TargetUserId),
            request.TargetProfileId.HasValue ? ProfileId.Load(request.TargetProfileId.Value) : null,
            SystemSuiteId.Load(request.RequestedSystemId),
            request.RequestedBranchId.HasValue ? BranchId.Load(request.RequestedBranchId.Value) : null,
            RoleId.Load(request.RequestedRoleId),
            request.Justification,
            ActorId.Create(_userContext.UserId));

        if (result.IsFailure) return Result<CreateApprovalRequestResponse>.Failure(result.Error);

        await _repository.AddAsync(result.Value, cancellationToken);
        await _repository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        return Result<CreateApprovalRequestResponse>.Success(new CreateApprovalRequestResponse(result.Value.Props.Id.GetValue()));
    }
}
