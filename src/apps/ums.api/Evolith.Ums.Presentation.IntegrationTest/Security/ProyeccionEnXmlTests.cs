using System.Xml.Linq;
using Evolith.Ums.Domain.Authorization.Graph;
using Evolith.Ums.Infrastructure.Authorization.Graph;

namespace Evolith.Ums.Presentation.IntegrationTest.Security;

/// <summary>
/// La proyección vale igual en XML que en JSON.
///
/// <para>Hoy <c>GET /client/graph</c> codifica la forma canónica en JSON directamente y no llega a
/// este serializador, así que sería fácil dejarlo fuera y no notar nada. Sería un error con fecha
/// de caducidad: en cuanto la proyección alcance a <c>/client/authenticate</c> —donde el formato
/// lo elige el INQUILINO, no el llamante— un serializador que ignorase la proyección le devolvería
/// al inquilino configurado en XML el grafo entero que ese mismo satélite recibe recortado en
/// JSON. Y la asimetría no se descubre leyendo el código: se descubre en producción, por el lado
/// que concede.</para>
///
/// <para>Lo que estas pruebas NO afirman: que el XML y la forma canónica sean equivalentes. No lo
/// son, y no por este cambio — al XML le faltan desde antes <c>schemaVersion</c>,
/// <c>onboardingPending</c>, <c>accessState</c>, <c>profiles</c>, <c>settings</c> y varios campos
/// más. Ese hueco es anterior y ajeno a la proyección; arreglarlo aquí mezclaría dos cambios de
/// contrato en uno.</para>
///
/// <para>SÍ afirman, y es lo que añadieron las últimas dos pruebas, que el eco no MIENTE sobre ese
/// hueco: <c>projection</c> lista lo que este serializador ha honrado de verdad, y los tokens que
/// no sabe servir se declaran en <c>unsupported</c> en vez de aparecer como si se hubieran
/// aplicado. Un eco que repitiera la petición diría «te di settings» sin mandarlos.</para>
/// </summary>
public sealed class ProyeccionEnXmlTests
{
    private static XElement Serializar(string? include)
    {
        GraphProjection.TryParse(include, out var proyeccion, out _).Should().BeTrue();

        var xml = new XmlAuthorizationGraphSerializer().Serialize(Grafo(proyeccion));
        return XDocument.Parse(xml).Root!;
    }

    [Fact]
    public void Sin_proyeccion_el_xml_es_el_de_siempre()
    {
        var raiz = Serializar(null);

        raiz.Element("projection").Should().BeNull("sin `include` no hay nada que ecoar");
        foreach (var seccion in new[]
                 { "context", "authentication", "actions", "menuAccess", "domainPermissions",
                   "featureFlags", "effectiveConfig", "scopes" })
        {
            raiz.Element(seccion).Should().NotBeNull($"'{seccion}' viaja siempre en la respuesta completa");
        }
    }

    [Fact]
    public void Con_navigation_solo_viajan_la_navegacion_y_el_sobre()
    {
        var raiz = Serializar("navigation");

        raiz.Element("actions").Should().NotBeNull();
        raiz.Element("menuAccess").Should().NotBeNull();

        // Omitidos, no vacíos: un `<domainPermissions/>` sin hijos y «no lo pedí» son lo mismo
        // para el evaluador de deny-wins, y solo uno de los dos es cierto.
        raiz.Element("domainPermissions").Should().BeNull();
        raiz.Element("scopes").Should().BeNull();
        raiz.Element("featureFlags").Should().BeNull();
        raiz.Element("effectiveConfig").Should().BeNull();

        // El sobre no se negocia tampoco aquí.
        raiz.Element("context").Should().NotBeNull();
        raiz.Element("authentication").Should().NotBeNull();
        raiz.Attribute("generatedAt").Should().NotBeNull();
        raiz.Attribute("validUntil").Should().NotBeNull();
    }

    [Fact]
    public void Permissions_es_atomico_tambien_en_xml()
    {
        var raiz = Serializar("permissions");

        raiz.Element("scopes").Should().NotBeNull();
        raiz.Element("domainPermissions").Should().NotBeNull();
        raiz.Element("menuAccess").Should().BeNull();
        raiz.Element("actions").Should().BeNull();
    }

