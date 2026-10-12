namespace Evolith.Ums.Application.Approvals.UserDocument.Commands;
public sealed class ReUploadUserDocumentCommandValidator : AbstractValidator<ReUploadUserDocumentCommand>
{
    public ReUploadUserDocumentCommandValidator()
    {
        RuleFor(x => x.UserDocumentId).NotEmpty();
        RuleFor(x => x.NewIssueDate).NotEmpty();
        RuleFor(x => x.NewExpirationDate).GreaterThan(x => x.NewIssueDate).WithMessage("Expiration date must be after issue date.");
        RuleFor(x => x.NewFileStoragePath).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.NewFileChecksum).NotEmpty().MaximumLength(128);
    }
}
