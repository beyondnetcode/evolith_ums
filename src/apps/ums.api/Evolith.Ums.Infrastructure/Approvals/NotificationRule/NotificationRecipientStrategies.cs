using System.Net.Mail;
using System.Text.RegularExpressions;

namespace Evolith.Ums.Infrastructure.Approvals.NotificationRule;

internal abstract class NotificationRecipientStrategyBase : INotificationRecipientStrategy
{
    public abstract Result<string> Normalize(string recipient);

    protected static Result<string> Failure(string message) => Result<string>.Failure(message);
}

internal sealed class EmailNotificationRecipientStrategy : NotificationRecipientStrategyBase
{
    public override Result<string> Normalize(string recipient)
    {
        var normalized = recipient.Trim().ToLowerInvariant();

        try
        {
            _ = new MailAddress(normalized);
            return Result<string>.Success(normalized);
        }
        catch (FormatException)
        {
            return Failure("Email notification recipient is invalid.");
        }
    }
}

internal sealed class SmsNotificationRecipientStrategy : NotificationRecipientStrategyBase
{
    private static readonly Regex AllowedCharacters = new(@"^[\d\+\-\(\)\s]+$", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    public override Result<string> Normalize(string recipient)
    {
        var normalized = recipient.Trim();
        if (!AllowedCharacters.IsMatch(normalized))
        {
            return Failure("SMS notification recipient is invalid.");
        }

        var digits = new string(normalized.Where(char.IsDigit).ToArray());
        if (digits.Length < 8)
        {
            return Failure("SMS notification recipient is invalid.");
        }

        return Result<string>.Success(normalized.StartsWith('+') ? $"+{digits}" : digits);
    }
}

internal sealed class InAppNotificationRecipientStrategy : NotificationRecipientStrategyBase
{
    public override Result<string> Normalize(string recipient)
    {
        var normalized = recipient.Trim();
        return string.IsNullOrWhiteSpace(normalized)
            ? Failure("In-app notification recipient is required.")
            : Result<string>.Success(normalized);
    }
}
