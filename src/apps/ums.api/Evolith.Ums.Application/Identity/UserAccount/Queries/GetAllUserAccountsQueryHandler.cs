using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Application.Identity.UserAccount.DTOs;
using Evolith.Ums.Domain.Identity;
using static Evolith.Ums.Application.Common.QueryRequestNormalizer;

namespace Evolith.Ums.Application.Identity.UserAccount.Queries;

public sealed class GetAllUserAccountsQueryHandler : IQueryHandler<GetAllUserAccountsQuery, PagedResult<UserAccountDto>>
{
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IUserContext _userContext;
    private readonly ITenantContext? _tenantContext;

    public GetAllUserAccountsQueryHandler(
        IUserAccountRepository userAccountRepository,
        IUserContext userContext,
        ITenantContext? tenantContext = null)
    {
        _userAccountRepository = userAccountRepository;
        _userContext = userContext;
        _tenantContext = tenantContext;
    }

    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]

    [AuthorizationAspect(ResourceCode = "view_users", ActionCode = "view")]
    public async Task<Result<PagedResult<UserAccountDto>>> Handle(
        GetAllUserAccountsQuery request,
        CancellationToken cancellationToken)
    {
        var page = NormalizePage(request.Page);
        var pageSize = NormalizePageSize(request.PageSize);
        var status = NormalizeText(request.Status, "all");
        var sortBy = NormalizeText(request.SortBy, "email").ToLowerInvariant();
        var sortOrder = NormalizeText(request.SortOrder, "asc").ToLowerInvariant();
        var search = NormalizeSearch(request.Search);

        var isInternalAdmin = _tenantContext?.IsInternalAdmin ?? false;
        Guid? effectiveTenantId = null;

        if (isInternalAdmin)
        {
            // Internal admin sees all users from all tenants if TenantId is null
            effectiveTenantId = request.TenantId;
            if (effectiveTenantId == null)
            {
                _tenantContext?.EnableCrossTenantAccess();
            }
            else
            {
                _tenantContext?.SetOrganizationId(effectiveTenantId.Value);
            }
        }
        else
        {
            // Regular user is strictly restricted to their own tenant
            var ctxTenantId = !string.IsNullOrWhiteSpace(_userContext.TenantId) && Guid.TryParse(_userContext.TenantId, out var parsedTenantId)
                ? parsedTenantId
                : Guid.Empty;

            if (ctxTenantId == Guid.Empty || (request.TenantId.HasValue && request.TenantId.Value != ctxTenantId))
            {
                // Tenant isolation violation: tried to access another tenant's data
                // Force an empty Guid so no results are returned, preventing data leakage.
                effectiveTenantId = Guid.Empty;
            }
            else
            {
                effectiveTenantId = ctxTenantId;
            }
        }

        var (userAccounts, totalItems) = await _userAccountRepository.GetPagedAsync(
            page, pageSize, search, status, sortBy, sortOrder,
            tenantId: effectiveTenantId,
            cancellationToken: cancellationToken);

        var items = userAccounts.Select(u =>
        {
            var activePassword = u.PasswordCredentials.SingleOrDefault(x => x.IsActive);
            return new UserAccountDto(
                u.Props.Id.GetValue(),
                u.Props.TenantId.GetValue(),
                u.Props.BranchId?.GetValue(),
                u.Props.Email.GetValue(),
                u.Props.DisplayName?.GetValue(),
                u.Props.Category.ToString(),
                u.Props.Status.ToString(),
                u.Props.IdentityReference?.GetValue(),
                u.Props.IdentityReferenceType?.ToString(),
                activePassword is not null,
                activePassword?.Props.Audit.GetValue().UpdatedAt ?? activePassword?.Props.Audit.GetValue().CreatedAt);
        })
            .ToList();

        var totalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);

        return Result<PagedResult<UserAccountDto>>.Success(new PagedResult<UserAccountDto>(
            items,
            page,
            pageSize,
            totalItems,
            totalPages));
    }
}
