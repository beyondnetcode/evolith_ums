namespace Evolith.Ums.Application.Approvals.DocumentType.DTOs;

public sealed record DocumentTypeDto(
    Guid DocumentTypeId,
    Guid TenantId,
    string Code,
    string Name,
    string Description,
    string Criticity);
