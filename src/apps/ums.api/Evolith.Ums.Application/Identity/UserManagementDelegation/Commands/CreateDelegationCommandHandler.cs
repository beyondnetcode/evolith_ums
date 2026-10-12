using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Application.Identity.UserManagementDelegation.DTOs;
using Evolith.Ums.Domain.Enums;
using Evolith.Ums.Domain.Identity.UserManagementDelegation;

namespace Evolith.Ums.Application.Identity.UserManagementDelegation.Commands;

public sealed class CreateDelegationCommandHandler : ICommandHandler<CreateDelegationCommand, CreateDelegationResponse>
{
    private readonly IUserManagementDelegationRepository _repository;
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IUserContext _userContext;

    public CreateDelegationCommandHandler(
        IUserManagementDelegationRepository repository,
        IUserAccountRepository userAccountRepository,
        IUserContext userContext)
    {
        _repository = repository;
        _userAccountRepository = userAccountRepository;
        _userContext = userContext;
    }

    [AuthorizationAspect(ResourceCode = "manage_delegations", ActionCode = "manage")]
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result<CreateDelegationResponse>> Handle(
        CreateDelegationCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_userContext.UserId))
        {
            return Result<CreateDelegationResponse>.Failure("Authenticated user is required to create a delegation.");
        }

        if (!Guid.TryParse(_userContext.UserId, out var currentUserId) || currentUserId != request.DelegatingAdminId)
        {
            return Result<CreateDelegationResponse>.Failure("Delegation can only be created by the delegating user.");
        }

        var scopeType = DomainEnumerationParser.FromName<DelegationScopeType>(request.ScopeType);
        if (scopeType is null)
        {
            return Result<CreateDelegationResponse>.Failure($"Invalid scope type: {request.ScopeType}.");
        }

        if (!string.Equals(scopeType.Name, DelegationScopeType.Tenant.Name, StringComparison.OrdinalIgnoreCase))
        {
            return Result<CreateDelegationResponse>.Failure("Only tenant-scoped delegations are allowed for UMS delegation management.");
        }

        var delegatingAdmin = await _userAccountRepository.GetByIdAsync(request.DelegatingAdminId, cancellationToken);
        if (delegatingAdmin is null)
        {
            return Result<CreateDelegationResponse>.Failure("Delegating user account was not found.");
        }

        if (delegatingAdmin.Status != UserStatus.Active)
        {
            return Result<CreateDelegationResponse>.Failure("Delegating user account is not active.");
        }

        var delegatedAdmin = await _userAccountRepository.GetByIdAsync(request.DelegatedAdminId, cancellationToken);
        if (delegatedAdmin is null)
        {
            return Result<CreateDelegationResponse>.Failure("Delegated user account was not found.");
        }

        if (delegatedAdmin.Status != UserStatus.Active)
        {
            return Result<CreateDelegationResponse>.Failure("Delegated user account is not active.");
        }

        if (delegatingAdmin.TenantId.GetValue() != request.TenantId || delegatedAdmin.TenantId.GetValue() != request.TenantId)
        {
            return Result<CreateDelegationResponse>.Failure("Delegation is only allowed within the same tenant.");
        }

        var allowedActions = request.AllowedActions
            .Select(a => DomainEnumerationParser.FromName<DelegatedAction>(a))
            .Where(a => a is not null)
            .Select(a => a!)
            .ToList();

        if (allowedActions.Count == 0)
        {
            return Result<CreateDelegationResponse>.Failure("At least one valid allowed action is required.");
        }

        var delegationResult = Evolith.Ums.Domain.Identity.UserManagementDelegation.UserManagementDelegation.Create(
            TenantId.Load(request.TenantId),
            UserAccountId.Load(request.DelegatingAdminId),
            UserAccountId.Load(request.DelegatedAdminId),
            scopeType,
            request.ScopeId,
            allowedActions,
            request.ValidFrom,
            request.ValidUntil,
            request.MaxDurationDays,
            request.RequiresApproval,
            ActorId.Create(_userContext.UserId));

        if (delegationResult.IsFailure)
        {
            return Result<CreateDelegationResponse>.Failure(delegationResult.Error);
        }

        await _repository.AddAsync(delegationResult.Value, cancellationToken);
        await _repository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        return Result<CreateDelegationResponse>.Success(
            new CreateDelegationResponse(delegationResult.Value.GetId().GetValue()));
    }
}
