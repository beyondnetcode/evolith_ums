namespace Evolith.Ums.Application.Approvals.UserDocument.Commands;

using FluentValidation;

public sealed class UploadUserDocumentCommandValidator : AbstractValidator<UploadUserDocumentCommand>
{
    public UploadUserDocumentCommandValidator()
    {
        RuleFor(c => c.UserId).NotEmpty();
        RuleFor(c => c.DocumentTypeId).NotEmpty();
        RuleFor(c => c.IssueDate).NotEmpty();
        RuleFor(c => c.ExpirationDate).NotEmpty();
        // G-045: valida el nombre del enum para evitar un NullReference → 500 cuando el
        // handler parsea un valor no vacío pero inválido con `!`.
        RuleFor(c => c.Criticity)
            .NotEmpty()
            .Must(criticity => DomainEnumerationParser.FromName<DocumentCriticity>(criticity) is not null)
            .WithMessage("Document criticity is not supported.");
        RuleFor(c => c.FileStoragePath).NotEmpty().MaximumLength(500);
        RuleFor(c => c.FileChecksum).NotEmpty().MaximumLength(128);
    }
}
