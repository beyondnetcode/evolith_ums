namespace Evolith.Ums.Presentation.Extensions;

/// <summary>
/// REC-10: Utility for converting SQL Server RowVersion (byte[8]) to and from the
/// RFC 7232 ETag / If-Match header format.
///
/// Format: <c>"&lt;base64-encoded-rowversion&gt;"</c>  (with surrounding double-quotes per RFC 7232)
///
/// Usage (GET endpoint — set ETag):
/// <code>
///   if (dto.RowVersion is { } rv)
///       context.Response.Headers.ETag = ETagHelper.Encode(rv);
/// </code>
///
/// Usage (PUT endpoint — read If-Match):
/// <code>
///   var rowVersion = ETagHelper.DecodeIfMatch(context.Request.Headers.IfMatch);
/// </code>
/// </summary>
public static class ETagHelper
{
    /// <summary>
    /// Encodes a <see cref="byte"/> array to a quoted base64 ETag value.
    /// Returns null when <paramref name="rowVersion"/> is null or empty.
    /// </summary>
    public static string? Encode(byte[]? rowVersion)
    {
        if (rowVersion is not { Length: > 0 }) return null;
        return $"\"{Convert.ToBase64String(rowVersion)}\"";
    }

    /// <summary>
    /// Decodes the first value in an <c>If-Match</c> header to a <see cref="byte"/> array.
    /// Strips surrounding double-quotes (RFC 7232 strong ETag format).
    /// Returns null when the header is absent, <c>*</c>, or malformed.
    /// </summary>
    public static byte[]? DecodeIfMatch(Microsoft.Extensions.Primitives.StringValues ifMatchHeader)
    {
        var raw = (string?)ifMatchHeader;
        if (string.IsNullOrWhiteSpace(raw) || raw == "*") return null;

        // Strip surrounding quotes
        var stripped = raw.Trim().Trim('"');

        try
        {
            return Convert.FromBase64String(stripped);
        }
        catch (FormatException)
        {
            return null; // Malformed ETag — treat as absent
        }
    }
}
