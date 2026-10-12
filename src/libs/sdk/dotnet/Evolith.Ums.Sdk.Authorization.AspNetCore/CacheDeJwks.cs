using System.Security.Cryptography;
using System.Text.Json;

namespace Evolith.Ums.Sdk.Authorization.AspNetCore;

/// <summary>Cómo terminó la búsqueda de una clave. Distingue «no está» de «no pude mirar».</summary>
public enum EstadoDeClave
{
    /// <summary>La clave se encontró y es utilizable.</summary>
    Ok,

    /// <summary>El <c>kid</c> no está entre las claves que publica el emisor.</summary>
    Desconocida,

    /// <summary>No se pudo consultar el JWKS y no hay copia previa.</summary>
    Inaccesible,
}

/// <summary>Resultado de buscar una clave por su <c>kid</c>.</summary>
/// <param name="Estado">Cómo terminó la búsqueda.</param>
/// <param name="Clave">Parámetros públicos de la clave RSA, si se encontró.</param>
public sealed record ResultadoDeClave(EstadoDeClave Estado, RSAParameters? Clave);

/// <summary>
/// Caché del JWKS del emisor: las claves públicas con las que se verifica un portador RS256.
/// </summary>
/// <remarks>
/// <para>
/// Existe por dos razones que tiran en direcciones opuestas y hay que conciliar:
/// </para>
/// <para>
/// <b>1. No se puede ir a la red en cada verificación.</b> La verificación está en la ruta caliente
/// de cada petición; un GET al JWKS por token convertiría al emisor en el cuello de botella de todo
/// el ecosistema, y en su punto único de fallo.
/// </para>
/// <para>
/// <b>2. Las claves rotan.</b> Con una caché eterna, una rotación dejaría a todos los satélites
/// rechazando tokens legítimos hasta que alguien los reiniciara. ADR-0157 §4.4 permite el solape
/// para que la rotación no sea un corte, pero el solape solo sirve si el consumidor va a buscar la
/// clave nueva cuando la ve.
/// </para>
/// <para>
/// La conciliación es traer el JWKS cuando aparece un <c>kid</c> desconocido. Y ahí está el riesgo
/// que esta clase existe para acotar: <b>ese refresco lo dispara un desconocido</b>, porque el
/// <c>kid</c> viaja en la cabecera de un token todavía sin verificar. Sin límite, basta con mandar
/// tokens con <c>kid</c> inventados —uno distinto cada vez— para que el satélite bombardee al
/// emisor: un amplificador de denegación de servicio construido con nuestras propias manos.
/// </para>
/// <para>
/// Por eso el refresco está acotado por tres mecanismos que se complementan: <b>cadencia mínima</b>
/// entre refrescos disparados por un <c>kid</c> desconocido, <b>memoria de lo ya descartado</b> para
/// que repetir el mismo <c>kid</c> falso no consuma esa cadencia, y <b>una sola petición en vuelo</b>
/// para que N verificaciones concurrentes no produzcan N peticiones.
/// </para>
/// </remarks>
public sealed class CacheDeJwks : IDisposable
{
    /// <summary>
    /// Tamaño mínimo de clave RSA aceptado, en bytes del módulo (2048 bits).
    /// </summary>
    /// <remarks>
    /// Es el mismo suelo que UMS se impone al arrancar (ADR-0157 §4.7). Se repite aquí porque el
    /// consumidor no puede dar por hecho que el otro extremo lo cumple: una clave corta verifica
    /// sin error y sin aviso, y deja el sistema débil sin que nada falle.
    /// </remarks>
    private const int BytesMinimosDeModulo = 256;

    private const int MaxBytes = 512 * 1024;

    private readonly Uri uri;
    private readonly HttpClient http;
    private readonly bool httpEsPropio;
    private readonly TimeSpan ttl;
    private readonly TimeSpan esperaEntreRefrescos;
    private readonly TimeSpan memoriaDeDescartados;
    private readonly TimeSpan tiempoDeEspera;
    private readonly Func<DateTimeOffset> reloj;
    private readonly SemaphoreSlim cerrojo = new(1, 1);

    private Dictionary<string, RSAParameters> claves = [];
    private DateTimeOffset traidoEn = DateTimeOffset.MinValue;

