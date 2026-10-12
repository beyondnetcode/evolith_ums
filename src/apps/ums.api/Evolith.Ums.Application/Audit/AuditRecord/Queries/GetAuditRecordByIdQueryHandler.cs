using Evolith.Ums.Application.Audit.AuditRecord.DTOs;
using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Domain.Audit.AuditRecord;

namespace Evolith.Ums.Application.Audit.AuditRecord.Queries;

public sealed class GetAuditRecordByIdQueryHandler : IQueryHandler<GetAuditRecordByIdQuery, AuditRecordDto>
{
    private readonly IAuditRecordRepository _auditRecordRepository;
    private readonly ITenantContext? _tenantContext;

    public GetAuditRecordByIdQueryHandler(IAuditRecordRepository auditRecordRepository, ITenantContext? tenantContext = null)
    {
        _auditRecordRepository = auditRecordRepository;
        _tenantContext = tenantContext;
    }

    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]

    [AuthorizationAspect(ResourceCode = "view_audit", ActionCode = "view")]
    public async Task<Result<AuditRecordDto>> Handle(
        GetAuditRecordByIdQuery request,
        CancellationToken cancellationToken)
    {
        var record = await _auditRecordRepository.GetByIdAsync(request.AuditRecordId, cancellationToken);

        if (record is null)
        {
            return Result<AuditRecordDto>.Failure("Audit record not found.");
        }

        // G-040 (SEGURIDAD): aislamiento por inquilino. Un usuario regular solo puede
        // leer registros de su propio inquilino; los admin internos ven todos. Se
        // devuelve "not found" para no revelar la existencia de registros ajenos.
        if (_tenantContext?.IsInternalAdmin != true)
        {
            var effectiveTenantId = _tenantContext?.OrganizationId;
            if (effectiveTenantId is null || record.Props.RootTenantId != effectiveTenantId.Value)
            {
                return Result<AuditRecordDto>.Failure("Audit record not found.");
            }
        }

        return Result<AuditRecordDto>.Success(new AuditRecordDto(
            record.Props.Id.GetValue(),
            record.Props.WhoActed,
            record.Props.SubjectType.ToString(),
            record.Props.WhenOccurred,
            record.Props.WhatChanged,
            record.Props.EventType,
            record.Props.AuditResult.ToString(),
            record.Props.AffectedEntityId,
            record.Props.AffectedEntityType,
            record.Props.RootTenantId,
            record.Props.Metadata));
    }
}
