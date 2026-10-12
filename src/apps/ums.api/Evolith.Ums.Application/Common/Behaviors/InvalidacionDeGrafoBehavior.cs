namespace Evolith.Ums.Application.Common.Behaviors;

using System.Collections.Concurrent;
using MediatR;
using Microsoft.Extensions.Logging;
using Evolith.Ums.Application.Abstractions.Messaging;
using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Domain.Kernel;

/// <summary>
/// Invalida el grafo retenido tras cualquier comando que haya tenido éxito.
///
/// <para>POR QUÉ AQUÍ Y NO EN CADA MANEJADOR. Hay 43 manejadores capaces de cambiar lo que un grafo
/// contiene. Cablear la invalidación en cada uno significa que olvidarse de UNO sirve un permiso ya
/// retirado hasta que el grafo caduque solo —el fallo abierto, y silencioso—. La tubería se ejecuta
/// para todos por construcción, y un comando nuevo queda cubierto sin que nadie se acuerde.</para>
///
/// <para>POR QUÉ NO POR EVENTO DE DOMINIO, que sería lo natural. Medido el 2026-08-18: la cobertura
/// de eventos es PARCIAL —<c>SystemSuite</c> tiene 27 métodos que mutan y 9 <c>RaiseEvent</c>,
/// <c>Profile</c> tiene 9 y 6— y además el despacho vive en <c>UmsPlatformDbContext</c>, que 22
/// repositorios en memoria no atraviesan. Colgarlo del evento habría dejado fuera justo lo que se
/// quería cubrir. El comando, en cambio, siempre pasa.</para>
///
/// <para>A QUÉ NIVEL INVALIDA. El modelo de autorización está acotado al inquilino —<c>SystemSuite</c>,
/// <c>Role</c>, <c>PermissionTemplate</c> y <c>Profile</c> llevan <c>TenantId</c>, y <c>Profile</c>
/// rechaza una plantilla ajena—, así que un cambio en uno no puede alterar el grafo de otro y basta
/// avanzar el contador de ese inquilino. Dos casos se salen y van al contador global:</para>
/// <list type="bullet">
///   <item>Los comandos de configuración: <c>FeatureFlagProps.TenantId</c> y
///   <c>AppConfiguration.TenantId</c> son ANULABLES, de modo que una bandera o un parámetro sin
///   inquilino cambian el grafo de todos.</item>
///   <item>El administrador interno operando entre inquilinos, donde <c>OrganizationId</c> es
///   <c>null</c> y no hay uno al que acotar.</item>
/// </list>
///
/// <para>La regla falla hacia el lado seguro: equivocarse invalida de más —se reconstruye un grafo
/// que seguía siendo válido— nunca de menos.</para>
/// </summary>
public sealed class InvalidacionDeGrafoBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    /// <summary>
    /// Espacio de nombres de lo que NO está acotado a un inquilino. Se compara sobre el tipo del
    /// comando y no sobre una lista de nombres: una lista hay que mantenerla, y el día que no se
    /// mantenga el fallo será silencioso.
    /// </summary>
    private const string EspacioDeConfiguracion = ".Configuration.";

    /// <summary>Un comando no cambia de forma en tiempo de ejecución; la reflexión se paga una vez.</summary>
    private static readonly ConcurrentDictionary<Type, bool> EsComandoPorTipo = new();

    private readonly ICacheDeGrafo _cache;
    private readonly ITenantContext _inquilino;
    private readonly ILogger<InvalidacionDeGrafoBehavior<TRequest, TResponse>> _logger;

    public InvalidacionDeGrafoBehavior(
        ICacheDeGrafo cache,
        ITenantContext inquilino,
        ILogger<InvalidacionDeGrafoBehavior<TRequest, TResponse>> logger)
    {
        _cache = cache;
        _inquilino = inquilino;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var respuesta = await next();

        // Solo comandos, y solo si salieron bien: un comando que falló no cambió nada, y tirar los
        // grafos de un inquilino entero por un 422 sería regalar trabajo a cada intento inválido.
        if (!EsComando(request) || respuesta is not Result resultado || resultado.IsFailure)
        {
            return respuesta;
        }

        // La invalidación NO puede tumbar la operación. El comando ya se confirmó: propagar aquí un
        // fallo del almacén devolvería un error por algo que sí ocurrió, y el llamante lo
        // reintentaría. Se registra y se sigue; lo peor que pasa es que un grafo viva hasta su
        // caducidad absoluta, que está acotada.
        try
        {
            await InvalidarAsync(request, cancellationToken);
        }
        catch (Exception error)
        {
            _logger.LogError(error,
                "No se pudo invalidar el grafo tras {Comando}. El comando SÍ se aplicó: puede servirse "
                + "un grafo obsoleto hasta que caduque.",
                typeof(TRequest).Name);
        }

        return respuesta;
    }

    private Task InvalidarAsync(TRequest request, CancellationToken cancellationToken)
    {
        var inquilino = _inquilino.OrganizationId;

        if (EsDeConfiguracion(request) || inquilino is null)
        {
            return _cache.InvalidarGlobalAsync(cancellationToken);
        }

        return _cache.InvalidarInquilinoAsync(inquilino.Value, cancellationToken);
    }

    private static bool EsDeConfiguracion(TRequest request) =>
        request.GetType().Namespace?.Contains(EspacioDeConfiguracion, StringComparison.Ordinal) == true;

    private static bool EsComando(TRequest request) =>
        EsComandoPorTipo.GetOrAdd(request.GetType(), static tipo =>
            tipo.GetInterfaces().Any(i =>
                i == typeof(ICommand)
                || (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommand<>))));
}
