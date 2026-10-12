namespace Evolith.Ums.Application.Approvals.UserDocument.Commands;

public sealed record RecordEnforcementExecutedCommand(
    Guid UserDocumentId,
    string Action) : ICommand;
