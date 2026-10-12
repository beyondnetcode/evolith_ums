namespace Evolith.Ums.Domain.Configuration.Parameter;

using System.Globalization;
using System.Text.Json;
using Evolith.Ums.Domain.Configuration.Parameter.ValueObjects;

internal static class ParameterValueValidation
{
    public static bool IsValid(string? value, ParameterDataType dataType)
    {
        var normalized = value ?? string.Empty;

        return dataType.Id switch
        {
            1 => true,
            2 => decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out _)
                || decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.CurrentCulture, out _),
            3 => bool.TryParse(normalized, out _),
            4 => IsValidJson(normalized),
            _ => false
        };
    }

    private static bool IsValidJson(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            using var _ = JsonDocument.Parse(value);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
