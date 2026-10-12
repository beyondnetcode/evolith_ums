namespace Evolith.Ums.Application.Approvals.DocumentType.Commands;
public sealed class UpdateDocumentTypeCommandValidator : AbstractValidator<UpdateDocumentTypeCommand>
{
    public UpdateDocumentTypeCommandValidator() { RuleFor(x => x.DocumentTypeId).NotEmpty(); RuleFor(x => x.Name).NotEmpty().MaximumLength(200); RuleFor(x => x.Description).NotEmpty().MaximumLength(500); }
}
