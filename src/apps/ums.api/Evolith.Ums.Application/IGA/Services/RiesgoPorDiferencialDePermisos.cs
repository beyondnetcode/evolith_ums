namespace Evolith.Ums.Application.IGA.Services;

using Evolith.Ums.Domain.Authorization;
using Evolith.Ums.Domain.Enums;
using ModuleEntity = Evolith.Ums.Domain.Authorization.SystemSuite.Module.Module;
using NodeEntity = Evolith.Ums.Domain.Authorization.SystemSuite.MenuNode.MenuNode;

/// <summary>
/// Calcula el RiskScore de una promoción por lo que la persona GANA, no por dónde queda en el
/// organigrama (<c>iga-risk-graph-v2</c>, FR-061, ADR-UMS-093).
///
/// <para>Sustituye a <see cref="HeuristicRiskScoreCalculator"/>, cuya v1 declaraba usar el nivel
/// jerárquico como «proxy de permisos nuevos». El proxy no era impreciso: estaba INVERTIDO. La spec
/// del Tablero numera el rol más alto como nivel 0 y la fórmula asumía que un número mayor es más
/// sensible, de modo que —medido el 2026-08-21 sobre los 56 pares de esa suite— las cinco
/// promociones más «arriesgadas» eran las que llevaban al rol más JUNIOR (105 sobre 100) y
/// convertirse en administrador puntuaba **15**, por debajo del umbral de revisión reforzada. El
/// control existía, estaba cableado, y dejaba pasar exactamente aquello para lo que existe.</para>
///
/// <para>Aquí el riesgo es el <b>diferencial de ESCRITURAS</b> entre el rol de destino y el de
/// origen, medido en scopes reales del catálogo. Leer de más no es un riesgo del mismo orden que
/// escribir de más, y contar ambos por igual diluye justo lo que se quiere ver.</para>
///
/// <para>Y añade la única combinación tóxica que esta suite declara de verdad: <b>quien edita no
/// certifica</b>. Un rol que pueda redactar un artefacto y aprobarlo salta de banda, no suma un
/// punto: es un cambio de naturaleza, no de grado — el control que define al aprobador deja de
/// existir para esa persona.</para>
/// </summary>
public sealed class RiesgoPorDiferencialDePermisos : IRiskScoreCalculator
{
    public const string ModelVersion = "iga-risk-graph-v2";

    private const int PesoBase = 10;
    /// <summary>Techo del factor de escrituras nuevas. Con la banda tóxica, el total llega a 100.</summary>
    private const int PesoMaximoDeEscrituras = 60;

    /// <summary>
    /// Romper la separación entre editar y aprobar basta, por sí solo, para exigir revisión
    /// reforzada: con la base son 70, que es el umbral.
    ///
    /// <para>Estuvo en 30 durante medio día y ahí se vio el problema: `COORDINADOR → ADMIN_SDLC`
    /// puntuaba 66 —rompía la separación y ganaba 21 escrituras— y no cruzaba, de modo que el peso
    /// convertía un cambio de naturaleza en una cuestión de cuánto se gana. Si la separación que
    /// define al aprobador deja de existir para alguien, que además gane mucho o poco no debería
    /// decidir si eso se revisa.</para>
    /// </summary>
    private const int PesoDeCombinacionToxica = 60;

    /// <summary>Acciones que LEEN. Todo lo demás escribe, y es lo que este cálculo mide.</summary>
    private static readonly HashSet<string> AccionesDeLectura =
        new(StringComparer.OrdinalIgnoreCase) { "view", "search", "export" };

    /// <summary>Editar el artefacto y resolver su aprobación: las dos mitades que no deben juntarse.</summary>
    private static readonly HashSet<string> EscrituraDeArtefacto =
        new(StringComparer.OrdinalIgnoreCase) { "art_editor.update", "art_editor.upload" };

