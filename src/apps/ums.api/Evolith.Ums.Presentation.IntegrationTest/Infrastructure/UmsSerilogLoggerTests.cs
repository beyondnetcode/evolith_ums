using System.Diagnostics;
using BeyondNetCode.Shell.Aop;
using Evolith.Ums.Infrastructure.Aop;
using Evolith.Ums.Infrastructure.Observability;
using Microsoft.Extensions.Logging;

namespace Evolith.Ums.Presentation.IntegrationTest.Infrastructure;

using MelILogger = Microsoft.Extensions.Logging.ILogger;
using MelILoggerProvider = Microsoft.Extensions.Logging.ILoggerProvider;

public sealed class UmsSerilogLoggerTests
{
    [Fact]
    public void OnEntry_ShouldEmitFullObservabilityEnvelope()
    {
        var provider = new CapturingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddProvider(provider);
        });

        using var activity = StartW3CActivity();
        var requestContext = new RequestContext();
        requestContext.SetSessionTrackingId("session-001");

        var logger = new UmsSerilogLogger(loggerFactory, new StubUserContext("tenant-001"), requestContext);

        logger.OnEntry(
            CreateJoinPoint(),
            [new Argument { Name = "request", Type = "CreateFeatureFlagCommand", Value = new object() }],
            requestId: "fallback-request-id");

        provider.Entries.Should().ContainSingle();
        var entry = provider.Entries.Single();

        entry.Level.Should().Be(LogLevel.Information);
        entry.Properties["TenantId"].Should().Be("tenant-001");
        entry.Properties["SessionTrackingId"].Should().Be("session-001");
        entry.Properties["TraceId"].Should().Be(activity.TraceId.ToString());
        entry.Properties["SpanId"].Should().Be(activity.SpanId.ToString());
        // Unificación W3C: la correlación es el trace_id (no un GUID propio).
        entry.Properties["CorrelationId"].Should().Be(activity.TraceId.ToString());
        entry.Properties["BoundedContext"].Should().Be("Configuration");
    }

    [Fact]
    public void OnExit_ShouldKeepSessionAndTraceFields()
    {
        var provider = new CapturingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddProvider(provider);
        });

        using var activity = StartW3CActivity();
        var requestContext = new RequestContext();
        requestContext.SetSessionTrackingId("session-002");

        var logger = new UmsSerilogLogger(loggerFactory, new StubUserContext("tenant-002"), requestContext);

        logger.OnExit(CreateJoinPoint(), requestId: string.Empty, duration: 42L);

        provider.Entries.Should().ContainSingle();
        var entry = provider.Entries.Single();

        entry.Properties["TenantId"].Should().Be("tenant-002");
        entry.Properties["SessionTrackingId"].Should().Be("session-002");
        entry.Properties["TraceId"].Should().Be(activity.TraceId.ToString());
        entry.Properties["SpanId"].Should().Be(activity.SpanId.ToString());
        entry.Properties["CorrelationId"].Should().Be(activity.TraceId.ToString());
    }

    private static Activity StartW3CActivity()
    {
        Activity.DefaultIdFormat = ActivityIdFormat.W3C;
        Activity.ForceDefaultIdFormat = true;
        var activity = new Activity("test-request");
        activity.SetIdFormat(ActivityIdFormat.W3C);
        activity.Start();
        return activity;
    }

    private static IJoinPoint CreateJoinPoint()
        => new JoinPoint
        {
            TargetObject = new Evolith.Ums.Application.Configuration.FeatureFlag.Commands.FakeFeatureFlagHandler(),
            TargetType = typeof(Evolith.Ums.Application.Configuration.FeatureFlag.Commands.FakeFeatureFlagHandler),
            MethodInfo = typeof(Evolith.Ums.Application.Configuration.FeatureFlag.Commands.FakeFeatureFlagHandler)
                .GetMethod(nameof(Evolith.Ums.Application.Configuration.FeatureFlag.Commands.FakeFeatureFlagHandler.Handle))!,
            Arguments = [],
        };

    private sealed record CapturedLogEntry(
        LogLevel Level,
        string Message,
        Dictionary<string, object?> Properties,
        Exception? Exception);

    private sealed class CapturingLoggerProvider : MelILoggerProvider
    {
        public List<CapturedLogEntry> Entries { get; } = [];

        public MelILogger CreateLogger(string categoryName) => new CapturingLogger(Entries);

        public void Dispose()
        {
        }
    }

    private sealed class CapturingLogger(List<CapturedLogEntry> entries) : MelILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var properties = state is IEnumerable<KeyValuePair<string, object?>> keyValuePairs
                ? keyValuePairs.ToDictionary(pair => pair.Key, pair => pair.Value)
                : new Dictionary<string, object?>();

            entries.Add(new CapturedLogEntry(logLevel, formatter(state, exception), properties, exception));
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }

    private sealed class StubUserContext(string? tenantId) : IUserContext
    {
        public string? UserId => "test-user";

        public string? UserName => "Test User";

        public string? TenantId => tenantId;

        public bool IsAuthenticated => true;

        public bool HasPermission(string permission) => true;
    }
}
