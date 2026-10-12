using Evolith.Ums.Application.Approvals.DocumentType.DTOs;

namespace Evolith.Ums.Application.Approvals.DocumentType.Queries;

public sealed record GetDocumentTypeByIdQuery(Guid DocumentTypeId) : IQuery<DocumentTypeDto>;