    /// <summary>
    /// Momento del último refresco disparado por un <c>kid</c> desconocido con la copia fresca.
    /// </summary>
    /// <remarks>
    /// Se lleva aparte del refresco por caducidad a propósito: si un único contador gobernara los
    /// dos, el refresco del arranque —programado, nuestro, inevitable— gastaría el presupuesto del
    /// siguiente, y una rotación legítima esperaría la ventana entera sin motivo. El presupuesto
    /// que hay que racionar es solo el que puede disparar un desconocido.
    /// </remarks>
    private DateTimeOffset ultimoRefrescoPorKid = DateTimeOffset.MinValue;

    /// <summary>Momento del último intento fallido. Impide martillear un emisor caído.</summary>
    private DateTimeOffset ultimoFallo = DateTimeOffset.MinValue;

    private readonly Dictionary<string, DateTimeOffset> kidsDescartados = [];

    /// <summary>Construye la caché.</summary>
    /// <param name="opciones">Ajustes del JWKS.</param>
    public CacheDeJwks(OpcionesDeJwks opciones)
    {
        ArgumentNullException.ThrowIfNull(opciones);
        if (string.IsNullOrWhiteSpace(opciones.Uri))
        {
            throw new ArgumentException("Falta la URI del JWKS del emisor.", nameof(opciones));
        }

        uri = new Uri(opciones.Uri, UriKind.Absolute);
        httpEsPropio = opciones.HttpClient is null;
        http = opciones.HttpClient ?? new HttpClient();
        ttl = opciones.Ttl;
        esperaEntreRefrescos = opciones.EsperaEntreRefrescos;
        memoriaDeDescartados = opciones.MemoriaDeDescartados;
        tiempoDeEspera = opciones.TiempoDeEspera;
        reloj = opciones.Reloj ?? (() => DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Devuelve la clave de ese <c>kid</c>, trayendo el JWKS solo si hace falta y solo si toca.
    /// </summary>
    /// <remarks>
    /// Nunca devuelve «alguna» clave: sin <c>kid</c> que case, se rechaza. Probar el token contra
    /// todas las claves parece inofensivo —o firma o no firma— pero convierte cada rotación en una
    /// ventana en la que una clave retirada sigue siendo válida, y borra la única señal que dice
    /// con qué clave se emitió realmente.
    /// </remarks>
    /// <param name="kid">Identificador de la clave, tomado de la cabecera del token.</param>
    /// <param name="cancelacion">Cancelación de la petición en curso.</param>
    /// <returns>La clave, o el motivo por el que no se pudo dar.</returns>
    public async Task<ResultadoDeClave> ClaveParaAsync(string kid, CancellationToken cancelacion = default)
    {
        if (string.IsNullOrEmpty(kid)) return new ResultadoDeClave(EstadoDeClave.Desconocida, null);

        await cerrojo.WaitAsync(cancelacion).ConfigureAwait(false);
        try
        {
            var ahora = reloj();

            // Lo ya descartado se responde sin red y sin gastar la cadencia. Es lo que impide que
            // un `kid` falso repetido monopolice el refresco que necesita una rotación de verdad.
            if (kidsDescartados.TryGetValue(kid, out var descartadoEn)
                && ahora - descartadoEn < memoriaDeDescartados)
            {
                return new ResultadoDeClave(EstadoDeClave.Desconocida, null);
            }

            var hayCopia = traidoEn != DateTimeOffset.MinValue;
            var copiaFresca = hayCopia && ahora - traidoEn < ttl;
            var estaba = claves.TryGetValue(kid, out var enCache);
            if (estaba && copiaFresca) return new ResultadoDeClave(EstadoDeClave.Ok, enCache);

            // Hay que ir a la red, y el motivo cambia quién está al mando:
            //  - Copia fría o caducada: lo dispara el reloj, no quien llama. Nadie de fuera puede
            //    acelerar ese reloj, así que como mucho ocurre una vez por TTL.
            //  - Copia fresca y `kid` sin conocer: lo dispara la cabecera de un token SIN VERIFICAR.
            //    Es el caso que hay que racionar, y el único.
            var loDisparaUnDesconocido = copiaFresca;
            var refrescado = await RefrescarAsync(ahora, loDisparaUnDesconocido, cancelacion).ConfigureAwait(false);

            if (claves.TryGetValue(kid, out var trasRefresco))
            {
                return new ResultadoDeClave(EstadoDeClave.Ok, trasRefresco);
            }

            if (refrescado)
            {
                // Se miró de verdad y no estaba: se recuerda, para no volver a mirar por este `kid`.
                kidsDescartados[kid] = reloj();
                PodarDescartados();
                return new ResultadoDeClave(EstadoDeClave.Desconocida, null);
            }

            // Copia caducada y refresco frenado: se sirve lo viejo antes que rechazar un token
            // legítimo por un problema de red. La clave sigue siendo del emisor, solo sin revalidar.
            if (estaba) return new ResultadoDeClave(EstadoDeClave.Ok, enCache);

            return new ResultadoDeClave(
                hayCopia ? EstadoDeClave.Desconocida : EstadoDeClave.Inaccesible, null);
        }
        finally
        {
            _ = cerrojo.Release();
        }
    }

    /// <summary>Libera el <see cref="HttpClient"/> propio, si lo hay.</summary>
    public void Dispose()
    {
        cerrojo.Dispose();
        if (httpEsPropio) http.Dispose();
    }

    /// <summary>
    /// Trae el JWKS si toca. El cerrojo de <see cref="ClaveParaAsync"/> ya garantiza que solo hay
    /// una petición en vuelo: N verificaciones concurrentes con la caché fría producen UNA
    /// petición, no N, que es el caso del arranque de un satélite con tráfico encima.
    /// </summary>
    private async Task<bool> RefrescarAsync(
        DateTimeOffset ahora, bool loDisparaUnDesconocido, CancellationToken cancelacion)
    {
        if (loDisparaUnDesconocido
            && ultimoRefrescoPorKid != DateTimeOffset.MinValue
            && ahora - ultimoRefrescoPorKid < esperaEntreRefrescos)
        {
            return false;
        }

        // Si el emisor acaba de fallar, no se insiste: reintentar sin freno contra algo caído
        // convierte una caída ajena en una propia.
        if (ultimoFallo != DateTimeOffset.MinValue && ahora - ultimoFallo < esperaEntreRefrescos)
        {
            return false;
        }

        if (loDisparaUnDesconocido) ultimoRefrescoPorKid = ahora;

        var ok = await TraerAsync(cancelacion).ConfigureAwait(false);
        if (!ok) ultimoFallo = reloj();
        return ok;
    }

    private async Task<bool> TraerAsync(CancellationToken cancelacion)
    {
        try
        {
            using var limite = CancellationTokenSource.CreateLinkedTokenSource(cancelacion);
            limite.CancelAfter(tiempoDeEspera);

            using var peticion = new HttpRequestMessage(HttpMethod.Get, uri);
            peticion.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

            using var respuesta = await http.SendAsync(peticion, limite.Token).ConfigureAwait(false);
            if (!respuesta.IsSuccessStatusCode) return false;

            var texto = await respuesta.Content.ReadAsStringAsync(limite.Token).ConfigureAwait(false);

            // Un JWKS legítimo son unos cientos de bytes. Un documento enorme aquí es un error de
            // enrutado o algo peor, y no se gana nada intentando interpretarlo.
            if (texto.Length > MaxBytes) return false;

            using var documento = JsonDocument.Parse(texto);
            if (!documento.RootElement.TryGetProperty("keys", out var listaDeClaves)
                || listaDeClaves.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            var nuevas = new Dictionary<string, RSAParameters>(StringComparer.Ordinal);
            foreach (var jwk in listaDeClaves.EnumerateArray())
            {
                if (Convertir(jwk) is (string kid, RSAParameters parametros)) nuevas[kid] = parametros;
            }

            // Solo se reemplaza si se obtuvo algo utilizable: un JWKS vacío o ilegible no debe
            // borrar las claves buenas que ya se tenían.
            if (nuevas.Count == 0) return false;

            claves = nuevas;
            traidoEn = reloj();

            // Una rotación puede reintroducir un `kid` que antes no estaba; la memoria de
            // descartados dejaría de decir la verdad, así que se limpia junto con la copia.
            kidsDescartados.Clear();
            return true;
        }
        catch (Exception error) when (
            error is HttpRequestException or TaskCanceledException or OperationCanceledException
                or JsonException or FormatException or UriFormatException)
        {
            // Cualquier fallo —red, tiempo agotado, JSON ilegible, HTML del enrutador equivocado—
            // es un refresco que no ocurrió. Nunca una excepción hacia arriba: quien verifica
            // decide qué hacer con la ausencia de clave, y lo hace con un rechazo.
            return false;
        }
    }

    /// <summary>
    /// Convierte un JWK en parámetros RSA, descartando lo que no sirva.
    /// </summary>
    /// <remarks>
    /// Solo pasan claves RSA de firma: aceptar una simétrica desde un documento público sería
    /// exactamente la degradación que todo esto quiere evitar —el emisor publicaría, sin querer,
    /// con qué firmar—.
    /// </remarks>
    private static (string, RSAParameters)? Convertir(JsonElement jwk)
    {
        if (jwk.ValueKind != JsonValueKind.Object) return null;
        if (!Texto(jwk, "kty").Equals("RSA", StringComparison.Ordinal)) return null;

        var uso = Texto(jwk, "use");
        if (uso.Length > 0 && !uso.Equals("sig", StringComparison.Ordinal)) return null;

        var kid = Texto(jwk, "kid");
        if (kid.Length == 0) return null;

        // Si la clave declara algoritmo, es el que manda. Si no lo declara, RS256 es lo único que
        // UMS emite (ADR-0157) y lo único que este verificador sabe comprobar.
        var alg = Texto(jwk, "alg");
        if (alg.Length > 0 && !alg.Equals("RS256", StringComparison.Ordinal)) return null;

        var n = Texto(jwk, "n");
        var e = Texto(jwk, "e");
        if (n.Length == 0 || e.Length == 0) return null;

        try
        {
            var modulo = Base64UrlDecode(n);
            if (modulo.Length < BytesMinimosDeModulo) return null;

            return (kid, new RSAParameters { Modulus = modulo, Exponent = Base64UrlDecode(e) });
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static string Texto(JsonElement objeto, string propiedad) =>
        objeto.TryGetProperty(propiedad, out var valor) && valor.ValueKind == JsonValueKind.String
            ? valor.GetString() ?? string.Empty
            : string.Empty;

    private static byte[] Base64UrlDecode(string input)
    {
        var normalized = input.Replace('-', '+').Replace('_', '/');
        switch (normalized.Length % 4)
        {
            case 2: normalized += "=="; break;
            case 3: normalized += "="; break;
            case 1: throw new FormatException("Invalid base64url length.");
            default: break;
        }

        return Convert.FromBase64String(normalized);
    }

    private void PodarDescartados()
    {
        var ahora = reloj();
        var caducados = kidsDescartados
            .Where(par => ahora - par.Value >= memoriaDeDescartados)
            .Select(par => par.Key)
            .ToList();
        foreach (var kid in caducados) _ = kidsDescartados.Remove(kid);
    }
}

/// <summary>Ajustes de la caché del JWKS.</summary>
public sealed class OpcionesDeJwks
{
    /// <summary>URI absoluta del JWKS del emisor.</summary>
    public string Uri { get; set; } = string.Empty;

    /// <summary>Cliente HTTP. Se inyecta para poder probar sin red.</summary>
    public HttpClient? HttpClient { get; set; }

    /// <summary>Cuánto se considera fresca la copia. Por defecto 10 min, el <c>max-age</c> de UMS.</summary>
    public TimeSpan Ttl { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>Cadencia mínima entre refrescos disparados por un <c>kid</c> desconocido.</summary>
    public TimeSpan EsperaEntreRefrescos { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>Cuánto se recuerda un <c>kid</c> que no estaba en el JWKS.</summary>
    public TimeSpan MemoriaDeDescartados { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>Tiempo máximo de la petición al JWKS.</summary>
    public TimeSpan TiempoDeEspera { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Reloj inyectable, para que las pruebas no dependan del paso real del tiempo.</summary>
    public Func<DateTimeOffset>? Reloj { get; set; }
}
