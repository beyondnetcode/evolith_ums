namespace Evolith.Ums.Application.Test.IGA;

using Moq;
using Evolith.Ums.Application.IGA.Services;
using Evolith.Ums.Domain.Authorization;
using Evolith.Ums.Domain.Authorization.SystemSuite.MenuNode;
using Evolith.Ums.Domain.Enums;
using Evolith.Ums.Domain.Kernel;
using RoleAggregate = Evolith.Ums.Domain.Authorization.Role.Role;
using RolePromotionRequestAggregate = Evolith.Ums.Domain.IGA.RolePromotionRequest.RolePromotionRequest;
using SuiteAggregate = Evolith.Ums.Domain.Authorization.SystemSuite.SystemSuite;
using TemplateAggregate = Evolith.Ums.Domain.Authorization.Template.PermissionTemplate;

/// <summary>
/// El cálculo v2 mide lo que se GANA, no dónde queda uno en el organigrama.
///
/// <para>La prueba que da nombre a este fichero es la de la INVERSIÓN: con la heurística v1,
/// «auditor de solo lectura → administrador con todas las escrituras» puntuaba 15 y una promoción
/// menor puntuaba 20. Un número que ordena al revés es peor que no tener número, porque se usa.</para>
/// </summary>
public class RiesgoPorDiferencialDePermisosTests
{
    private static readonly Guid Inquilino = Guid.NewGuid();
    private static readonly ActorId Actor = ActorId.Create("prueba");

    private readonly Mock<IRoleRepository> _roles = new();
    private readonly Mock<IPermissionTemplateRepository> _plantillas = new();
    private readonly Mock<ISystemSuiteRepository> _suites = new();

    /// <summary>Una suite con un nodo por capacidad y las acciones que el Tablero usa de verdad.</summary>
    private static (SuiteAggregate suite, Dictionary<string, Guid> nodos, Dictionary<string, Guid> acciones) Catalogo()
    {
        var suite = SuiteAggregate.Create(
            TenantId.Load(Inquilino), Code.Create("SDLC"), Name.Create("Tablero"),
            Description.Create("Suite de prueba"), Actor).Value;

        foreach (var (codigo, nombre) in new[] { ("VIEW", "Ver"), ("UPDATE", "Actualizar"), ("CERTIFY", "Certificar"), ("EXECUTE", "Ejecutar") })
            suite.RegisterAction(ActionCode.Create(codigo), Name.Create(nombre), Actor);

        suite.AddModule(Code.Create("PRD"), Name.Create("Iniciativas"), Description.Create("x"), 1, Actor);
        var modulo = suite.Modules.First();
        suite.ActivateModule(modulo.Props.Id, Actor);

        var nodos = new Dictionary<string, Guid>();
        var orden = 1;
        foreach (var (codigo, acciones) in new[]
                 {
                     ("ART_EDITOR", new[] { "VIEW", "UPDATE" }),
                     ("ART_CERT", new[] { "VIEW", "CERTIFY" }),
                     ("PLAN_HITOS", new[] { "VIEW", "UPDATE", "EXECUTE" }),
                 })
        {
            suite.AddModuleRootNode(modulo.Props.Id, NodeKind.Option, Code.Create(codigo), Name.Create(codigo),
                Description.Create(codigo), orden++, Actor);
            var nodo = modulo.Nodes.First(n => n.Code.GetValue() == codigo);
            foreach (var a in acciones) suite.LinkModuleNodeAction(modulo.Props.Id, nodo.GetId(), ActionCode.Create(a), Actor);
            nodos[codigo] = nodo.GetId().GetValue();
        }

        var accionesPorCodigo = suite.Actions.ToDictionary(a => a.Code.GetValue(), a => a.GetId().GetValue());
        return (suite, nodos, accionesPorCodigo);
    }

    /// <summary>
    /// Un rol raíz (nivel 0) o colgado de un padre. El nivel importa solo para poder EXHIBIR la
    /// inversión de la v1: la v2 no lo mira, y esa es justamente la tesis.
    /// </summary>
    private static RoleAggregate Rol(SuiteAggregate suite, string codigo, int nivel = 0, RoleAggregate? padre = null)
        => RoleAggregate.Create(TenantId.Load(Inquilino), suite.GetId(), Code.Create(codigo), Name.Create(codigo),
            Description.Create(codigo), padre?.GetId(), nivel, 0, Actor).Value;

    private static TemplateAggregate Plantilla(SuiteAggregate suite, RoleAggregate rol,
        Dictionary<string, Guid> nodos, Dictionary<string, Guid> acciones, params (string nodo, string accion)[] concesiones)
    {
        var t = TemplateAggregate.Create(TenantId.Load(Inquilino), rol.GetId(), suite.GetId(), Actor).Value;
        foreach (var (nodo, accion) in concesiones)
            t.AddItem(ExclusiveArcTarget.Option, IdValueObject.Load(nodos[nodo]), ActionId.Load(acciones[accion]), true, false, Actor);
        t.Publish(Actor);
        return t;
    }

