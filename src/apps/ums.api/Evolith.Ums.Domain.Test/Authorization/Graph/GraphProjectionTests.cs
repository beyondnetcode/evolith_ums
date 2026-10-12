namespace Evolith.Ums.Domain.Test.Authorization.Graph;

using Evolith.Ums.Domain.Authorization.Graph;
using Xunit;

/// <summary>
/// El parser de <c>?include=</c>. Es la puerta por la que la proyección entra al sistema, así que
/// es donde se hacen cumplir las dos reglas que no se pueden delegar al esquema:
///
/// <list type="bullet">
///   <item>que omitir el parámetro signifique EL GRAFO COMPLETO, sin ruta de código nueva;</item>
///   <item>que no exista forma de pedir media proyección de permisos.</item>
/// </list>
///
/// Un valor desconocido se rechaza aquí y no aguas abajo: si llegara a construirse el grafo, el
/// error tendría que decidirse en un sitio que ya no sabe qué escribió el llamante.
/// </summary>
public class GraphProjectionTests
{
    // ── I1: sin parámetro, todo ──────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    // Solo separadores: indistinguible de no haber pedido nada. Entre leerlo como «todo» o como
    // «nada», solo la primera opción es segura — dar de más nunca rompe a un cliente.
    [InlineData(",")]
    [InlineData(" , , ")]
    public void Sin_valor_util_no_hay_proyeccion(string? include)
    {
        var ok = GraphProjection.TryParse(include, out var proyeccion, out var desconocido);

        Assert.True(ok);
        Assert.Null(proyeccion);
        Assert.Null(desconocido);
    }

    // ── Forma: tolerante ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("permissions")]
    [InlineData("PERMISSIONS")]
    [InlineData("PeRmIsSiOnS")]
    [InlineData("  permissions  ")]
    [InlineData(",permissions,")]
    public void El_token_es_insensible_a_mayusculas_y_tolera_espacios(string include)
    {
        Assert.True(GraphProjection.TryParse(include, out var proyeccion, out _));
        Assert.NotNull(proyeccion);
        Assert.True(proyeccion!.IncluyePermisos);
        Assert.Equal(new[] { GraphProjection.Permisos }, proyeccion.Tokens);
    }

    [Fact]
    public void Los_tokens_se_deduplican_y_salen_en_orden_canonico()
    {
        // El eco es parte del grafo y candidato a clave de caché: dos peticiones equivalentes
        // escritas al revés tienen que producir el mismo eco, o no serviría para eso.
        Assert.True(GraphProjection.TryParse(
            " settings , PERMISSIONS ,permissions,navigation ", out var proyeccion, out _));

        Assert.Equal(
            new[] { GraphProjection.Permisos, GraphProjection.Navegacion, GraphProjection.Ajustes },
            proyeccion!.Tokens);
    }

    [Fact]
    public void Todo_el_vocabulario_se_acepta()
    {
        var include = string.Join(",", GraphProjection.Vocabulario);

        Assert.True(GraphProjection.TryParse(include, out var proyeccion, out _));
        Assert.Equal(GraphProjection.Vocabulario, proyeccion!.Tokens);
        Assert.True(proyeccion.IncluyePermisos);
        Assert.True(proyeccion.IncluyeNavegacion);
        Assert.True(proyeccion.IncluyePerfiles);
        Assert.True(proyeccion.IncluyeBanderas);
        Assert.True(proyeccion.IncluyeConfig);
        Assert.True(proyeccion.IncluyeAjustes);
    }

    // ── I3: fondo intransigente ──────────────────────────────────────────────────

    [Theory]
    // La errata del enunciado. Ignorarla dejaría al cliente creyendo que pidió permisos y
    // recibiendo un grafo sin ellos: leería la ausencia como «no tengo permisos» o, peor, se
    // saltaría la comprobación.
    [InlineData("permisions")]
    [InlineData("PERMISIONS")]
    [InlineData("todo")]
    [InlineData("permission")]
    public void Un_valor_desconocido_aborta_el_parseo(string valor)
    {
        var ok = GraphProjection.TryParse(valor, out var proyeccion, out var desconocido);

        Assert.False(ok);
        Assert.Null(proyeccion);
        Assert.Equal(valor, desconocido);
    }

    [Fact]
    public void Un_valor_desconocido_entre_validos_tambien_aborta()
    {
        // No se acepta «lo que se entienda»: una proyección a medias es peor que ninguna, porque
        // el cliente creería que se aplicó entera.
        Assert.False(GraphProjection.TryParse(
            "permissions,navegacion,features", out var proyeccion, out var desconocido));

        Assert.Null(proyeccion);
        Assert.Equal("navegacion", desconocido);
    }

    [Fact]
    public void El_valor_desconocido_se_devuelve_tal_como_lo_escribio_el_llamante()
    {
        // Sin normalizar: el integrador tiene que ver SU errata en el mensaje, no una versión
        // limpiada de ella que ya no se parece a lo que envió.
        Assert.False(GraphProjection.TryParse("  PerMisions  ", out _, out var desconocido));
        Assert.Equal("PerMisions", desconocido);
    }

    // ── I2: atomicidad de `permissions` ──────────────────────────────────────────

