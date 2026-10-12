namespace Evolith.Ums.Application.Common;

public static class QueryDefaults
{
    public const int Page = 1;
    public const int PageSize = 20;
    public const int MaximumPageSize = 100;
    public const string AllStatuses = "all";
    public const string Ascending = "asc";
    public const string Descending = "desc";
}

public static class QueryRequestNormalizer
{
    public static int NormalizePage(int page) => page < QueryDefaults.Page ? QueryDefaults.Page : page;

    public static int NormalizePageSize(
        int pageSize,
        int min = 1,
        int max = QueryDefaults.MaximumPageSize)
        => Math.Clamp(pageSize, min, max);

    public static string NormalizeText(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    public static string? NormalizeSearch(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string? NormalizeStatusFilter(string? value)
        => string.IsNullOrWhiteSpace(value)
            || string.Equals(value.Trim(), QueryDefaults.AllStatuses, StringComparison.OrdinalIgnoreCase)
                ? null
                : value.Trim();

    public static string NormalizeSortOrder(string? value)
        => string.Equals(value?.Trim(), QueryDefaults.Descending, StringComparison.OrdinalIgnoreCase)
            ? QueryDefaults.Descending
            : QueryDefaults.Ascending;
}
