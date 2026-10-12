namespace Evolith.Ums.Presentation.Extensions;

using System.Text.Json;

/// <summary>
/// Shared JSON serialization options for all RFC 7807 Problem Details responses.
/// Centralised here so every handler (GlobalExceptionHandler, ResultExtensions,
/// rate-limiter, etc.) emits identical casing and formatting.
/// </summary>
internal static class UmsProblemDetailsJsonOptions
{
    public static readonly JsonSerializerOptions Instance = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };
}
