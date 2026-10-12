namespace Evolith.Ums.Sdk.Contracts;

/// <summary>
/// Compile-time constants describing the AuthorizationGraph schema this SDK was built against.
/// See ADR-0074 for the versioning policy and SCHEMA_VERSIONING.md for the operational summary.
/// </summary>
public static class SchemaVersion
{
    /// <summary>
    /// Versión que declara una respuesta COMPLETA — la que recibe todo el que no envía
    /// <c>?include=</c>, es decir todos los satélites de hoy.
    ///
    /// <para>Se queda en 2.4.0 A PROPÓSITO y no sube con la proyección. La tentación era subirla a
    /// 2.5.0 en todas las respuestas y declararlo inocuo «porque los verificadores conocidos
    /// comparan RANGO». El argumento es falso por dos motivos. Primero, no son los únicos: un
    /// satélite puede tener el esquema 2.4.0 vendorizado y validar contra su <c>const</c>, que es
    /// una comparación por IGUALDAD, y entonces rechazaría TODAS las respuestas sin haber pedido
    /// jamás una proyección. Segundo, «los verificadores conocidos» no es una propiedad del
    /// contrato: es un censo, y un censo caduca. En este mismo repositorio hay un consumidor que
    /// compara por igualdad —<c>src/tests/e2e-functional/robosoft/contexts/configuration.py</c>—.</para>
    ///
    /// <para>Un mismo servidor SÍ puede declarar dos versiones según la petición, siempre que el
    /// esquema publicado lo admita: aquí lo hace condicionando el <c>const</c> a la presencia de
    /// <c>projection</c>. La versión solo sube cuando la respuesta cambia de forma de verdad.</para>
    /// </summary>
    public const string Current = "2.4.0";

    /// <summary>
    /// Versión que declara una respuesta PROYECTADA (<c>?include=</c>, v2.5.0). Solo la ve quien
    /// pide una proyección, y quien la pide ya sabe que la clave <c>projection</c> viaja y que
    /// faltan bloques: la versión nueva describe una forma que de verdad es nueva.
    /// </summary>
    public const string Projected = "2.5.0";

    /// <summary>
    /// La versión MÁS ALTA que este SDK entiende. Es el techo contra el que se comparan
    /// <see cref="IsMajorMatch"/>, <see cref="IsMinorAhead"/> y <see cref="IsMinorBehind"/>: si el
    /// techo fuera <see cref="Current"/>, un grafo proyectado —2.5.0, que este SDK lee sin
    /// problema— se reportaría como «servidor por delante del SDK», que es falso.
    /// </summary>
    public const string Latest = Projected;

    /// <summary>
    /// Las versiones que este SDK puede EMITIR y que el esquema publicado acepta como valor de
    /// <c>schemaVersion</c>. Existe para que una prueba pueda atar el <c>enum</c> del esquema a las
    /// constantes en vez de copiar los números.
    /// </summary>
    public static readonly IReadOnlyList<string> Emitidas = [Current, Projected];

    /// <summary>Inclusive lower bound of the schema compatibility range.</summary>
    public const string CompatibilityMinInclusive = "2.0.0";

    /// <summary>Exclusive upper bound of the schema compatibility range.</summary>
    public const string CompatibilityMaxExclusive = "3.0.0";

    /// <summary>
    /// Returns true when <paramref name="schemaVersion"/> falls within the SDK's supported range.
    /// </summary>
    public static bool IsSupported(string? schemaVersion)
    {
        if (string.IsNullOrWhiteSpace(schemaVersion)) return false;
        if (!TryParse(schemaVersion, out var v)) return false;
        if (!TryParse(CompatibilityMinInclusive, out var min)) return false;
        if (!TryParse(CompatibilityMaxExclusive, out var max)) return false;
        return Compare(v, min) >= 0 && Compare(v, max) < 0;
    }

    /// <summary>True when the major component of <paramref name="schemaVersion"/> equals the current major.</summary>
    public static bool IsMajorMatch(string? schemaVersion)
    {
        if (!TryParse(schemaVersion, out var v)) return false;
        if (!TryParse(Latest, out var c)) return false;
        return v.Major == c.Major;
    }

    /// <summary>True when <paramref name="schemaVersion"/> is a MINOR ahead of the current schema (server newer).</summary>
    public static bool IsMinorAhead(string? schemaVersion)
    {
        if (!TryParse(schemaVersion, out var v)) return false;
        if (!TryParse(Latest, out var c)) return false;
        if (v.Major != c.Major) return false;
        return v.Minor > c.Minor;
    }

    /// <summary>True when <paramref name="schemaVersion"/> is a MINOR behind the current schema (SDK newer).</summary>
    public static bool IsMinorBehind(string? schemaVersion)
    {
        if (!TryParse(schemaVersion, out var v)) return false;
        if (!TryParse(Latest, out var c)) return false;
        if (v.Major != c.Major) return false;
        return v.Minor < c.Minor;
    }

    internal static bool TryParse(string? input, out (int Major, int Minor, int Patch) parsed)
    {
        parsed = default;
        if (string.IsNullOrWhiteSpace(input)) return false;
        var parts = input.Split('.');
        if (parts.Length != 3) return false;
        if (!int.TryParse(parts[0], out var major) ||
            !int.TryParse(parts[1], out var minor) ||
            !int.TryParse(parts[2], out var patch)) return false;
        parsed = (major, minor, patch);
        return true;
    }

    internal static int Compare((int Major, int Minor, int Patch) a, (int Major, int Minor, int Patch) b)
    {
        if (a.Major != b.Major) return a.Major.CompareTo(b.Major);
        if (a.Minor != b.Minor) return a.Minor.CompareTo(b.Minor);
        return a.Patch.CompareTo(b.Patch);
    }
}
