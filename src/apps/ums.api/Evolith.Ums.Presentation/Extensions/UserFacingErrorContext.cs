namespace Evolith.Ums.Presentation.Extensions;

using Microsoft.AspNetCore.Http;

internal static class UserFacingErrorContext
{
    public const string ErrorIdItemKey = "Evolith.Ums.ErrorId";
    public const string ErrorIdHeader = "X-Error-Id";

    public static string GetOrCreateErrorId(HttpContext context)
    {
        if (context.Items.TryGetValue(ErrorIdItemKey, out var existing)
            && existing is string errorId
            && !string.IsNullOrWhiteSpace(errorId))
        {
            return errorId;
        }

        var newErrorId = Guid.NewGuid().ToString("D");
        context.Items[ErrorIdItemKey] = newErrorId;
        context.Response.Headers[ErrorIdHeader] = newErrorId;
        return newErrorId;
    }

    public static string? GetErrorId(HttpContext context)
        => context.Items.TryGetValue(ErrorIdItemKey, out var value) ? value as string : null;
}