    private static readonly HashSet<string> AprobacionDeArtefacto =
        new(StringComparer.OrdinalIgnoreCase) { "art_cert.certify", "art_revision.approve" };

    private readonly IRoleRepository _roles;
    private readonly IPermissionTemplateRepository _plantillas;
    private readonly ISystemSuiteRepository _suites;

    public RiesgoPorDiferencialDePermisos(
        IRoleRepository roles,
        IPermissionTemplateRepository plantillas,
        ISystemSuiteRepository suites)
    {
        _roles = roles;
        _plantillas = plantillas;
        _suites = suites;
    }

    public async Task<Result<RiskAssessment>> CalculateAsync(
        RolePromotionRiskContext context,
        CancellationToken cancellationToken = default)
    {
        var rolActual = await _roles.GetByIdAsync(context.CurrentRoleId, cancellationToken);
        if (rolActual is null)
            return Result<RiskAssessment>.Failure("No se encontró el rol actual para calcular el RiskScore.");

        var rolObjetivo = await _roles.GetByIdAsync(context.TargetRoleId, cancellationToken);
        if (rolObjetivo is null)
            return Result<RiskAssessment>.Failure("No se encontró el rol objetivo para calcular el RiskScore.");

        var suite = await _suites.GetByIdAsync(rolObjetivo.Props.SystemSuiteId.GetValue(), cancellationToken);
        if (suite is null)
            return Result<RiskAssessment>.Failure("No se encontró la suite del rol objetivo para calcular el RiskScore.");

        var nodos = IndicePorId(suite.Modules);
        var acciones = suite.Actions.ToDictionary(a => a.GetId().GetValue(), a => a.Code.GetValue());

        var origen = await EscriturasDeAsync(context.TenantId, rolActual.GetId().GetValue(),
            rolActual.Props.SystemSuiteId.GetValue(), nodos, acciones, cancellationToken);
        var destino = await EscriturasDeAsync(context.TenantId, rolObjetivo.GetId().GetValue(),
            rolObjetivo.Props.SystemSuiteId.GetValue(), nodos, acciones, cancellationToken);

        var nuevas = destino.Except(origen, StringComparer.OrdinalIgnoreCase).ToList();

        var factores = new List<RiskFactor>
        {
            new("base", PesoBase, "Riesgo base de toda promoción de rol."),
        };

        // La superficie de escritura de la suite es el denominador: 20 escrituras nuevas no pesan lo
        // mismo en un sistema que tiene 25 que en uno que tiene 500.
        var superficie = SuperficieDeEscritura(suite.Modules);
        var pesoEscrituras = superficie == 0
            ? 0
            : Math.Min(PesoMaximoDeEscrituras,
                (int)Math.Ceiling(nuevas.Count * (double)PesoMaximoDeEscrituras / superficie));

        factores.Add(new RiskFactor(
            "escrituras_nuevas",
            pesoEscrituras,
            $"{nuevas.Count} escritura(s) que el rol objetivo concede y el actual no, sobre una superficie de {superficie}."));

        // El factor es DIFERENCIAL, como todo lo demás de este modelo: pesa que la promoción ROMPA
        // la separación, no que el destino la tenga rota. A quien ya podía editar y aprobar, llegar
        // a otro rol que también puede no le quita ningún control: no hay nada nuevo que revisar, y
        // cobrárselo otra vez inflaría el número justo donde no cambia nada.
        var rompe = Rompe(destino) && !Rompe(origen);

        if (rompe)
        {
            factores.Add(new RiskFactor(
                "combinacion_toxica_editar_aprobar",
                PesoDeCombinacionToxica,
                "La promoción une redactar un artefacto y resolver su aprobación: la separación que define al aprobador deja de existir para esta persona."));
        }

        var score = Math.Clamp(factores.Sum(f => f.Weight), RiskScore.Min, RiskScore.Max);
        return Result<RiskAssessment>.Success(new RiskAssessment(score, ModelVersion, factores));
    }

