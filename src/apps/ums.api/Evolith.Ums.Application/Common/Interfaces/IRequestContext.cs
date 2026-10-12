namespace Evolith.Ums.Application.Common.Interfaces;

/// <summary>
/// Domain-agnostic port that exposes request execution correlation data to the application layer.
/// </summary>
public interface IRequestContext
{
    string? SessionTrackingId { get; }

    string? CorrelationId { get; }

    string? TraceId { get; }

    string? SpanId { get; }

    /// <summary>
    /// ADR-0076 D2: IANA timezone identifier sent by the client via X-Timezone header.
    /// Null when the header is absent (e.g. system-to-system calls without a browser session).
    /// </summary>
    string? ClientTimezone { get; }
}
