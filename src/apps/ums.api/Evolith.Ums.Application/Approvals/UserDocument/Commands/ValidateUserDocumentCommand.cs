using Evolith.Ums.Application.Approvals.UserDocument.DTOs;

namespace Evolith.Ums.Application.Approvals.UserDocument.Commands;

public sealed record ValidateUserDocumentCommand(Guid UserDocumentId) : ICommand;
