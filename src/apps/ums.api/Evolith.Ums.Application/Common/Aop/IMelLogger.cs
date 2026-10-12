
namespace Evolith.Ums.Application.Common.Aop;

/// <summary>
/// Marker interface used to select the Microsoft.Extensions.Logging adapter
/// when decorating handlers with <see cref="LoggerAspectAttribute"/>.
///
/// The concrete implementation (<c>MelLogger</c>) lives in <c>Evolith.Ums.Infrastructure</c>
/// and is registered in DI with this interface as the keyed-service key, keeping the
/// Application layer free of any Infrastructure dependency.
///
/// Usage on a command handler:
/// <code>
/// [LoggerAspect(Type = typeof(IMelLogger), LogDuration = true, LogException = true)]
/// public async Task&lt;Result&lt;T&gt;&gt; Handle(...) { ... }
/// </code>
/// </summary>
public interface IMelLogger : ILogger;
