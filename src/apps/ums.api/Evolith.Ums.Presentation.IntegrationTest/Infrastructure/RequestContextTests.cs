using System.Diagnostics;
using Evolith.Ums.Infrastructure.Observability;

namespace Evolith.Ums.Presentation.IntegrationTest.Infrastructure;

/// <summary>
/// Verifica el <see cref="RequestContext"/> tras la unificación W3C (ADR-0046): TraceId/SpanId
/// se derivan de <see cref="Activity.Current"/> y la correlación es el trace_id (no un GUID propio).
/// </summary>
public sealed class RequestContextTests
{
    [Fact]
    public void SetSessionTrackingId_ShouldExposeThroughProperty()
    {
        var context = new RequestContext();

        context.SetSessionTrackingId("session-123");

        context.SessionTrackingId.Should().Be("session-123");
    }

    [Fact]
    public void SetSessionTrackingId_WithWhitespace_ShouldReturnNull()
    {
        var context = new RequestContext();

        context.SetSessionTrackingId("   ");

        context.SessionTrackingId.Should().BeNull();
    }

    [Fact]
    public void TraceAndSpan_ShouldDeriveFromCurrentW3CActivity()
    {
        var context = new RequestContext();

        Activity.DefaultIdFormat = ActivityIdFormat.W3C;
        Activity.ForceDefaultIdFormat = true;
        using var activity = new Activity("test-request");
        activity.SetIdFormat(ActivityIdFormat.W3C);
        activity.Start();

        context.TraceId.Should().Be(activity.TraceId.ToString());
        context.SpanId.Should().Be(activity.SpanId.ToString());
        // Unificación W3C: la correlación es el trace_id.
        context.CorrelationId.Should().Be(activity.TraceId.ToString());
    }

    [Fact]
    public void TraceAndSpan_WithoutActivity_ShouldReturnNull()
    {
        var context = new RequestContext();

        // Sin Activity actual no hay contexto de traza W3C.
        Activity.Current = null;

        context.TraceId.Should().BeNull();
        context.SpanId.Should().BeNull();
        context.CorrelationId.Should().BeNull();
    }

    [Fact]
    public void SetClientTimezone_ShouldExposeThroughClientTimezoneProperty()
    {
        var context = new RequestContext();

        context.SetClientTimezone("America/Lima");

        context.ClientTimezone.Should().Be("America/Lima");
    }

    [Fact]
    public void SetClientTimezone_WithNull_ShouldReturnNull()
    {
        var context = new RequestContext();
        context.SetClientTimezone("America/Lima");

        context.SetClientTimezone(null);

        context.ClientTimezone.Should().BeNull();
    }
}
