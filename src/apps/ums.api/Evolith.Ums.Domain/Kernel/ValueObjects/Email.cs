namespace Evolith.Ums.Domain.Kernel.ValueObjects;

using global::System.Text.RegularExpressions;

public partial class Email : StringValueObject
{
    private Email(string value) : base(value) { }

    public static Email Create(string value) => new Email(value?.Trim().ToLowerInvariant() ?? string.Empty);

    public override void AddValidators()
    {
        base.AddValidators();
        ValidatorRules.Add(new EmailRuleValidator(this));
    }
}

public partial class EmailRuleValidator : AbstractRuleValidator<ValueObject<string>>
{
    private static readonly Regex EmailPattern = EmailRegex();

    public EmailRuleValidator(ValueObject<string> subject) : base(subject) { }

    public override void AddRules(BeyondNetCode.Shell.Ddd.Rules.RuleContext? context)
    {
        var value = Subject.GetValue();

        if (string.IsNullOrWhiteSpace(value))
        {
            AddBrokenRule(nameof(Email), DomainErrors.ValueObject.PropertyRequired);
            return;
        }

        if (!EmailPattern.IsMatch(value))
        {
            AddBrokenRule(nameof(Email), DomainErrors.UserAccount.InvalidEmail);
        }
    }

    [GeneratedRegex(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$")]
    private static partial Regex EmailRegex();
}
