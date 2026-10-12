using Evolith.Ums.Globalization.Access;
using Evolith.Ums.Globalization.Core;

namespace Evolith.Ums.Globalization;

public class LocalizationService : ILocalizationService
{
    public string T(string key)
    {
        return StringLocalizer.T(key);
    }

    public string T(string key, LanguageCode language)
    {
        return StringLocalizer.T(key, language.ToCultureString());
    }

    public LocalizedMessage Localize(string key)
    {
        return StringLocalizer.Localize(key);
    }

    public LocalizedMessage Localize(string key, LanguageCode language)
    {
        return StringLocalizer.Localize(key, language.ToCultureString());
    }
}
