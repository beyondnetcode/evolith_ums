namespace Evolith.Ums.Infrastructure.Configuration;

using System.Collections.Concurrent;
using System.Text.Json;
using Evolith.Ums.Application.Configuration.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using AppConfigurationAggregate = Evolith.Ums.Domain.Configuration.AppConfiguration.AppConfiguration;

/// <summary>
/// Hybrid implementation of <see cref="IConfigurationCache"/> backed by Redis Pub/Sub (G-069).
///
/// Provides lightning-fast synchronous reads using local memory dictionaries (0 network latency),
/// while ensuring cross-pod consistency by subscribing to Redis Pub/Sub invalidation channels.
/// When a configuration is populated or invalidated, an event is broadcasted so other pods
/// drop their local cache and reload from the database.
/// </summary>
public sealed class RedisConfigurationCache : IConfigurationCache, IDisposable
{
    private readonly ConcurrentDictionary<string, AppConfigurationAggregate> _global
        = new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, AppConfigurationAggregate>> _tenant = new();
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, AppConfigurationAggregate>> _suite = new();
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, AppConfigurationAggregate>> _module = new();

    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisConfigurationCache> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private ISubscriber? _subscriber;

    private const string ChannelPrefix = "ums:config:invalidate";

    /// <summary>
    /// Identidad de ESTA instancia. Viaja en cada mensaje publicado para que un pod
    /// descarte sus propios avisos: la suscripción es por patrón, así que sin esto cada
    /// pod se escucha a sí mismo (G-170).
    /// </summary>
    private static readonly string OriginId = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Marca «estoy aplicando una invalidación que llegó de otro pod». Mientras está activa,
    /// <see cref="PublishInvalidation"/> no publica.
    ///
    /// Es la pieza que rompe el ciclo: el manejador de la suscripción llama a
    /// <c>ReloadAsync</c>/<c>ReloadTenantAsync</c>, que a su vez invocan
    /// <see cref="InvalidateAll"/>/<see cref="InvalidateTenant"/>. Sin esta marca, aplicar un
    /// aviso genera otro aviso, y con N réplicas el tráfico se autoamplifica hasta la tormenta
    /// (G-170). <see cref="AsyncLocal{T}"/> y no un campo: el manejador es asíncrono y la marca
    /// debe seguir al flujo lógico, no al hilo.
    /// </summary>
    private static readonly AsyncLocal<bool> AplicandoRemoto = new();

    public RedisConfigurationCache(
        IConnectionMultiplexer redis,
        ILogger<RedisConfigurationCache> logger,
        IServiceScopeFactory scopeFactory)
    {
        _redis = redis;
        _logger = logger;
        _scopeFactory = scopeFactory;

        SubscribeToInvalidations();
    }

