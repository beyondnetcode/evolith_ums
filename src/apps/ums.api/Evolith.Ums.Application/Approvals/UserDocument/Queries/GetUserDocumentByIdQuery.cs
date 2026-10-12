using Evolith.Ums.Application.Approvals.UserDocument.DTOs;

namespace Evolith.Ums.Application.Approvals.UserDocument.Queries;

public sealed record GetUserDocumentByIdQuery(Guid UserDocumentId) : IQuery<UserDocumentDto>;
