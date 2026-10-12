using System.Text.RegularExpressions;

namespace Evolith.Ums.Domain.Kernel.ValueObjects;

public class EmailAddressValidator : AbstractRuleValidator<ValueObject<string>>
{
    private static readonly Regex EmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        TimeSpan.FromSeconds(1));

    public EmailAddressValidator(ValueObject<string> subject) : base(subject) { }

    public override void AddRules(RuleContext? context)
    {
        var value = Subject.GetValue();
        if (string.IsNullOrWhiteSpace(value) || !EmailRegex.IsMatch(value))
        {
            AddBrokenRule("EmailAddress", DomainErrors.ValueObject.EmailRequired);
        }
    }
}
