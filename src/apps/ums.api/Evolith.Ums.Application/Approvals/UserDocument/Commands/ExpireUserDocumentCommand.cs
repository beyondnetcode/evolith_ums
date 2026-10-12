namespace Evolith.Ums.Application.Approvals.UserDocument.Commands;
public sealed record ExpireUserDocumentCommand(Guid UserDocumentId) : ICommand;
