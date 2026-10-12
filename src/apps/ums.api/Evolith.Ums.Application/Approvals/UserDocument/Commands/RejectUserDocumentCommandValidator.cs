namespace Evolith.Ums.Application.Approvals.UserDocument.Commands;
public sealed class RejectUserDocumentCommandValidator : AbstractValidator<RejectUserDocumentCommand>
{
    public RejectUserDocumentCommandValidator() { RuleFor(x => x.UserDocumentId).NotEmpty(); RuleFor(x => x.RejectionReason).NotEmpty().MaximumLength(500); }
}