    [Fact]
    public void El_eco_de_la_proyeccion_viaja_en_el_xml()
    {
        // Sin él, un consumidor de XML no puede distinguir «no lo pedí» de «este UMS no lo emite»
        // —que es justo la ambigüedad que la omisión introduce—. Y el XML tiene además el hueco
        // preexistente de secciones que nunca emitió: sin el eco, las dos ausencias se confunden.
        var raiz = Serializar("permissions,features");

        raiz.Element("projection")!.Elements("include").Select(e => e.Value)
            .Should().Equal("permissions", "features");

        // Todo lo pedido se honró, así que no hay nada que declarar aparte.
        raiz.Element("projection")!.Element("unsupported").Should().BeNull();
    }

    [Fact]
    public void El_eco_declara_lo_APLICADO_no_lo_pedido()
    {
        // EL defecto que esta prueba cierra. `settings` es un token válido del vocabulario, pero
        // este serializador no emite `settings` —nunca lo ha hecho—. Ecoarlo como aplicado sería
        // decirle al cliente «te di settings» y no mandárselos: compararía petición y eco, los
        // vería coincidir, y leería la ausencia como «la sección llegó vacía». Un fallo mudo.
        var raiz = Serializar("navigation,profiles,settings");
        var eco  = raiz.Element("projection")!;

        eco.Elements("include").Select(e => e.Value)
            .Should().Equal("navigation");   // el XML solo honra lo que sabe emitir

        // Y lo no honrado NO se calla: se declara. Un eco más corto que la petición, sin decir por
        // qué, obligaría al cliente a adivinar si le faltan bloques o si pidió mal.
        eco.Element("unsupported")!.Elements("include").Select(e => e.Value)
            .Should().Equal("profiles", "settings");

        // Coherencia con lo emitido: lo ecoado está, lo declarado sin honrar no aparece por su
        // cuenta — el XML nunca emitió esos elementos y sigue sin emitirlos.
        raiz.Element("menuAccess").Should().NotBeNull();
        raiz.Element("actions").Should().NotBeNull();
        raiz.Element("profiles").Should().BeNull();
        raiz.Element("settings").Should().BeNull();
    }

    [Fact]
    public void Una_proyeccion_que_el_xml_no_puede_honrar_lo_dice_en_vez_de_callarlo()
    {
        // Caso límite del anterior: TODO lo pedido cae fuera de lo que este serializador emite. El
        // eco queda vacío de `include` —que es la verdad: no se aplicó nada— y `unsupported` lo
        // explica. La alternativa, un `<projection><include>settings</include></projection>` sobre
        // una respuesta sin settings, es exactamente la mentira que esto evita.
        var eco = Serializar("settings").Element("projection")!;

        eco.Elements("include").Should().BeEmpty();
        eco.Element("unsupported")!.Elements("include").Select(e => e.Value)
            .Should().Equal("settings");
    }

    private static AuthorizationGraph Grafo(GraphProjection? proyeccion)
    {
        var suiteId = Guid.NewGuid();
        var opcion = new GraphNavigationNode(Guid.NewGuid(), "STOCK_VIEW", "Ver Stock", "Option", 1,
            null, "/inv/stock-view",
            [new GraphNodeAction("VIEW", AccessEffect.Allow, PermissionSource.Template)], []);
        var modulo = new GraphMenuModule(Guid.NewGuid(), "INV", "Inventario", 1, "Active", "package", [opcion]);

        var contexto = new GraphContext(
            new GraphUser(Guid.NewGuid(), "ana@evolith.example", "ana", "Ana Torres", "Active"),
            new GraphTenant(Guid.NewGuid(), "EVOLITH", "Evolith UMS", "Active", false),
            new GraphSystemSuite(suiteId, "WMS", "Almacén", "Active"),
            new GraphRole(Guid.NewGuid(), "OPERARIO", "Operario", 2, null),
            new GraphProfile(Guid.NewGuid(), "OrgWide", true),
            Branch: null);

        return AuthorizationGraph.Build(
            contexto,
            new GraphAuthentication("Local", null, false, DateTime.UtcNow, DateTime.UtcNow.AddHours(1)),
            [new GraphAction(Guid.NewGuid(), "VIEW", "Ver")],
            [modulo],
            [new GraphDomainPermission(Guid.NewGuid(), "Aggregate", "PO", "Orden", null, null,
                [new GraphDomainAction(Guid.NewGuid(), "VIEW", "Ver", AccessEffect.Allow, PermissionSource.Template)])],
            [new GraphFeatureFlag("WMS_BULK", suiteId, false, null)],
            new GraphEffectiveConfig(60, 5, 12, true, ["Totp"], 3600000, false),
            ["stock_view.view"],
            DateTime.UtcNow,
            projection: proyeccion);
    }
}
