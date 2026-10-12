namespace Evolith.Ums.Application.Identity.UserAccount.Commands;

public sealed class BlockUserAccountCommandValidator : AbstractValidator<BlockUserAccountCommand>
{
    public BlockUserAccountCommandValidator()
    {
        RuleFor(x => x.UserAccountId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}
