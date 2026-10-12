using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Application.Identity.UserAccount.DTOs;

namespace Evolith.Ums.Application.Identity.UserAccount.Commands;

using Evolith.Ums.Domain.Enums;
using Evolith.Ums.Domain.Identity.UserAccount;

public sealed class CreateUserAccountCommandHandler : ICommandHandler<CreateUserAccountCommand, CreateUserAccountResponse>
{
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IUserContext _userContext;
    private readonly ITenantScopePolicy _tenantScopePolicy;
    private readonly IUserManagementDelegationAccessService _delegationAccessService;

    public CreateUserAccountCommandHandler(
        IUserAccountRepository userAccountRepository,
        IUserContext userContext,
        ITenantScopePolicy tenantScopePolicy,
        IUserManagementDelegationAccessService delegationAccessService)
    {
        _userAccountRepository = userAccountRepository;
        _userContext = userContext;
        _tenantScopePolicy = tenantScopePolicy;
        _delegationAccessService = delegationAccessService;
    }

    [AuthorizationAspect(ResourceCode = "manage_users", ActionCode = "manage")]
    [AuditTrail]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result<CreateUserAccountResponse>> Handle(
        CreateUserAccountCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_userContext.UserId))
        {
            return Result<CreateUserAccountResponse>.Failure("Authenticated user is required to create a user account.");
        }

        var scopeResult = await _tenantScopePolicy.EnsureManagementOwnerScopeAsync(request.TenantId, cancellationToken);
        if (scopeResult.IsFailure)
        {
            var delegatedAccess = await _delegationAccessService.EnsureCanExecuteAsync(
                request.TenantId,
                Evolith.Ums.Domain.Identity.UserManagementDelegation.DelegatedAction.CreateUser,
                null,
                cancellationToken);

            if (delegatedAccess.IsFailure)
            {
                return Result<CreateUserAccountResponse>.Failure(scopeResult.Error);
            }
        }

        var email = Email.Create(request.Email);
        var existingUser = await _userAccountRepository.GetByEmailAsync(email, cancellationToken);
        if (existingUser is not null)
        {
            return Result<CreateUserAccountResponse>.Failure("User email already exists.");
        }

        var category = DomainEnumerationParser.FromName<UserCategory>(request.Category)!;
        var identityReference = string.IsNullOrWhiteSpace(request.IdentityReference)
            ? null
            : IdentityReference.Create(request.IdentityReference);
        var identityReferenceType = string.IsNullOrWhiteSpace(request.IdentityReferenceType)
            ? null
            : DomainEnumerationParser.FromName<IdentityReferenceType>(request.IdentityReferenceType);
        var branchId = request.BranchId.HasValue
            ? BranchId.Load(request.BranchId.Value)
            : null;

        // Se pasa `null` y no un `Name` vacío cuando no viene: `Name.Create("")` fallaría, y un
        // nombre ausente es un dato que no se tiene, no una cadena vacía.
        var displayName = string.IsNullOrWhiteSpace(request.DisplayName)
            ? null
            : Name.Create(request.DisplayName.Trim());

        var userAccountResult = UserAccount.Create(
            TenantId.Load(request.TenantId),
            email,
            category,
            identityReference,
            identityReferenceType,
            ActorId.Create(_userContext.UserId),
            branchId,
            displayName: displayName);

        if (userAccountResult.IsFailure)
        {
            return Result<CreateUserAccountResponse>.Failure(userAccountResult.Error);
        }

        await _userAccountRepository.AddAsync(userAccountResult.Value, cancellationToken);
        await _userAccountRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        return Result<CreateUserAccountResponse>.Success(
            new CreateUserAccountResponse(userAccountResult.Value.Props.Id.GetValue()));
    }
}
