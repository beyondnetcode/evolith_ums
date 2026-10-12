using BeyondNetCode.Shell.Factory.Impl;
using Microsoft.Extensions.Logging;

namespace Evolith.Ums.Infrastructure.Aop;

/// <summary>
/// Intercepts Shell.Factory execution to provide observability (logging/metrics).
/// Since this uses standard ILogger, it hooks into Serilog (and thus Loki/OTel) automatically.
/// </summary>
public sealed class FactoryLoggingInterceptor : AbstractFactoryInterceptor
{
    private readonly ILogger<FactoryLoggingInterceptor> _logger;

    public FactoryLoggingInterceptor(ILogger<FactoryLoggingInterceptor> logger)
    {
        _logger = logger;
    }

    public override void OnEntry<TTarget>(TTarget target, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            _logger.LogDebug("Factory.Create resolving strategies for Target={TargetType}", typeof(TTarget).Name);
        else
            _logger.LogDebug("Factory.Create resolving named group '{GroupName}' for Target={TargetType}", name, typeof(TTarget).Name);
    }

    public override void OnSuccess<TTarget, TService>(TTarget target, string name, IList<TService> services)
    {
        _logger.LogDebug("Factory successfully resolved {Count} implementations of {ServiceType} for Target={TargetType}",
            services.Count, typeof(TService).Name, typeof(TTarget).Name);
    }

    public override void OnError<TTarget, TService>(TTarget target, string name, IList<TService> services, Exception exception)
    {
        _logger.LogError(exception, "Factory resolution failed for Service={ServiceType}, Target={TargetType}, Group={GroupName}",
            typeof(TService).Name, typeof(TTarget).Name, name ?? "default");
    }

    public override void OnExit<TTarget, TService>(TTarget target, string name, IList<TService> services)
    {
        // Optional: emit metrics or stop a stopwatch here.
    }
}
