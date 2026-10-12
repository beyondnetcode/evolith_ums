namespace Evolith.Ums.Globalization.Core;

public enum LanguageCode
{
    En = 0,
    Es = 1
}

public static class LanguageCodeExtensions
{
    public static string ToCultureString(this LanguageCode code)
    {
        return code switch
        {
            LanguageCode.En => "en",
            LanguageCode.Es => "es",
            _ => "en"
        };
    }

    public static LanguageCode FromCultureString(string culture)
    {
        return culture[..2].ToLowerInvariant() switch
        {
            "en" => LanguageCode.En,
            "es" => LanguageCode.Es,
            _ => LanguageCode.En
        };
    }
}
