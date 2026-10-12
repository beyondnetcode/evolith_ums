namespace Evolith.Ums.Application.Common;

public static class QueryRequestNormalizer
{
    public static int NormalizePage(int page) => page < 1 ? 1 : page;

    public static int NormalizePageSize(int pageSize, int min = 1, int max = 100)
        => Math.Clamp(pageSize, min, max);

    public static string NormalizeText(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    public static string? NormalizeSearch(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
