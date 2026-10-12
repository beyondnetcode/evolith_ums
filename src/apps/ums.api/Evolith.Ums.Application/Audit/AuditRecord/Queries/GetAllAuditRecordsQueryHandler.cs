using Evolith.Ums.Application.Audit.AuditRecord.DTOs;
using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Domain.Audit.AuditRecord;
using static Evolith.Ums.Application.Common.QueryRequestNormalizer;
using AuditRecordAggregate = Evolith.Ums.Domain.Audit.AuditRecord.AuditRecord;

namespace Evolith.Ums.Application.Audit.AuditRecord.Queries;

public sealed class GetAllAuditRecordsQueryHandler : IQueryHandler<GetAllAuditRecordsQuery, PagedResult<AuditRecordDto>>
{
    private readonly IAuditRecordRepository _auditRecordRepository;
    private readonly ITenantContext? _tenantContext;

    public GetAllAuditRecordsQueryHandler(IAuditRecordRepository auditRecordRepository, ITenantContext? tenantContext = null)
    {
        _auditRecordRepository = auditRecordRepository;
        _tenantContext = tenantContext;
    }

    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]

    [AuthorizationAspect(ResourceCode = "view_audit", ActionCode = "view")]
    public async Task<Result<PagedResult<AuditRecordDto>>> Handle(
        GetAllAuditRecordsQuery request,
        CancellationToken cancellationToken)
    {
        var page = NormalizePage(request.Page);
        var pageSize = NormalizePageSize(request.PageSize);

        var from = request.From ?? DateTime.UtcNow.AddDays(-30);
        var to = request.To ?? DateTime.UtcNow;

        // Tenant isolation: regular users can only query their own tenant's audit records.
        // G-113: el internal-admin puede consultar un tenant específico (request.TenantId); SIN él,
        // cae a su PROPIA organización — no a Guid.Empty, que filtraba RootTenantId==empty y devolvía
        // 0 pese a existir registros (la escritura persiste; el query los ocultaba por el filtro vacío).
        var effectiveTenantId = (_tenantContext?.IsInternalAdmin == true)
            ? (request.TenantId ?? _tenantContext?.OrganizationId ?? Guid.Empty)
            : (_tenantContext?.OrganizationId ?? Guid.Empty);

        IReadOnlyList<AuditRecordAggregate> records;

        if (request.ActorId.HasValue)
        {
            records = await _auditRecordRepository.QueryByActorAsync(request.ActorId.Value, from, to, cancellationToken);
        }
        else if (request.EntityId.HasValue && !string.IsNullOrWhiteSpace(request.EntityType))
        {
            records = await _auditRecordRepository.QueryByEntityAsync(request.EntityId.Value, request.EntityType, effectiveTenantId, from, to, cancellationToken);
        }
        else if (!string.IsNullOrWhiteSpace(request.EventType))
        {
            records = await _auditRecordRepository.QueryByEventTypeAsync(request.EventType, effectiveTenantId, from, to, cancellationToken);
        }
        else
        {
            records = await _auditRecordRepository.QueryByEventTypeAsync("*", effectiveTenantId, from, to, cancellationToken);
        }

        var query = records.Select(r => new AuditRecordDto(
            r.Props.Id.GetValue(),
            r.Props.WhoActed,
            r.Props.SubjectType.ToString(),
            r.Props.WhenOccurred,
            r.Props.WhatChanged,
            r.Props.EventType,
            r.Props.AuditResult.ToString(),
            r.Props.AffectedEntityId,
            r.Props.AffectedEntityType,
            r.Props.RootTenantId,
            r.Props.Metadata));

        if (!string.IsNullOrWhiteSpace(request.EventType) && request.EventType != "*")
        {
            query = query.Where(r => r.EventType.Equals(request.EventType, StringComparison.OrdinalIgnoreCase));
        }

        query = query.OrderByDescending(r => r.WhenOccurred);

        var totalItems = query.Count();
        var totalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);
        var items = query.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return Result<PagedResult<AuditRecordDto>>.Success(new PagedResult<AuditRecordDto>(
            items, page, pageSize, totalItems, totalPages));
    }
}
