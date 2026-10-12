namespace Evolith.Ums.Application.Approvals.DocumentType.Commands;

using FluentValidation;

public sealed class CreateDocumentTypeCommandValidator : AbstractValidator<CreateDocumentTypeCommand>
{
    public CreateDocumentTypeCommandValidator()
    {
        RuleFor(c => c.TenantId).NotEmpty();
        RuleFor(c => c.Code).NotEmpty().MaximumLength(50);
        RuleFor(c => c.Name).NotEmpty().MaximumLength(150);
        RuleFor(c => c.Description).NotEmpty().MaximumLength(500);
        // G-045: sin validar el nombre del enum, un valor no vacío pero inválido pasa la
        // validación y el handler lo parsea con `!`, produciendo un NullReference → 500.
        // Validamos aquí para devolver 400/422 con detalle en vez de un 500 inesperado.
        RuleFor(c => c.Criticity)
            .NotEmpty()
            .Must(criticity => DomainEnumerationParser.FromName<DocumentCriticity>(criticity) is not null)
            .WithMessage("Document criticity is not supported.");
    }
}
