namespace Evolith.Ums.Domain.Approvals;

using Evolith.Ums.Domain.Enums;
using ApprovalWorkflowAggregate = Evolith.Ums.Domain.Approvals.ApprovalWorkflow.ApprovalWorkflow;
using UserDocumentAggregate = Evolith.Ums.Domain.Approvals.UserDocument.UserDocument;

/// <summary>
/// Politica de dominio (pura, sin E/S) que hace cumplir el checklist de documentos
/// requeridos de un <see cref="ApprovalWorkflowAggregate"/> contra los documentos del
/// usuario objetivo al momento de aprobar una solicitud (G-051 F4).
///
/// Es una regla de dominio cross-agregado de SOLO LECTURA: cruza el checklist declarado
/// por el workflow con el estado de los <see cref="UserDocumentAggregate"/> del usuario.
/// No muta ningun agregado, por lo que no introduce una excepcion a D-016; el handler
/// que la invoca sigue mutando un unico agregado de negocio (ApprovalRequest) en su
/// transaccion. La lectura precede a la transicion y es fail-closed.
///
/// Semantica fail-closed: si un tipo de documento marcado como obligatorio no cuenta con
/// un UserDocument en estado <see cref="DocumentStatus.Valid"/> para el usuario objetivo,
/// la evaluacion falla y la aprobacion no puede proceder.
/// </summary>
public static class RequiredDocumentChecklist
{
    /// <summary>
    /// Evalua la completitud del checklist obligatorio. Devuelve <c>Result.Success</c> solo
    /// cuando cada documento obligatorio del workflow tiene un UserDocument valido del usuario.
    /// </summary>
    /// <param name="workflow">Workflow que declara el checklist de documentos requeridos.</param>
    /// <param name="targetUserDocuments">Documentos del usuario objetivo de la solicitud.</param>
    public static Result Evaluate(
        ApprovalWorkflowAggregate workflow,
        IReadOnlyCollection<UserDocumentAggregate> targetUserDocuments)
    {
        var validDocumentTypeIds = targetUserDocuments
            .Where(document => document.Status == DocumentStatus.Valid)
            .Select(document => document.DocumentTypeId.GetValue())
            .ToHashSet();

        var hasMissingMandatoryDocument = workflow.RequiredDocuments
            .Where(required => required.IsMandatory)
            .Any(required => !validDocumentTypeIds.Contains(required.DocumentTypeId.GetValue()));

        return hasMissingMandatoryDocument
            ? Result.Failure(DomainErrors.Approvals.RequiredDocumentsIncomplete)
            : Result.Success();
    }
}
