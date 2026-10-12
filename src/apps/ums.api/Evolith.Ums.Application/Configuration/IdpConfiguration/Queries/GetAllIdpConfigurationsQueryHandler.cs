using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Application.Configuration.IdpConfiguration.DTOs;
using Evolith.Ums.Domain.Configuration;
using static Evolith.Ums.Application.Common.QueryRequestNormalizer;

namespace Evolith.Ums.Application.Configuration.IdpConfiguration.Queries;

public sealed class GetAllIdpConfigurationsQueryHandler : IQueryHandler<GetAllIdpConfigurationsQuery, PagedResult<IdpConfigurationDto>>
{
    private readonly IIdpConfigurationRepository _repository;
    private readonly ITenantContext? _tenantContext;

    public GetAllIdpConfigurationsQueryHandler(IIdpConfigurationRepository repository, ITenantContext? tenantContext = null)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]

    [AuthorizationAspect(ResourceCode = "view_config", ActionCode = "view")]
    public async Task<Result<PagedResult<IdpConfigurationDto>>> Handle(GetAllIdpConfigurationsQuery request, CancellationToken cancellationToken)
    {
        var page = NormalizePage(request.Page);
        var pageSize = NormalizePageSize(request.PageSize);
        var search = NormalizeSearch(request.Search);
        var status = NormalizeText(request.Status, "all");
        var sortBy = NormalizeText(request.SortBy, "resolutionPriority").ToLowerInvariant();
        var sortOrder = NormalizeText(request.SortOrder, "asc").ToLowerInvariant();
        var providerType = NormalizeSearch(request.ProviderType);

        var effectiveTenantId = (_tenantContext?.IsInternalAdmin == true)
            ? request.TenantId
            : _tenantContext?.OrganizationId;

        var query = (effectiveTenantId.HasValue
                ? await _repository.GetByTenantIdAsync(effectiveTenantId.Value, cancellationToken)
                : await _repository.GetAllAsync(null, cancellationToken))
            .Select(configuration => new IdpConfigurationDto(
                configuration.Props.Id.GetValue(),
                configuration.Props.TenantId.GetValue(),
                configuration.Props.SystemSuiteId.GetValue(),
                configuration.Props.ProviderType.Name,
                configuration.Props.DomainHints,
                configuration.Props.ConfigPayload,
                configuration.Props.SecretRef,
                configuration.Props.Status.Name,
                configuration.Props.ResolutionPriority,
                configuration.Props.FallbackToId,
                configuration.Props.Version));

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(configuration =>
                configuration.ProviderType.Contains(search, StringComparison.OrdinalIgnoreCase)
                || configuration.SecretRef.Contains(search, StringComparison.OrdinalIgnoreCase)
                || configuration.DomainHints.Any(hint => hint.Contains(search, StringComparison.OrdinalIgnoreCase)));
        }

        if (!string.Equals(status, "all", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(configuration => string.Equals(configuration.Status, status, StringComparison.OrdinalIgnoreCase));
        }

        if (request.SystemSuiteId.HasValue)
        {
            query = query.Where(configuration => configuration.SystemSuiteId == request.SystemSuiteId.Value);
        }

        if (!string.IsNullOrWhiteSpace(providerType))
        {
            query = query.Where(configuration => string.Equals(configuration.ProviderType, providerType, StringComparison.OrdinalIgnoreCase));
        }

        query = (sortBy, sortOrder) switch
        {
            ("providertype", "desc") => query.OrderByDescending(configuration => configuration.ProviderType).ThenByDescending(configuration => configuration.ResolutionPriority),
            ("providertype", _) => query.OrderBy(configuration => configuration.ProviderType).ThenBy(configuration => configuration.ResolutionPriority),
            ("status", "desc") => query.OrderByDescending(configuration => configuration.Status).ThenByDescending(configuration => configuration.ResolutionPriority),
            ("status", _) => query.OrderBy(configuration => configuration.Status).ThenBy(configuration => configuration.ResolutionPriority),
            ("version", "desc") => query.OrderByDescending(configuration => configuration.Version).ThenByDescending(configuration => configuration.ResolutionPriority),
            ("version", _) => query.OrderBy(configuration => configuration.Version).ThenBy(configuration => configuration.ResolutionPriority),
            ("resolutionpriority", "desc") => query.OrderByDescending(configuration => configuration.ResolutionPriority),
            _ => query.OrderBy(configuration => configuration.ResolutionPriority),
        };

        var totalItems = query.Count();
        var totalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);
        var paged = query.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return Result<PagedResult<IdpConfigurationDto>>.Success(new PagedResult<IdpConfigurationDto>(paged, page, pageSize, totalItems, totalPages));
    }
}
