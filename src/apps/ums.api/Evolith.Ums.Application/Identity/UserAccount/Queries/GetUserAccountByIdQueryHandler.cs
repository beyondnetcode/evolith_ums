using Evolith.Ums.Application.Identity.UserAccount.DTOs;
using Evolith.Ums.Domain.Identity.UserAccount;

namespace Evolith.Ums.Application.Identity.UserAccount.Queries;

public sealed class GetUserAccountByIdQueryHandler : IQueryHandler<GetUserAccountByIdQuery, UserAccountDto>
{
    private readonly IUserAccountRepository _userAccountRepository;

    public GetUserAccountByIdQueryHandler(IUserAccountRepository userAccountRepository)
    {
        _userAccountRepository = userAccountRepository;
    }

    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]

    [AuthorizationAspect(ResourceCode = "view_users", ActionCode = "view")]
    public async Task<Result<UserAccountDto>> Handle(
        GetUserAccountByIdQuery request,
        CancellationToken cancellationToken)
    {
        var userAccount = await _userAccountRepository.GetByIdAsync(request.UserAccountId, cancellationToken);

        if (userAccount is null)
        {
            return Result<UserAccountDto>.Failure("User account not found.");
        }

        var activePassword = userAccount.PasswordCredentials.SingleOrDefault(x => x.IsActive);

        return Result<UserAccountDto>.Success(new UserAccountDto(
            userAccount.Props.Id.GetValue(),
            userAccount.Props.TenantId.GetValue(),
            userAccount.Props.BranchId?.GetValue(),
            userAccount.Props.Email.GetValue(),
            userAccount.Props.DisplayName?.GetValue(),
            userAccount.Props.Category.ToString(),
            userAccount.Props.Status.ToString(),
            userAccount.Props.IdentityReference?.GetValue(),
            userAccount.Props.IdentityReferenceType?.ToString(),
            activePassword is not null,
            activePassword?.Props.Audit.GetValue().UpdatedAt ?? activePassword?.Props.Audit.GetValue().CreatedAt,
            userAccount.ExpiresAt));
    }
}