    [Theory]
    [InlineData("scopes")]
    [InlineData("domainPermissions")]
    [InlineData("menuAccess")]
    [InlineData("actions")]
    [InlineData("featureFlags")]
    [InlineData("effectiveConfig")]
    public void No_existe_token_para_pedir_una_seccion_suelta(string seccion)
    {
        // La atomicidad no se comprueba: se hace IMPOSIBLE. Si existiera un token `scopes`, un
        // grafo con scopes y sin domainPermissions sería alcanzable, y ese grafo no deniega de
        // más — CONCEDE de más, porque el evaluador lee la lista ausente como «no hay ninguna
        // denegación explícita». Un fallo que abre no lo ve nadie.
        Assert.False(GraphProjection.TryParse(seccion, out var proyeccion, out var desconocido));
        Assert.Null(proyeccion);
        Assert.Equal(seccion, desconocido);
    }

    [Fact]
    public void Permissions_enciende_las_dos_mitades_a_la_vez()
    {
        Assert.True(GraphProjection.TryParse("permissions", out var proyeccion, out _));

        // Una sola bandera gobierna scopes y domainPermissions: no hay estado intermedio que
        // pudiera dejar una sin la otra.
        Assert.True(proyeccion!.IncluyePermisos);
        Assert.False(proyeccion.IncluyeNavegacion);
    }

    // ── Mensaje de error (la mitad útil de I3) ───────────────────────────────────

    [Fact]
    public void El_mensaje_dice_que_valor_falla_y_cuales_valen()
    {
        var mensaje = GraphProjection.MensajeDeValorDesconocido("permisions");

        Assert.Contains("permisions", mensaje);
        foreach (var valido in GraphProjection.Vocabulario) Assert.Contains(valido, mensaje);
    }

    [Fact]
    public void El_mensaje_encamina_a_quien_escribio_el_nombre_de_la_seccion()
    {
        // Un 400 que solo dijera «valor inválido» obligaría a leer el código de UMS para
        // arreglarlo. Y en el caso del par atómico, además explica POR QUÉ no se puede pedir
        // suelto: sin esa frase parece una rareza de nomenclatura y no una regla de seguridad.
        var scopes = GraphProjection.MensajeDeValorDesconocido("scopes");
        Assert.Contains(GraphProjection.Permisos, scopes);
        Assert.Contains("domainPermissions", scopes);

        var menu = GraphProjection.MensajeDeValorDesconocido("menuAccess");
        Assert.Contains(GraphProjection.Navegacion, menu);
    }

    // ── Cota del valor reflejado ───────────────────────────

    [Fact]
    public void El_valor_desconocido_vuelve_ACOTADO_no_integro()
    {
        // Sin cota, el llamante decide cuántos bytes escribe UMS en el cuerpo del 400 y en su
        // propio registro de Serilog: medido, 8000 caracteres volvían enteros por las dos vías.
        // Es un amplificador gratis y, de paso, un vertedero en los ficheros de registro.
        var enorme = new string('x', 8000);

        Assert.False(GraphProjection.TryParse(enorme, out _, out var desconocido));

        Assert.NotNull(desconocido);
        Assert.True(desconocido!.Length < 100,
            $"el eco debería venir acotado y midió {desconocido.Length}");
        Assert.StartsWith("xxxx", desconocido);

        // Y el corte se DICE. Un valor cortado en seco parece el valor que el llamante escribió,
        // y le haría buscar una errata donde no está.
        Assert.Contains("recortado", desconocido);

        var mensaje = GraphProjection.MensajeDeValorDesconocido(desconocido);
        Assert.True(mensaje.Length < 600, $"el mensaje del 400 midió {mensaje.Length}");
        Assert.DoesNotContain(enorme, mensaje);
    }

    [Fact]
    public void La_cota_tambien_vale_llamando_al_mensaje_directamente()
    {
        // `MensajeDeValorDesconocido` es público: nada impide que otra ruta lo llame con el valor
        // crudo del llamante. La cota tiene que estar también aquí, no solo en el parser.
        var mensaje = GraphProjection.MensajeDeValorDesconocido(new string('y', 8000));

        Assert.True(mensaje.Length < 600, $"midió {mensaje.Length}");
        Assert.Contains("recortado", mensaje);
    }

    [Fact]
    public void Un_valor_corto_vuelve_intacto()
    {
        // La cota no puede estropear el caso normal: quien escribe una errata tiene que reconocer
        // su propia palabra, que es lo único para lo que sirve devolverla.
        Assert.False(GraphProjection.TryParse("permisions", out _, out var desconocido));

        Assert.Equal("permisions", desconocido);
        Assert.DoesNotContain("recortado", desconocido);
    }

    // ── Lectura de una proyección ausente = grafo completo ───────────────────────

    [Fact]
    public void Una_proyeccion_nula_lo_lleva_todo()
    {
        // Es lo que hace que I1 sea cierta por construcción: el código de armado y el de
        // serialización preguntan siempre lo mismo, y ante `null` la respuesta es siempre «sí».
        GraphProjection? ninguna = null;

        Assert.True(ninguna.LlevaPermisos());
        Assert.True(ninguna.LlevaNavegacion());
        Assert.True(ninguna.LlevaPerfiles());
        Assert.True(ninguna.LlevaBanderas());
        Assert.True(ninguna.LlevaConfig());
        Assert.True(ninguna.LlevaAjustes());
    }
}
