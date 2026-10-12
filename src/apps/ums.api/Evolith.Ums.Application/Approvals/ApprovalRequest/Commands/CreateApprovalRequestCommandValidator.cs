namespace Evolith.Ums.Application.Approvals.ApprovalRequest.Commands;

using FluentValidation;

public sealed class CreateApprovalRequestCommandValidator : AbstractValidator<CreateApprovalRequestCommand>
{
    public CreateApprovalRequestCommandValidator()
    {
        RuleFor(c => c.WorkflowId).NotEmpty();
        RuleFor(c => c.TargetUserId).NotEmpty();
        RuleFor(c => c.RequestedSystemId).NotEmpty();
        RuleFor(c => c.RequestedRoleId).NotEmpty();
        RuleFor(c => c.Justification).MaximumLength(1000).When(c => c.Justification is not null);
    }
}
