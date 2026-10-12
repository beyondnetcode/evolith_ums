namespace Evolith.Ums.Application.Approvals.AccessEnforcementPolicy.Commands;

using FluentValidation;

public sealed class CreateAccessEnforcementPolicyCommandValidator : AbstractValidator<CreateAccessEnforcementPolicyCommand>
{
    public CreateAccessEnforcementPolicyCommandValidator()
    {
        RuleFor(c => c.TenantId).NotEmpty();
        // G-045: valida el nombre del enum para evitar un NullReference → 500 cuando el
        // handler parsea un valor no vacío pero inválido con `!`.
        RuleFor(c => c.EnforcementAction)
            .NotEmpty()
            .Must(action => DomainEnumerationParser.FromName<AccessEnforcementAction>(action) is not null)
            .WithMessage("Enforcement action is not supported.");
        RuleFor(c => c).Must(c => c.ProfileId.HasValue || c.RoleId.HasValue)
            .WithMessage("Either ProfileId or RoleId must be provided.");
        // G-120 (FR-053): periodo de gracia no negativo (0 = enforcement inmediato).
        RuleFor(c => c.GracePeriodDays).GreaterThanOrEqualTo(0)
            .WithMessage("Grace period days must be zero or greater.");
    }
}
