using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Evolith.Ums.Application.Common.Aop;

/// <summary>
/// G-040 (residual #5, FR-072): desinfecta la <b>metadata serializada</b> de la traza de auditoría
/// antes de persistirla, redactando los valores cuyas claves casen patrones sensibles (contraseñas,
/// hashes, PIN, llaves, tokens, secretos, credenciales…). Se aplica en AMBAS vías —la manual
/// (<c>RecordAuditCommandHandler</c>) y la automática (<c>AuditTrailAspect</c>)— antes de emitir/persistir.
///
/// <para>
/// La traza es <b>append-only e inmutable</b> (G-081): un secreto filtrado a ella no se puede borrar
/// después. Por eso la redacción es <b>conservadora</b> (ante la duda, redacta) y por <b>clave</b>, no
/// por valor: se conserva la clave —para dejar constancia de que el dato existía— y se sustituye su
/// valor completo por <see cref="RedactionPlaceholder"/>. Si la clave marca un subárbol (objeto o
/// arreglo) sensible, se redacta el subárbol entero sin descender en él.
/// </para>
///
/// <para>
/// La metadata de auditoría es un <b>string JSON</b> en ambas vías (la vía manual lo valida como JSON
/// bien formado en <c>RecordAuditCommandValidator</c>; la automática lo serializa con
/// <c>JsonSerializer.Serialize</c>). El saneador parsea ese JSON, recorre el árbol y reconstruye una
/// copia redactada. No muta la entrada.
/// </para>
///
/// <para>
/// <b>Lista de patrones</b> (fácil de extender — añade a <see cref="SubstringPatterns"/> o
/// <see cref="TokenPatterns"/>). Las comparaciones son <b>insensibles a mayúsculas</b>. Se tokeniza la
/// clave por límites <c>camelCase</c>, <c>snake_case</c>, <c>kebab-case</c> y separadores, de modo que
/// <c>apiKey</c>, <c>api_key</c> y <c>API-KEY</c> se tratan igual.
/// <list type="bullet">
///   <item><see cref="SubstringPatterns"/> — patrones largos e inequívocos; casan en cualquier parte
///   de la clave (p. ej. <c>passwordHash</c>, <c>bearerToken</c>, <c>clientSecret</c>).</item>
///   <item><see cref="TokenPatterns"/> — patrones cortos y ambiguos; casan solo como <b>token
///   delimitado</b> para no redactar por accidente claves legítimas (p. ej. <c>shipping</c> contiene
///   «pin», <c>monkey</c> contiene «key»: no deben redactarse).</item>
/// </list>
/// </para>
///
/// <para>
/// <b>Limitación honesta:</b> la redacción es por <b>nombre de clave</b>. Un secreto embebido en el
/// <i>valor</i> de una clave no sensible (p. ej. un texto libre <c>"note":"la clave es 1234"</c>) no
/// se detecta. La lista es deliberadamente amplia por seguridad: puede sobre-redactar claves legítimas
/// que contengan «key» (p. ej. <c>publicKey</c>, <c>idempotencyKey</c>) — la sobre-redacción es el lado
/// seguro para una traza inmutable.
/// </para>
/// </summary>
public static class AuditMetadataSanitizer
{
    /// <summary>Valor de reemplazo para los datos redactados (alineado con <c>PiiMaskingPolicy</c>).</summary>
    public const string RedactionPlaceholder = "[REDACTED]";

    /// <summary>
    /// Patrones largos e inequívocos: casan como subcadena en cualquier parte de la clave (en minúsculas).
    /// Extiende aquí para nuevos secretos «largos».
    /// </summary>
    private static readonly string[] SubstringPatterns =
    [
        "password",
        "passwd",
        "secret",
        "token",
        "apikey",
        "authorization",
        "credential",
        "hash",
    ];

    /// <summary>
    /// Patrones cortos y ambiguos: casan solo como token delimitado (p. ej. «key» en <c>apiKey</c>,
    /// pero no en <c>monkey</c>). Extiende aquí para nuevos secretos «cortos».
    /// </summary>
    private static readonly HashSet<string> TokenPatterns = new(StringComparer.OrdinalIgnoreCase)
    {
        "pwd",
        "pin",
        "key",
        "auth",
        "cred",
    };

    // Tokeniza una clave por límites camelCase, respetando acrónimos: "APIKey" → [API, Key],
    // "apiKey" → [api, Key], "api_key" → [api, key], "password" → [password].
    private static readonly Regex TokenBoundary = new(
        "[A-Z]+(?![a-z])|[A-Z][a-z0-9]*|[a-z0-9]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    /// <summary>
    /// Devuelve una copia de la metadata JSON con los valores de las claves sensibles redactados.
    /// Entrada nula, en blanco o no-JSON se devuelve sin cambios (no falla): ambas vías reales
    /// garantizan JSON válido, y una entrada sin estructura clave-valor no tiene claves que redactar.
    /// </summary>
    public static string? Sanitize(string? metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata))
        {
            return metadata;
        }

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(metadata);
        }
        catch (JsonException)
        {
            // No es JSON: sin estructura clave-valor sobre la que redactar por clave. Se devuelve tal cual.
            return metadata;
        }

        if (root is null)
        {
            // Literal JSON "null": nada que redactar.
            return metadata;
        }

        var sanitized = SanitizeNode(root);
        return sanitized?.ToJsonString();
    }

    /// <summary>
    /// Indica si una clave debe considerarse sensible (y por tanto redactarse su valor). Público para
    /// que la lista de patrones sea verificable de forma unitaria y fácil de razonar.
    /// </summary>
    public static bool IsSensitiveKey(string? key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return false;
        }

        var lower = key.ToLowerInvariant();
        if (SubstringPatterns.Any(pattern => lower.Contains(pattern, StringComparison.Ordinal)))
        {
            return true;
        }

        return TokenBoundary.Matches(key).Any(token => TokenPatterns.Contains(token.Value));
    }

    // Reconstruye el nodo redactado. No muta la entrada: los objetos/arreglos se rehacen desde cero y
    // los escalares se clonan, de modo que ningún nodo cambia de padre.
    private static JsonNode? SanitizeNode(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                var sanitizedObject = new JsonObject();
                foreach (var property in obj)
                {
                    sanitizedObject[property.Key] = IsSensitiveKey(property.Key)
                        ? RedactionPlaceholder
                        : SanitizeNode(property.Value);
                }

                return sanitizedObject;

            case JsonArray array:
                var sanitizedArray = new JsonArray();
                foreach (var item in array)
                {
                    sanitizedArray.Add(SanitizeNode(item));
                }

                return sanitizedArray;

            default:
                return node?.DeepClone();
        }
    }
}
