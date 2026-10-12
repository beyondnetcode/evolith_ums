namespace Evolith.Ums.Application.Approvals.DocumentType.Commands;
public sealed record UpdateDocumentTypeCommand(Guid DocumentTypeId, string Name, string Description) : ICommand;
