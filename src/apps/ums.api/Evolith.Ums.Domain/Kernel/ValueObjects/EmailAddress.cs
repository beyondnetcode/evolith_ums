namespace Evolith.Ums.Domain.Kernel.ValueObjects;

public class EmailAddress : StringValueObject
{
    private EmailAddress(string value) : base(value)
    {
    }

    public static Result<EmailAddress> Create(string value)
    {
        var email = new EmailAddress(value.Trim().ToLowerInvariant());
        if (!email.IsValid)
        {
            return Result<EmailAddress>.Failure(email.BrokenRules.GetBrokenRules()[0].Message);
        }
        return Result<EmailAddress>.Success(email);
    }

    public static EmailAddress Default()
    {
        return new EmailAddress(string.Empty);
    }

    public override void AddValidators()
    {
        base.AddValidators();
        ValidatorRules.Add(new EmailAddressValidator(this));
    }
}