    /// <summary>Si el reparto reúne las dos mitades que no deben juntarse.</summary>
    private static bool Rompe(IReadOnlySet<string> escrituras)
        => escrituras.Any(EscrituraDeArtefacto.Contains) && escrituras.Any(AprobacionDeArtefacto.Contains);

    /// <summary>Los scopes de ESCRITURA que concede la plantilla viva del rol.</summary>
    private async Task<HashSet<string>> EscriturasDeAsync(
        Guid tenantId,
        Guid roleId,
        Guid suiteId,
        IReadOnlyDictionary<Guid, string> nodos,
        IReadOnlyDictionary<Guid, string> acciones,
        CancellationToken ct)
    {
        var candidatas = await _plantillas.GetByTenantRoleSuiteAsync(tenantId, roleId, suiteId, ct);

        // La MAYOR versión publicada, no la primera que aparezca. Una plantilla publicada no admite
        // ítems nuevos, así que corregir permisos crea otra versión: quedarse con la vieja mide un
        // reparto que ya no existe. Es el mismo error que costó una tarde en el cargador.
        //
        // Se ordena por el VALUE OBJECT, que compara major/minor/patch como números. Comparar la
        // cadena tal cual funciona hasta la décima revisión y entonces miente en silencio: por
        // orden lexicográfico «0.10.0» va ANTES que «0.9.0», así que el cálculo volvería a medir un
        // reparto derogado justo en los roles que más se han retocado.
        var plantilla = candidatas
            .Where(p => p.Status == TemplateStatus.Published)
            .MaxBy(p => p.Version);

        var escrituras = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (plantilla is null) return escrituras;

        foreach (var item in plantilla.ActiveItems.Where(i => i.IsAllowed && !i.IsDenied))
        {
            if (!nodos.TryGetValue(item.TargetId.GetValue(), out var codigoNodo)) continue;
            if (!acciones.TryGetValue(item.ActionId.GetValue(), out var codigoAccion)) continue;
            if (AccionesDeLectura.Contains(codigoAccion)) continue;

            escrituras.Add($"{codigoNodo.ToLowerInvariant()}.{codigoAccion.ToLowerInvariant()}");
        }

        return escrituras;
    }

    /// <summary>
    /// Cuántos scopes de escritura EXISTEN en la suite: el denominador del factor.
    ///
    /// <para>Son los pares (nodo, acción) realmente vinculados, no el producto de nodos por
    /// acciones: el catálogo enlaza cada opción con las acciones que le corresponden, y multiplicar
    /// daría una superficie imaginaria que hunde el peso de cualquier promoción.</para>
    /// </summary>
    private static int SuperficieDeEscritura(IEnumerable<ModuleEntity> modulos)
    {
        var pares = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Recorrer(IEnumerable<NodeEntity> nodos)
        {
            foreach (var n in nodos)
            {
                foreach (var codigo in n.ActionCodes.Select(a => a.GetValue()))
                {
                    if (AccionesDeLectura.Contains(codigo)) continue;
                    pares.Add($"{n.Code.GetValue().ToLowerInvariant()}.{codigo.ToLowerInvariant()}");
                }
                Recorrer(n.Children);
            }
        }

        foreach (var m in modulos) Recorrer(m.Nodes);
        return pares.Count;
    }

    /// <summary>Aplana el árbol de navegación: id de nodo → código, sin presuponer niveles.</summary>
    private static Dictionary<Guid, string> IndicePorId(IEnumerable<ModuleEntity> modulos)
    {
        var indice = new Dictionary<Guid, string>();

        void Recorrer(IEnumerable<NodeEntity> nodos)
        {
            foreach (var n in nodos)
            {
                indice[n.GetId().GetValue()] = n.Code.GetValue();
                Recorrer(n.Children);
            }
        }

        foreach (var m in modulos) Recorrer(m.Nodes);
        return indice;
    }
}
