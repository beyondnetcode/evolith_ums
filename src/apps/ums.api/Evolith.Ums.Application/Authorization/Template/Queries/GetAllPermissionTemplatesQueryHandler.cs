using Evolith.Ums.Application.Authorization.Template.DTOs;
using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Domain.Authorization;
using static Evolith.Ums.Application.Common.QueryRequestNormalizer;

namespace Evolith.Ums.Application.Authorization.Template.Queries;

public sealed class GetAllPermissionTemplatesQueryHandler : IQueryHandler<GetAllPermissionTemplatesQuery, PagedResult<PermissionTemplateDto>>
{
    private readonly IPermissionTemplateRepository _templateRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly ISystemSuiteRepository _systemSuiteRepository;
    private readonly IUserContext _userContext;
    private readonly ITenantContext? _tenantContext;

    public GetAllPermissionTemplatesQueryHandler(
        IPermissionTemplateRepository templateRepository,
        IRoleRepository roleRepository,
        ISystemSuiteRepository systemSuiteRepository,
        IUserContext userContext,
        ITenantContext? tenantContext = null)
    {
        _templateRepository = templateRepository;
        _roleRepository = roleRepository;
        _systemSuiteRepository = systemSuiteRepository;
        _userContext = userContext;
        _tenantContext = tenantContext;
    }

    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]

    [AuthorizationAspect(ResourceCode = "view_templates", ActionCode = "view")]
    public async Task<Result<PagedResult<PermissionTemplateDto>>> Handle(
        GetAllPermissionTemplatesQuery request,
        CancellationToken cancellationToken)
    {
        var page = NormalizePage(request.Page);
        var pageSize = NormalizePageSize(request.PageSize);
        var criteria = NormalizeText(request.Criteria, "version").ToLowerInvariant();
        var status = NormalizeText(request.Status, "all");
        var sortBy = NormalizeText(request.SortBy, "version").ToLowerInvariant();
        var sortOrder = NormalizeText(request.SortOrder, "asc").ToLowerInvariant();
        var search = NormalizeSearch(request.Search);

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

        var templates = effectiveTenantId.HasValue
            ? await _templateRepository.GetByTenantIdAsync(effectiveTenantId.Value, cancellationToken)
            : await _templateRepository.GetAllAsync(null, cancellationToken);

        if (request.SystemSuiteId.HasValue)
        {
            templates = templates.Where(t => t.Props.SystemSuiteId.GetValue() == request.SystemSuiteId.Value).ToList();
        }

        if (request.RoleId.HasValue)
        {
            templates = templates.Where(t => t.Props.RoleId.GetValue() == request.RoleId.Value).ToList();
        }

        // Build lookup dictionaries to avoid N+1 queries
        var roleIds = templates.Select(t => t.Props.RoleId.GetValue()).Distinct().ToList();
        var suiteIds = templates.Select(t => t.Props.SystemSuiteId.GetValue()).Distinct().ToList();

        var roleNames = new Dictionary<Guid, string>();
        foreach (var roleId in roleIds)
        {
            var role = await _roleRepository.GetByIdAsync(roleId, cancellationToken);
            roleNames[roleId] = role?.Props.Value.GetValue() ?? roleId.ToString()[..8];
        }

        var suiteNames = new Dictionary<Guid, string>();
        foreach (var suiteId in suiteIds)
        {
            var suite = await _systemSuiteRepository.GetByIdAsync(suiteId, cancellationToken);
            suiteNames[suiteId] = suite?.Props.Name.GetValue() ?? suiteId.ToString()[..8];
        }

        var query = templates.Select(t => new PermissionTemplateDto(
            t.Props.Id.GetValue(),
            t.Props.TenantId.GetValue(),
            t.Props.RoleId.GetValue(),
            roleNames.GetValueOrDefault(t.Props.RoleId.GetValue(), "—"),
            t.Props.SystemSuiteId.GetValue(),
            suiteNames.GetValueOrDefault(t.Props.SystemSuiteId.GetValue(), "—"),
            t.Props.Version.GetValue(),
            t.Props.Status.ToString()));

        if (!string.Equals(status, "all", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(t => string.Equals(t.Status, status, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = criteria switch
            {
                "id"    => query.Where(t => t.TemplateId.ToString().Contains(search, StringComparison.OrdinalIgnoreCase)),
                "role"  => query.Where(t => t.RoleName.Contains(search, StringComparison.OrdinalIgnoreCase)),
                "suite" => query.Where(t => t.SystemSuiteName.Contains(search, StringComparison.OrdinalIgnoreCase)),
                _       => query.Where(t => t.Version.Contains(search, StringComparison.OrdinalIgnoreCase)),
            };
        }

        query = (sortBy, sortOrder) switch
        {
            ("status",  "desc") => query.OrderByDescending(t => t.Status),
            ("status",  _)      => query.OrderBy(t => t.Status),
            ("role",    "desc") => query.OrderByDescending(t => t.RoleName),
            ("role",    _)      => query.OrderBy(t => t.RoleName),
            ("suite",   "desc") => query.OrderByDescending(t => t.SystemSuiteName),
            ("suite",   _)      => query.OrderBy(t => t.SystemSuiteName),
            (_,         "desc") => query.OrderByDescending(t => t.Version),
            _                   => query.OrderBy(t => t.SystemSuiteName).ThenBy(t => t.RoleName).ThenBy(t => t.Version),
        };

        var totalItems = query.Count();
        var totalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);
        var items = query.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return Result<PagedResult<PermissionTemplateDto>>.Success(new PagedResult<PermissionTemplateDto>(
            items, page, pageSize, totalItems, totalPages));
    }
}
