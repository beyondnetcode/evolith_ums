using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Evolith.Ums.Sdk.Authorization;
using Evolith.Ums.Sdk.Contracts;
using Xunit;

namespace Evolith.Ums.Sdk.Tests;

/// <summary>
/// Una sección AUSENTE no es una sección VACÍA, y el índice tiene que poder decir cuál de las dos
/// recibió.
///
/// <para>La guarda de nulos que introdujo la proyección arreglaba media cosa: evitaba la
/// <c>NullReferenceException</c> en la primera comprobación de permiso, pero colapsaba <c>null</c>
/// a <c>[]</c> y con eso volvía indistinguibles «no me la mandaron» y «está vacía» justo en la
/// estructura que decide permisos. El modo de fallo pasaba de ruidoso a MUDO, que en autorización
/// es peor: un satélite que pidiera <c>?include=navigation</c> por error y luego preguntara por un
/// permiso de dominio recibía un «no concedido» perfectamente plausible y ni una pista de que la
/// pregunta nunca llegó a hacerse.</para>
///
/// <para>El grafo de prueba se fabrica QUITANDO CLAVES de un fixture dorado y deserializando el
/// resultado, no construyendo el record con <c>null</c> a mano. Es la reproducción fiel: el
/// <c>null</c> no lo pone nadie a propósito, lo deja System.Text.Json ante una clave ausente pese a
/// que el record declare la colección como no anulable.</para>
/// </summary>
[Collection("Diagnostico de secciones")]
public sealed class GraphSectionDiagnosticsTests : IDisposable
{
    private readonly List<string> _avisos = [];
    private readonly bool _estrictoOriginal = UmsGraphDiagnostics.ThrowOnAbsentSection;

    public GraphSectionDiagnosticsTests()
    {
        UmsGraphDiagnostics.ThrowOnAbsentSection = false;
        UmsGraphDiagnostics.SectionAbsent += Anotar;
    }

    public void Dispose()
    {
        UmsGraphDiagnostics.SectionAbsent -= Anotar;
        UmsGraphDiagnostics.ThrowOnAbsentSection = _estrictoOriginal;
    }

    private void Anotar(object? _, GraphSectionAbsentEventArgs e) => _avisos.Add(e.SectionName);

    private static readonly IAuthorizationValidator Validador = new AuthorizationValidator();

    /// <summary>
    /// El fixture dorado tal como llegaría PROYECTADO con <c>?include=navigation</c>: sin
    /// <c>domainPermissions</c>, sin <c>scopes</c> y sin <c>featureFlags</c>.
    /// </summary>
    private static AuthorizationGraph GrafoProyectado()
    {
        var raiz = JsonNode.Parse(FixtureLoader.LoadRaw("local-auth-success"))!.AsObject();

        raiz.Remove("domainPermissions");
        raiz.Remove("scopes");
        raiz.Remove("featureFlags");
        raiz["schemaVersion"] = SchemaVersion.Projected;
        raiz["projection"] = new JsonArray("navigation");

        return Vigente(JsonSerializer.Deserialize<AuthorizationGraph>(raiz.ToJsonString())!);
    }

    /// <summary>El mismo fixture, COMPLETO, con esas secciones legítimamente vacías.</summary>
    private static AuthorizationGraph GrafoCompletoYVacio()
    {
        var raiz = JsonNode.Parse(FixtureLoader.LoadRaw("local-auth-success"))!.AsObject();

        raiz["domainPermissions"] = new JsonArray();
        raiz["scopes"]            = new JsonArray();
        raiz["featureFlags"]      = new JsonArray();

        return Vigente(JsonSerializer.Deserialize<AuthorizationGraph>(raiz.ToJsonString())!);
    }

    /// <summary>Los fixtures caducan; aquí se prueba la ausencia de secciones, no la expiración.</summary>
    private static AuthorizationGraph Vigente(AuthorizationGraph g) =>
        g with { ValidUntil = DateTimeOffset.UtcNow.AddHours(1) };

    [Fact]
    public void El_grafo_proyectado_llega_con_las_secciones_a_null()
    {
        // Que la premisa de todo lo demás sea cierta: no hace falta construir nada raro para que
        // una colección declarada NO anulable acabe siendo null. Basta que falte la clave.
        var grafo = GrafoProyectado();

        grafo.DomainPermissions.Should().BeNull();
        grafo.Scopes.Should().BeNull();
        grafo.FeatureFlags.Should().BeNull();
        grafo.MenuAccess.Should().NotBeNull("`navigation` sí se pidió");
    }

    [Fact]
    public void La_ausencia_se_tolera_no_tumba_el_proceso()
    {
        // La mitad que ya funcionaba y que hay que conservar: el orden de despliegue no se puede
        // garantizar entre organizaciones, y un SDK que reviente ante un grafo recortado convierte
        // un `curl` con `?include=` contra un entorno compartido en una caída ajena.
        var decision = Validador.RequireDomainAccess(GrafoProyectado(), "PURCHASE_ORDER", "APPROVE");

        decision.IsGranted.Should().BeFalse("fail-closed: sin la sección no se concede nada");
    }

    [Fact]
    public void La_ausencia_queda_REGISTRADA_con_el_nombre_de_la_seccion()
    {
        // EL defecto que esta prueba cierra. Tolerar en silencio hacía que «no me la mandaron» y
        // «está vacía» fueran el mismo estado observable, y solo uno de los dos justifica concluir
        // que no hay ninguna denegación explícita.
        Validador.RequireDomainAccess(GrafoProyectado(), "PURCHASE_ORDER", "APPROVE");

        _avisos.Should().Contain(GraphSections.DomainPermissions);
        _avisos.Should().Contain(GraphSections.Scopes);
        _avisos.Should().Contain(GraphSections.FeatureFlags);

        // Y no al revés: lo que SÍ llegó no se reporta.
        _avisos.Should().NotContain(GraphSections.MenuAccess);
    }

    [Fact]
    public void Una_seccion_legitimamente_VACIA_no_se_reporta()
    {
        // El falso positivo que dejaría el aviso inservible. Un grafo lobby trae todas las
        // secciones vacías y es perfectamente normal: si vacío avisara, nadie leería los avisos.
        Validador.RequireDomainAccess(GrafoCompletoYVacio(), "PURCHASE_ORDER", "APPROVE");

        _avisos.Should().BeEmpty();
    }

    [Fact]
    public void Quien_prefiera_caerse_a_autorizar_a_ciegas_puede_pedirlo()
    {
        // La salida dura, opt-in. Existe porque «tolerar» es la respuesta correcta por defecto pero
        // no para todo el mundo: un satélite que NECESITA `domainPermissions` prefiere una
        // excepción tipada a un 403 que parece una decisión legítima.
        var grafo = GrafoProyectado();
        UmsGraphDiagnostics.ThrowOnAbsentSection = true;

        var fallo = Assert.Throws<GraphSectionMissingException>(
            () => Validador.RequireDomainAccess(grafo, "PURCHASE_ORDER", "APPROVE"));

        // El nombre de la sección viaja en el TIPO, no solo en el texto: es lo que permite decidir
        // programáticamente si esa ausencia concreta importa para esta comprobación.
        fallo.SectionName.Should().Be(GraphSections.DomainPermissions);
        fallo.Message.Should().Contain(GraphSections.DomainPermissions);
    }
}
