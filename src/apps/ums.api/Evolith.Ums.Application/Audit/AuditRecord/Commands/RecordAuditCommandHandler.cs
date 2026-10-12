using Evolith.Ums.Application.Audit.AuditRecord.DTOs;
using Evolith.Ums.Application.Common.Interfaces;

namespace Evolith.Ums.Application.Audit.AuditRecord.Commands;

using Evolith.Ums.Domain.Audit.AuditRecord;
using Evolith.Ums.Domain.Enums;

public sealed class RecordAuditCommandHandler : ICommandHandler<RecordAuditCommand, RecordAuditResponse>
{
    private readonly IAuditRecordRepository _auditRecordRepository;
    private readonly IUserContext _userContext;

    public RecordAuditCommandHandler(IAuditRecordRepository auditRecordRepository, IUserContext userContext)
    {
        _auditRecordRepository = auditRecordRepository;
        _userContext = userContext;
    }

    [LoggerAspect(Type = typeof(IUmsLogger), LogDuration = true, LogException = true, LogArguments = [])]
    public async Task<Result<RecordAuditResponse>> Handle(
        RecordAuditCommand request,
        CancellationToken cancellationToken)
    {
        // G-040 (SEGURIDAD): no repudio. El actor y el inquilino se derivan del
        // contexto autenticado, NO del cuerpo de la petición (que es falsificable).
        if (!_userContext.IsAuthenticated || string.IsNullOrWhiteSpace(_userContext.UserId))
        {
            return Result<RecordAuditResponse>.Failure("Authenticated user is required.");
        }

        if (!Guid.TryParse(_userContext.UserId, out var actorId))
        {
            return Result<RecordAuditResponse>.Failure("Authenticated user identifier is invalid.");
        }

        if (!Guid.TryParse(_userContext.TenantId, out var tenantId))
        {
            return Result<RecordAuditResponse>.Failure("Authenticated tenant is required.");
        }

        var subjectType = DomainEnumerationParser.FromName<SubjectType>(request.SubjectType) ?? SubjectType.User;
        var auditResult = DomainEnumerationParser.FromName<AuditResult>(request.AuditResult) ?? AuditResult.Success;

        // G-040 (FR-072): desinfecta la metadata antes de persistirla — la traza es append-only e
        // inmutable (G-081), así que un secreto (hash/PIN/llave/token) filtrado aquí no se puede borrar.
        var sanitizedMetadata = AuditMetadataSanitizer.Sanitize(request.Metadata);

        var auditRecordResult = AuditRecord.Record(
            actorId,
            subjectType,
            request.WhatChanged,
            request.EventType,
            auditResult,
            request.AffectedEntityId,
            request.AffectedEntityType,
            tenantId,
            sanitizedMetadata);

        if (auditRecordResult.IsFailure)
        {
            return Result<RecordAuditResponse>.Failure(auditRecordResult.Error);
        }

        await _auditRecordRepository.AppendAsync(auditRecordResult.Value, cancellationToken);
        await _auditRecordRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        return Result<RecordAuditResponse>.Success(
            new RecordAuditResponse(auditRecordResult.Value.Props.Id.GetValue()));
    }
}