    private void SubscribeToInvalidations()
    {
        _subscriber = _redis.GetSubscriber();
        _subscriber.Subscribe(new RedisChannel($"{ChannelPrefix}:*", RedisChannel.PatternMode.Pattern), async (channel, message) =>
        {
            try
            {
                var channelName = (string)channel!;
                var (origen, payload) = DesempaquetarMensaje((string)message!);

                // Un pod no se recarga por su propio aviso.
                if (origen == OriginId)
                {
                    return;
                }

                using var scope = _scopeFactory.CreateScope();
                var provider = scope.ServiceProvider.GetRequiredService<IConfigurationProvider>();

                AplicandoRemoto.Value = true;
                try
                {
                    if (channelName.EndsWith(":all"))
                    {
                        _logger.LogInformation("Received Redis invalidation for ALL configurations. Reloading...");
                        await provider.ReloadAsync();
                    }
                    else if (channelName.EndsWith(":tenant") && Guid.TryParse(payload, out var tenantId))
                    {
                        _logger.LogInformation("Received Redis invalidation for Tenant {TenantId}. Reloading...", tenantId);
                        await provider.ReloadTenantAsync(tenantId);
                    }
                }
                finally
                {
                    AplicandoRemoto.Value = false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Redis configuration invalidation message.");
            }
        });
    }

    private void PublishInvalidation(string suffix, string payload = "")
    {
        // Aplicar un aviso ajeno NO genera un aviso nuevo (G-170).
        if (AplicandoRemoto.Value)
        {
            return;
        }

        try
        {
            // Fire and forget to avoid blocking the caller
            _ = _subscriber?.PublishAsync(
                new RedisChannel($"{ChannelPrefix}:{suffix}", RedisChannel.PatternMode.Literal),
                $"{OriginId}|{payload}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish Redis invalidation for {Suffix}", suffix);
        }
    }

    /// <summary>
    /// Separa `origen|carga`. Tolera el formato antiguo sin origen para que una actualización
    /// progresiva —pods viejos y nuevos conviviendo— no pierda invalidaciones.
    /// </summary>
    private static (string Origen, string Payload) DesempaquetarMensaje(string mensaje)
    {
        var corte = mensaje.IndexOf('|');
        return corte < 0
            ? (string.Empty, mensaje)
            : (mensaje[..corte], mensaje[(corte + 1)..]);
    }

    // ── Read (Local Memory) ──────────────────────────────────────────────────

    public AppConfigurationAggregate? GetGlobal(string code)
    {
        _global.TryGetValue(code, out var config);
        return config;
    }

    public AppConfigurationAggregate? GetForTenant(Guid tenantId, string code)
        => TryGet(_tenant, tenantId, code);

    public AppConfigurationAggregate? GetForSuite(Guid suiteId, string code)
        => TryGet(_suite, suiteId, code);

    public AppConfigurationAggregate? GetForModule(Guid moduleId, string code)
        => TryGet(_module, moduleId, code);

    public AppConfigurationAggregate? GetWithPrecedence(
        string code,
        Guid? tenantId,
        Guid? suiteId = null,
        Guid? moduleId = null)
    {
        if (moduleId.HasValue)
        {
            var moduleValue = TryGet(_module, moduleId.Value, code);
            if (moduleValue is not null) return moduleValue;
        }

        if (suiteId.HasValue)
        {
            var suiteValue = TryGet(_suite, suiteId.Value, code);
            if (suiteValue is not null) return suiteValue;
        }

        if (tenantId.HasValue)
        {
            var tenantValue = TryGet(_tenant, tenantId.Value, code);
            if (tenantValue is not null) return tenantValue;
        }

        _global.TryGetValue(code, out var global);
        return global;
    }

    public IReadOnlyList<AppConfigurationAggregate> GetAllGlobal()
        => _global.Values.ToList();

    public IReadOnlyList<AppConfigurationAggregate> GetAllForTenant(Guid tenantId)
        => _tenant.TryGetValue(tenantId, out var cache) ? cache.Values.ToList() : [];

    public bool HasTenantOverride(string code, Guid tenantId)
        => TryGet(_tenant, tenantId, code) is not null;

    // ── Write (Local Memory + PubSub) ────────────────────────────────────────

    public void PopulateGlobal(IEnumerable<AppConfigurationAggregate> configs)
    {
        foreach (var config in configs)
            _global[config.Code.GetValue()] = config;
    }

    public void PopulateTenant(Guid tenantId, IEnumerable<AppConfigurationAggregate> configs)
        => PopulateScope(_tenant, tenantId, configs);

    public void PopulateSuite(Guid suiteId, IEnumerable<AppConfigurationAggregate> configs)
        => PopulateScope(_suite, suiteId, configs);

    public void PopulateModule(Guid moduleId, IEnumerable<AppConfigurationAggregate> configs)
        => PopulateScope(_module, moduleId, configs);

    /// <summary>
    /// Invalida la configuración de un inquilino y AVISA SIEMPRE a los demás pods.
    ///
    /// <para>El aviso estaba condicionado a que este pod tuviera algo que borrar
    /// (<c>if (_tenant.TryRemove(...))</c>), y esa condición es justo la que no se cumple en el
    /// caso que importa: un inquilino dado de alta DESPUÉS del arranque no está en la memoria de
    /// este pod, así que no había nada que quitar y el aviso nunca salía. Los demás pods no se
    /// enteraban de su configuración <b>nunca</b> —hasta reiniciar—, no con un desfase.</para>
    ///
    /// <para>Medido en vivo el 2026-08-04 con dos réplicas: al publicar
    /// <c>AUTH_REFRESH_TOKEN_ENABLED</c> para un inquilino recién creado, el pod que lo publicó
    /// emitía refresh token y el otro no; con un inquilino que ya existía al arrancar, ambos
    /// coincidían al instante. La diferencia era esta línea.</para>
    ///
    /// <para>El aviso es un hecho del mundo —«la configuración de este inquilino cambió»—, no una
    /// nota sobre el estado local de quien lo emite. Condicionarlo a la memoria propia era mezclar
    /// las dos cosas. El coste de publicar de más es un mensaje por escritura de configuración; el
    /// de publicar de menos era servir permisos obsoletos en la mitad del clúster.</para>
    ///
    /// <para>No hay riesgo de tormenta: <see cref="PublishInvalidation"/> ya calla mientras se
    /// aplica un aviso ajeno (G-170), así que un pod que recarga por un mensaje remoto no genera
    /// otro.</para>
    /// </summary>
    public void InvalidateTenant(Guid tenantId)
    {
        _tenant.TryRemove(tenantId, out _);
        PublishInvalidation("tenant", tenantId.ToString());
    }

    // Suite y módulo no publican a propósito: sus únicos llamadores en producción están DENTRO
    // de ConfigurationProvider.ReloadTenantAsync, que ya viaja como invalidación de inquilino y
    // repuebla ambos ámbitos en el pod remoto. Publicar por separado exigiría un manejador de
    // recarga por suite —que hoy no existe— y dejaría el bucket vacío resolviendo por herencia
    // al valor del inquilino: un valor incorrecto, no una ausencia. Si algún día se invalida una
    // suite fuera de esa ruta, hay que añadir el canal Y su recarga, no solo el canal.
    public void InvalidateSuite(Guid suiteId) => _suite.TryRemove(suiteId, out _);

    public void InvalidateModule(Guid moduleId) => _module.TryRemove(moduleId, out _);

    public void InvalidateAll()
    {
        _global.Clear();
        _tenant.Clear();
        _suite.Clear();
        _module.Clear();
        PublishInvalidation("all");
    }

    public void Dispose()
    {
        if (_subscriber != null)
        {
            _subscriber.UnsubscribeAll();
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static AppConfigurationAggregate? TryGet(
        ConcurrentDictionary<Guid, ConcurrentDictionary<string, AppConfigurationAggregate>> store,
        Guid key,
        string code)
        => store.TryGetValue(key, out var inner) && inner.TryGetValue(code, out var config)
            ? config
            : null;

    private static void PopulateScope(
        ConcurrentDictionary<Guid, ConcurrentDictionary<string, AppConfigurationAggregate>> store,
        Guid key,
        IEnumerable<AppConfigurationAggregate> configs)
    {
        var bucket = store.GetOrAdd(key, _ => new ConcurrentDictionary<string, AppConfigurationAggregate>(StringComparer.OrdinalIgnoreCase));
        foreach (var config in configs)
            bucket[config.Code.GetValue()] = config;
    }
}
