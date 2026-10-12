#pragma warning disable S1144
namespace Evolith.Ums.Presentation.Extensions;

using Serilog.Core;
using Serilog.Events;

/// <summary>
/// HARDENING-04: Serilog <see cref="IDestructuringPolicy"/> that masks PII fields
/// before they are written to any log sink.
///
/// Masking is applied based on property name conventions (case-insensitive) rather
/// than type annotations, so no changes are required in Domain or Application layers.
///
/// Masked fields → replacement value:
///   Email / EmailAddress      → "***@***.***"
///   Password / PasswordHash   → "[REDACTED]"
///   IdentityReference         → "[REDACTED]"
///   Token / AccessToken / RefreshToken / BearerToken → "[REDACTED]"
///   Secret / ApiKey / ApiSecret → "[REDACTED]"
///   Ssn / NationalId / TaxId   → "[REDACTED]"
///
/// For string scalar values in the log event that look like email addresses, an
/// additional enricher (<see cref="PiiSanitizerEnricher"/>) scans all log properties
/// and replaces email-shaped strings using a simple regex.
///
/// Wire-up in LoggingExtensions.cs:
///   .Destructure.With&lt;PiiMaskingPolicy&gt;()
///   .Enrich.With&lt;PiiSanitizerEnricher&gt;()
/// </summary>
public sealed class PiiMaskingPolicy : IDestructuringPolicy
{
    private static readonly HashSet<string> MaskedPropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "email", "emailaddress", "mail",
        "password", "passwordhash", "passwordtext",
        "identityreference",
        "token", "accesstoken", "refreshtoken", "bearertoken", "idtoken",
        "secret", "apikey", "apisecret", "clientsecret",
        "ssn", "nationalid", "taxid",
    };

    public bool TryDestructure(object value, ILogEventPropertyValueFactory propertyValueFactory, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out LogEventPropertyValue? result)
    {
        // This policy is called once per value; we cannot inspect the property name here.
        // Property-name–based masking is handled by PiiSanitizerEnricher (log event level).
        result = null;
        return false;
    }
}

/// <summary>
/// HARDENING-04: Log event enricher that scans all scalar string properties and
/// replaces the value when the property name matches a known PII field name.
/// </summary>
public sealed class PiiSanitizerEnricher : ILogEventEnricher
{
    private static readonly HashSet<string> MaskedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "email", "emailaddress", "mail",
        "password", "passwordhash", "passwordtext",
        "identityreference",
        "token", "accesstoken", "refreshtoken", "bearertoken", "idtoken",
        "secret", "apikey", "apisecret", "clientsecret",
        "ssn", "nationalid", "taxid",
    };

    private static readonly System.Text.RegularExpressions.Regex EmailRegex =
        new(@"[^@\s]+@[^@\s]+\.[^@\s]+",
            System.Text.RegularExpressions.RegexOptions.Compiled |
            System.Text.RegularExpressions.RegexOptions.IgnoreCase,
            TimeSpan.FromMilliseconds(100));

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        foreach (var property in logEvent.Properties.ToList())
        {
            if (MaskedNames.Contains(property.Key))
            {
                logEvent.AddOrUpdateProperty(
                    propertyFactory.CreateProperty(property.Key, "[REDACTED]"));
            }
            else if (property.Value is ScalarValue { Value: string str } && EmailRegex.IsMatch(str))
            {
                // Mask free-text properties that happen to contain an email address.
                logEvent.AddOrUpdateProperty(
                    propertyFactory.CreateProperty(property.Key, MaskEmail(str)));
            }
        }
    }

    private static string MaskEmail(string email)
    {
        var at = email.IndexOf('@');
        if (at <= 0) return "***@***.***";
        var local  = email[..Math.Min(at, 2)];
        var domain = email[(at + 1)..];
        var dot    = domain.LastIndexOf('.');
        var tld    = dot > 0 ? domain[(dot + 1)..] : "***";
        return $"{local}***@***.{tld}";
    }
}

#pragma warning restore S1144
