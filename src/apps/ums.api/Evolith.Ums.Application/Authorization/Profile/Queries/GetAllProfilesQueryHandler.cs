using Evolith.Ums.Application.Authorization.Profile.DTOs;
using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Domain.Authorization;
using Evolith.Ums.Domain.Identity;
using Evolith.Ums.Domain.Identity.UserAccount;
using static Evolith.Ums.Application.Common.QueryRequestNormalizer;

namespace Evolith.Ums.Application.Authorization.Profile.Queries;

#pragma warning disable S125

public sealed class GetAllProfilesQueryHandler : IQueryHandler<GetAllProfilesQuery, PagedResult<ProfileDto>>
{
    private readonly IProfileRepository _profileRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly ISystemSuiteRepository _systemSuiteRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IUserContext _userContext;
    private readonly ITenantContext? _tenantContext;

    public GetAllProfilesQueryHandler(
        IProfileRepository profileRepository,
        IRoleRepository roleRepository,
        ISystemSuiteRepository systemSuiteRepository,
        ITenantRepository tenantRepository,
        IUserAccountRepository userAccountRepository,
        IUserContext userContext,
        ITenantContext? tenantContext = null)
    {
        _profileRepository = profileRepository;
        _roleRepository = roleRepository;
        _systemSuiteRepository = systemSuiteRepository;
        _tenantRepository = tenantRepository;
        _userAccountRepository = userAccountRepository;
        _userContext = userContext;
        _tenantContext = tenantContext;
    }

    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]

    [AuthorizationAspect(ResourceCode = "view_profiles", ActionCode = "view")]
    public async Task<Result<PagedResult<ProfileDto>>> Handle(
        GetAllProfilesQuery request,
        CancellationToken cancellationToken)
    {
        var page = NormalizePage(request.Page);
        var pageSize = NormalizePageSize(request.PageSize);
        var criteria = NormalizeText(request.Criteria, "userId").ToLowerInvariant();
        var status = NormalizeText(request.Status, "all");
        var sortBy = NormalizeText(request.SortBy, "userId").ToLowerInvariant();
        var sortOrder = NormalizeText(request.SortOrder, "asc").ToLowerInvariant();
        var search = NormalizeSearch(request.Search);

        // Tenant isolation: ITenantContext is authoritative. Admins may use request.TenantId;
        // regular users are always scoped to their own tenant.
        Guid? effectiveTenantId;
        if (_tenantContext?.IsInternalAdmin == true)
        {
            effectiveTenantId = request.TenantId;
        }
        else
        {
            effectiveTenantId = _tenantContext?.OrganizationId ?? (
                !string.IsNullOrWhiteSpace(_userContext.TenantId) && Guid.TryParse(_userContext.TenantId, out var ctxTenantId)
                    ? ctxTenantId : (Guid?)null);
        }

        IReadOnlyList<Domain.Authorization.Profile.Profile> profiles;
        if (request.UserId.HasValue)
        {
            profiles = await _profileRepository.GetByUserIdAsync(request.UserId.Value, cancellationToken);
        }
        else if (effectiveTenantId.HasValue)
        {
            profiles = await _profileRepository.GetByTenantIdAsync(effectiveTenantId.Value, cancellationToken);
        }
        else
        {
            profiles = await _profileRepository.GetAllAsync(effectiveTenantId, cancellationToken);
        }

        var allTenants = await _tenantRepository.GetAllAsync(null, cancellationToken);
        var profileTenantIds = profiles.Select(p => p.Props.TenantId.GetValue()).Distinct().ToList();

        var allRoles = new List<Evolith.Ums.Domain.Authorization.Role.Role>();
        foreach (var tenantId in profileTenantIds)
        {
            var tenantRoles = await _roleRepository.GetByTenantIdAsync(tenantId, cancellationToken);
            allRoles.AddRange(tenantRoles);
        }

        var allUsers = await _userAccountRepository.GetAllAsync(effectiveTenantId, cancellationToken);

        var roleSystemSuiteIds = allRoles.Select(r => r.Props.SystemSuiteId.GetValue()).Distinct().ToList();
        var allSuites = new List<Evolith.Ums.Domain.Authorization.SystemSuite.SystemSuite>();
        foreach (var suiteId in roleSystemSuiteIds)
        {
            var suite = await _systemSuiteRepository.GetByIdAsync(suiteId, cancellationToken);
            if (suite != null) allSuites.Add(suite);
        }

        var tenantLookup = allTenants.ToDictionary(t => t.Props.Id.GetValue(), t => (Code: t.Props.Code.GetValue(), Name: t.Props.Name.GetValue()));
        var roleLookup = allRoles.ToDictionary(r => r.Props.Id.GetValue(), r => (Code: r.Props.Code.GetValue(), Name: r.Props.Value.GetValue(), SystemSuiteId: r.Props.SystemSuiteId.GetValue()));
        var userLookup = allUsers.ToDictionary(u => u.Props.Id.GetValue(), u => u.Props.Email.GetValue());
        var suiteLookup = allSuites.ToDictionary(s => s.Props.Id.GetValue(), s => (Code: s.Props.Code.GetValue(), Name: s.Props.Name.GetValue()));

        var query = profiles.Select(p =>
        {
            var roleInfo = roleLookup.GetValueOrDefault(p.Props.RoleId.GetValue());
            var suiteInfo = suiteLookup.GetValueOrDefault(roleInfo.SystemSuiteId);
            // Solo lo vivo: los permisos desligados quedan desactivados, no borrados (ADR-UMS-106).
            var permCount = p.Permissions.Count(x => x.IsActive);
            return new ProfileDto(
                p.Props.Id.GetValue(),
                p.Props.TenantId.GetValue(),
                tenantLookup.GetValueOrDefault(p.Props.TenantId.GetValue()).Code,
                tenantLookup.GetValueOrDefault(p.Props.TenantId.GetValue()).Name,
                p.Props.UserId.GetValue(),
                userLookup.GetValueOrDefault(p.Props.UserId.GetValue()) ?? "—",
                p.Props.RoleId.GetValue(),
                roleInfo.Code ?? "—",
                roleInfo.Name ?? "—",
                roleInfo.SystemSuiteId,
                suiteInfo.Code ?? "—",
                suiteInfo.Name ?? "—",
                p.Props.BranchId?.GetValue(),
                null,
                p.Props.Scope.ToString(),
                p.Props.IsActive,
                permCount,
                new List<ProfilePermissionDto>());
        });

        if (!string.Equals(status, "all", StringComparison.OrdinalIgnoreCase))
        {
            var isActiveStatus = string.Equals(status, "active", StringComparison.OrdinalIgnoreCase);
            query = query.Where(p => p.IsActive == isActiveStatus);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = criteria switch
            {
                "user" => query.Where(p => p.UserEmail.Contains(search, StringComparison.OrdinalIgnoreCase)),
                "role" => query.Where(p => p.RoleName.Contains(search, StringComparison.OrdinalIgnoreCase) || p.RoleCode.Contains(search, StringComparison.OrdinalIgnoreCase)),
                "tenant" => query.Where(p => p.TenantName.Contains(search, StringComparison.OrdinalIgnoreCase) || p.TenantCode.Contains(search, StringComparison.OrdinalIgnoreCase)),
                "system" => query.Where(p => p.SystemSuiteName.Contains(search, StringComparison.OrdinalIgnoreCase) || p.SystemSuiteCode.Contains(search, StringComparison.OrdinalIgnoreCase)),
                _ => query.Where(p => p.UserEmail.Contains(search, StringComparison.OrdinalIgnoreCase)),
            };
        }

        query = (sortBy, sortOrder) switch
        {
            ("scope", "desc") => query.OrderByDescending(p => p.Scope),
            ("scope", _) => query.OrderBy(p => p.Scope),
            ("role", "desc") => query.OrderByDescending(p => p.RoleName),
            ("role", _) => query.OrderBy(p => p.RoleName),
            ("user", "desc") => query.OrderByDescending(p => p.UserEmail),
            ("user", _) => query.OrderBy(p => p.UserEmail),
            ("tenant", "desc") => query.OrderByDescending(p => p.TenantName),
            ("tenant", _) => query.OrderBy(p => p.TenantName),
            ("system", "desc") => query.OrderByDescending(p => p.SystemSuiteName),
            ("system", _) => query.OrderBy(p => p.SystemSuiteName),
            _ => query.OrderBy(p => p.UserEmail),
        };

        var totalItems = query.Count();
        var totalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);
        var items = query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Result<PagedResult<ProfileDto>>.Success(new PagedResult<ProfileDto>(
            items,
            page,
            pageSize,
            totalItems,
            totalPages));
    }
}