    private RiesgoPorDiferencialDePermisos Calculadora() =>
        new(_roles.Object, _plantillas.Object, _suites.Object);

    private void Preparar(SuiteAggregate suite, RoleAggregate origen, RoleAggregate destino,
        TemplateAggregate plantillaOrigen, TemplateAggregate plantillaDestino)
    {
        _suites.Setup(s => s.GetByIdAsync(suite.GetId().GetValue(), It.IsAny<CancellationToken>())).ReturnsAsync(suite);
        _roles.Setup(r => r.GetByIdAsync(origen.GetId().GetValue(), It.IsAny<CancellationToken>())).ReturnsAsync(origen);
        _roles.Setup(r => r.GetByIdAsync(destino.GetId().GetValue(), It.IsAny<CancellationToken>())).ReturnsAsync(destino);
        _plantillas.Setup(p => p.GetByTenantRoleSuiteAsync(Inquilino, origen.GetId().GetValue(), suite.GetId().GetValue(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { plantillaOrigen });
        _plantillas.Setup(p => p.GetByTenantRoleSuiteAsync(Inquilino, destino.GetId().GetValue(), suite.GetId().GetValue(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { plantillaDestino });
    }

    [Fact]
    public async Task La_promocion_que_mas_gana_puntua_mas_que_la_que_gana_poco()
    {
        // ESTA es la regresión que encontramos: con la v1 el orden estaba invertido —15 contra 20—,
        // así que la promoción más grande de la suite se saltaba la revisión de seguridad y una
        // lateral la exigía.
        var (suite, nodos, acciones) = Catalogo();

        var auditor = Rol(suite, "AUDITOR");
        var admin = Rol(suite, "ADMIN");
        // Cuelgan del administrador para que la v1 vea un salto de nivel donde la v2 ve una
        // promoción menor: 3 → 2 es un peldaño; auditor → administrador, ninguno.
        var jefe = Rol(suite, "JEFE", 2, admin);
        var desarrollador = Rol(suite, "DESARROLLADOR", 3, admin);

        var soloLectura = Plantilla(suite, auditor, nodos, acciones, ("ART_EDITOR", "VIEW"), ("ART_CERT", "VIEW"));
        var todo = Plantilla(suite, admin, nodos, acciones,
            ("ART_EDITOR", "UPDATE"), ("ART_CERT", "CERTIFY"), ("PLAN_HITOS", "UPDATE"), ("PLAN_HITOS", "EXECUTE"));
        var escribeArtefacto = Plantilla(suite, desarrollador, nodos, acciones, ("ART_EDITOR", "UPDATE"));
        var escribeArtefactoYPlan = Plantilla(suite, jefe, nodos, acciones, ("ART_EDITOR", "UPDATE"), ("PLAN_HITOS", "UPDATE"));

        Preparar(suite, auditor, admin, soloLectura, todo);
        var grande = await Calculadora().CalculateAsync(
            new RolePromotionRiskContext(Inquilino, Guid.NewGuid(), auditor.GetId().GetValue(), admin.GetId().GetValue()));

        _roles.Reset(); _plantillas.Reset(); _suites.Reset();
        Preparar(suite, desarrollador, jefe, escribeArtefacto, escribeArtefactoYPlan);
        var pequena = await Calculadora().CalculateAsync(
            new RolePromotionRiskContext(Inquilino, Guid.NewGuid(), desarrollador.GetId().GetValue(), jefe.GetId().GetValue()));

        Assert.True(grande.IsSuccess);
        Assert.True(pequena.IsSuccess);
        Assert.True(grande.Value.Score > pequena.Value.Score,
            $"la promoción a administrador ({grande.Value.Score}) debe puntuar MÁS que la lateral ({pequena.Value.Score})");
    }

    [Fact]
    public async Task Ganar_solo_lecturas_no_cuenta_como_riesgo_de_escritura()
    {
        // Leer de más no es del mismo orden que escribir de más. Si contaran igual, el factor se
        // diluye justo donde se quiere afilar.
        var (suite, nodos, acciones) = Catalogo();
        var uno = Rol(suite, "UNO");
        var otro = Rol(suite, "OTRO");
        var conUnaLectura = Plantilla(suite, uno, nodos, acciones, ("ART_EDITOR", "VIEW"));
        var conTresLecturas = Plantilla(suite, otro, nodos, acciones, ("ART_EDITOR", "VIEW"), ("ART_CERT", "VIEW"), ("PLAN_HITOS", "VIEW"));

        Preparar(suite, uno, otro, conUnaLectura, conTresLecturas);
        var r = await Calculadora().CalculateAsync(
            new RolePromotionRiskContext(Inquilino, Guid.NewGuid(), uno.GetId().GetValue(), otro.GetId().GetValue()));

        Assert.True(r.IsSuccess);
        Assert.Equal(0, r.Value.Factors.Single(f => f.Code == "escrituras_nuevas").Weight);
    }

    [Fact]
    public async Task Editar_y_aprobar_el_mismo_artefacto_salta_de_banda()
    {
        // El control que define al aprobador: quien edita no certifica. Un rol que pueda las dos
        // cosas no es «un poco más arriesgado», es de otra naturaleza.
        var (suite, nodos, acciones) = Catalogo();
        var equipo = Rol(suite, "EQUIPO");
        var ambas = Rol(suite, "AMBAS");
        var soloEdita = Plantilla(suite, equipo, nodos, acciones, ("ART_EDITOR", "UPDATE"));
        var editaYCertifica = Plantilla(suite, ambas, nodos, acciones, ("ART_EDITOR", "UPDATE"), ("ART_CERT", "CERTIFY"));

        Preparar(suite, equipo, ambas, soloEdita, editaYCertifica);
        var r = await Calculadora().CalculateAsync(
            new RolePromotionRiskContext(Inquilino, Guid.NewGuid(), equipo.GetId().GetValue(), ambas.GetId().GetValue()));

        Assert.True(r.IsSuccess);
        Assert.Contains(r.Value.Factors, f => f.Code == "combinacion_toxica_editar_aprobar");
    }

    [Fact]
    public async Task A_quien_ya_editaba_y_aprobaba_no_se_le_cobra_dos_veces()
    {
        // El factor pesa que la promoción ROMPA la separación, no que el destino la tenga rota:
        // a quien ya podía las dos cosas, llegar a otro rol que también puede no le quita ningún
        // control, y cobrárselo inflaría el número justo donde nada cambia.
        var (suite, nodos, acciones) = Catalogo();
        var ambasA = Rol(suite, "AMBASA");
        var ambasB = Rol(suite, "AMBASB");
        var origen = Plantilla(suite, ambasA, nodos, acciones, ("ART_EDITOR", "UPDATE"), ("ART_CERT", "CERTIFY"));
        var destino = Plantilla(suite, ambasB, nodos, acciones,
            ("ART_EDITOR", "UPDATE"), ("ART_CERT", "CERTIFY"), ("PLAN_HITOS", "UPDATE"));

        Preparar(suite, ambasA, ambasB, origen, destino);
        var r = await Calculadora().CalculateAsync(
            new RolePromotionRiskContext(Inquilino, Guid.NewGuid(), ambasA.GetId().GetValue(), ambasB.GetId().GetValue()));

        Assert.True(r.IsSuccess);
        Assert.DoesNotContain(r.Value.Factors, f => f.Code == "combinacion_toxica_editar_aprobar");
    }

    [Fact]
    public async Task Romper_editar_y_aprobar_basta_para_exigir_revision_reforzada()
    {
        // La decisión de calibrado del 2026-08-21: si la separación deja de existir para alguien,
        // que además gane mucho o poco no debería decidir si eso se revisa. Con la base, el factor
        // solo ya alcanza el umbral por defecto.
        var (suite, nodos, acciones) = Catalogo();
        var equipo = Rol(suite, "EQUIPO2");
        var ambas = Rol(suite, "AMBAS2");
        var soloEdita = Plantilla(suite, equipo, nodos, acciones, ("ART_EDITOR", "UPDATE"));
        var editaYCertifica = Plantilla(suite, ambas, nodos, acciones, ("ART_EDITOR", "UPDATE"), ("ART_CERT", "CERTIFY"));

        Preparar(suite, equipo, ambas, soloEdita, editaYCertifica);
        var r = await Calculadora().CalculateAsync(
            new RolePromotionRiskContext(Inquilino, Guid.NewGuid(), equipo.GetId().GetValue(), ambas.GetId().GetValue()));

        Assert.True(r.IsSuccess);
        Assert.True(r.Value.Score >= RolePromotionRequestAggregate.DefaultHighRiskThreshold,
            $"romper la separación puntúa {r.Value.Score} y el umbral por defecto es "
            + $"{RolePromotionRequestAggregate.DefaultHighRiskThreshold}");
    }

    [Fact]
    public async Task El_modelo_se_identifica_para_poder_comparar_corridas()
    {
        var (suite, nodos, acciones) = Catalogo();
        var a = Rol(suite, "A");
        var b = Rol(suite, "B");
        var pa = Plantilla(suite, a, nodos, acciones, ("ART_EDITOR", "VIEW"));
        var pb = Plantilla(suite, b, nodos, acciones, ("ART_EDITOR", "UPDATE"));

        Preparar(suite, a, b, pa, pb);
        var r = await Calculadora().CalculateAsync(
            new RolePromotionRiskContext(Inquilino, Guid.NewGuid(), a.GetId().GetValue(), b.GetId().GetValue()));

        Assert.Equal("iga-risk-graph-v2", r.Value.RiskModelVersion);
    }
}
