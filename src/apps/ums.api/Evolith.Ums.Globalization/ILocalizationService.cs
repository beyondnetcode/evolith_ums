using Evolith.Ums.Globalization.Core;

namespace Evolith.Ums.Globalization;

public interface ILocalizationService
{
    string T(string key);
    string T(string key, LanguageCode language);
    LocalizedMessage Localize(string key);
    LocalizedMessage Localize(string key, LanguageCode language);
}
