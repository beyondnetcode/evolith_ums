namespace Evolith.Ums.Application.Approvals.ApprovalWorkflow.DTOs;

public sealed record ApprovalWorkflowDto(
    Guid ApprovalWorkflowId,
    Guid TenantId,
    Guid? SystemSuiteId,
    string Code,
    string Name,
    string Description,
    string TargetUserCategory,
    bool RequiresApproval,
    // WF2/WF3 (G-118): proyecta el checklist de documentos requeridos con su id, para que sea
    // legible (antes write-only) y que un cliente pueda obtener el id que necesita el DELETE.
    IReadOnlyList<RequiredDocumentDto> RequiredDocuments);

public sealed record RequiredDocumentDto(
    Guid RequiredDocumentId,
    Guid DocumentTypeId,
    bool IsMandatory);
