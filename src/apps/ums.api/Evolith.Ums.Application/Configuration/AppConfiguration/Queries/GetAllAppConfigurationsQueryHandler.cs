using Evolith.Ums.Application.Common;
using Evolith.Ums.Application.Configuration.AppConfiguration.DTOs;
using Evolith.Ums.Application.Configuration.Services;
using Evolith.Ums.Domain.Configuration;
using static Evolith.Ums.Application.Common.QueryRequestNormalizer;

namespace Evolith.Ums.Application.Configuration.AppConfiguration.Queries;

public sealed class GetAllAppConfigurationsQueryHandler : IQueryHandler<GetAllAppConfigurationsQuery, PagedResult<AppConfigurationDto>>
{
    private readonly IAppConfigurationRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly IValueEncryptionService _encryption;

    public GetAllAppConfigurationsQueryHandler(
        IAppConfigurationRepository repository,
        ITenantContext tenantContext,
        IValueEncryptionService encryption)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _encryption = encryption;
    }

    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]

    [AuthorizationAspect(ResourceCode = "view_params", ActionCode = "view")]
    public async Task<Result<PagedResult<AppConfigurationDto>>> Handle(GetAllAppConfigurationsQuery request, CancellationToken cancellationToken)
    {
        var page = NormalizePage(request.Page);
        var pageSize = NormalizePageSize(request.PageSize);
        var search = NormalizeSearch(request.Search);
        var status = NormalizeText(request.Status, "all");
        var sortBy = NormalizeText(request.SortBy, "code").ToLowerInvariant();
        var sortOrder = NormalizeText(request.SortOrder, "asc").ToLowerInvariant();
        var scope = NormalizeSearch(request.Scope);

        var isInternalAdmin = _tenantContext.IsInternalAdmin;
        var userTenantId = _tenantContext.OrganizationId;

        // Determine which configs to fetch:
        // 1. Request has tenantId -> fetch that specific tenant's configs
        // 2. Request has no tenantId and user is not internal admin -> fetch user's tenant
        // 3. Internal admin with no tenantId requested -> fetch all
        Guid? targetTenantId = null;

        if (request.TenantId.HasValue)
        {
            // Normalize to uppercase for case-insensitive comparison
            targetTenantId = Guid.Parse(request.TenantId.Value.ToString().ToUpperInvariant());
        }
        else if (!isInternalAdmin && userTenantId.HasValue)
        {
            targetTenantId = userTenantId;
        }
        // else (internal admin, no tenant specified) -> null means all

        var allConfigs = await _repository.GetAllAsync(targetTenantId, cancellationToken);

        var query = allConfigs
            .Select(configuration => new AppConfigurationDto(
                configuration.Props.Id.GetValue(),
                configuration.Props.TenantId?.GetValue(),
                configuration.Props.SystemSuiteId?.GetValue(),
                configuration.Props.ModuleId?.GetValue(),
                configuration.Props.Code.GetValue(),
                ResolveValue(configuration.Props.Value.GetValue(), configuration.Props.IsEncrypted),
                configuration.Props.Description.GetValue(),
                configuration.Props.Scope.Name,
                configuration.Props.IsInheritable,
                configuration.Props.IsEncrypted,
                configuration.Props.IsNonOverridable,
                configuration.Props.Version,
                configuration.Props.Status.Name))
            .AsEnumerable()  // Client-side filtering after repository query
            .AsQueryable();

        // Apply authorization filter: non-internal admins cannot see global configurations
        // EXCEPT when viewing a specific tenant's configs (to see base defaults + tenant overrides)
        if (!isInternalAdmin)
        {
            if (request.TenantId.HasValue)
            {
                // When viewing specific tenant, show: Global (base defaults) + Tenant (overrides)
                query = query.Where(configuration =>
                    configuration.Scope == "Global" ||
                    configuration.Scope == "Tenant");
            }
            else
            {
                // No specific tenant requested, show only tenant's own configs
                query = query.Where(configuration =>
                    configuration.Scope == "Tenant");
            }
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(configuration =>
                configuration.Code.Contains(search, StringComparison.OrdinalIgnoreCase)
                || configuration.Value.Contains(search, StringComparison.OrdinalIgnoreCase)
                || configuration.Description.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.Equals(status, "all", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(configuration => string.Equals(configuration.Status, status, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(scope))
        {
            query = query.Where(configuration => string.Equals(configuration.Scope, scope, StringComparison.OrdinalIgnoreCase));
        }

        if (targetTenantId.HasValue)
        {
            query = query.Where(configuration => configuration.TenantId == targetTenantId.Value);
        }

        if (request.SystemSuiteId.HasValue)
        {
            query = query.Where(configuration => configuration.SystemSuiteId == request.SystemSuiteId.Value);
        }

        if (request.ModuleId.HasValue)
        {
            query = query.Where(configuration => configuration.ModuleId == request.ModuleId.Value);
        }

        query = (sortBy, sortOrder) switch
        {
            ("scope", "desc") => query.OrderByDescending(configuration => configuration.Scope).ThenByDescending(configuration => configuration.Code),
            ("scope", _) => query.OrderBy(configuration => configuration.Scope).ThenBy(configuration => configuration.Code),
            ("status", "desc") => query.OrderByDescending(configuration => configuration.Status).ThenByDescending(configuration => configuration.Code),
            ("status", _) => query.OrderBy(configuration => configuration.Status).ThenBy(configuration => configuration.Code),
            ("version", "desc") => query.OrderByDescending(configuration => configuration.Version).ThenByDescending(configuration => configuration.Code),
            ("version", _) => query.OrderBy(configuration => configuration.Version).ThenBy(configuration => configuration.Code),
            ("code", "desc") => query.OrderByDescending(configuration => configuration.Code),
            _ => query.OrderBy(configuration => configuration.Code),
        };

        var totalItems = query.Count();
        var totalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);
        var paged = query.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return Result<PagedResult<AppConfigurationDto>>.Success(new PagedResult<AppConfigurationDto>(paged, page, pageSize, totalItems, totalPages));
    }

    private string ResolveValue(string storedValue, bool isEncrypted)
    {
        if (!isEncrypted) return storedValue;
        if (!_tenantContext.IsInternalAdmin) return "***";
        return _encryption.IsEncryptedValue(storedValue) ? _encryption.Decrypt(storedValue) : storedValue;
    }
}
