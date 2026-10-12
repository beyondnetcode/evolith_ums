using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Application.Identity.UserAccount.DTOs;
using Evolith.Ums.Domain.Enums;
using Evolith.Ums.Domain.Identity;

namespace Evolith.Ums.Application.Identity.UserAccount.Queries;

public sealed class GetPendingUserSignupRequestsQueryHandler
    : IQueryHandler<GetPendingUserSignupRequestsQuery, IReadOnlyList<PendingUserSignupDto>>
{
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly ITenantContext _tenantContext;

    public GetPendingUserSignupRequestsQueryHandler(
        IUserAccountRepository userAccountRepository,
        ITenantContext tenantContext)
    {
        _userAccountRepository = userAccountRepository;
        _tenantContext = tenantContext;
    }

    [AuthorizationAspect(ResourceCode = "view_users", ActionCode = "view")]
    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result<IReadOnlyList<PendingUserSignupDto>>> Handle(
        GetPendingUserSignupRequestsQuery request, CancellationToken cancellationToken)
    {
        if (_tenantContext.OrganizationId is null)
            return Result<IReadOnlyList<PendingUserSignupDto>>.Failure("Tenant context is required.");

        var users = await _userAccountRepository.GetByTenantIdAsync(
            _tenantContext.OrganizationId.Value, cancellationToken);

        var pending = users
            .Where(u => u.Status == UserStatus.Pending)
            .Select(u => new PendingUserSignupDto(
                u.Props.Id.GetValue(),
                u.TenantId.GetValue(),
                u.Email.GetValue(),
                u.DisplayName?.GetValue(),
                u.Category.Name,
                u.Props.Audit.GetValue().CreatedAt))
            .OrderBy(u => u.RequestedAt)
            .ToList();

        return Result<IReadOnlyList<PendingUserSignupDto>>.Success(pending);
    }
}
