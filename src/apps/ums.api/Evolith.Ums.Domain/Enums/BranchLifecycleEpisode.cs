namespace Evolith.Ums.Domain.Enums;

/// <summary>
/// Episodio del ciclo de vida de una sucursal: cada transición que abre o cierra una ÉPOCA de
/// operación.
///
/// Existe porque una sucursal no es un hecho puntual sino una serie de épocas: abre, deja de operar,
/// vuelve a operar con otra dirección y otro responsable, y un día cierra definitivamente. Con un
/// único <c>bool IsActive</c> y una sola marca de auditoría, esas épocas son indistinguibles: una
/// auditoría de 2028 sobre un despacho de 2024 vería la dirección de hoy y creería que es la de
/// entonces. Ese es exactamente el tipo de pregunta —«¿quién pudo hacer esto en marzo, y por qué?»—
/// que ADR-0164 §1.2 obliga a poder responder.
///
/// Los identificadores son estables porque se persisten (columna <c>EpisodeId</c>): añadir episodios
/// nuevos es seguro, renumerar los existentes reescribiría la historia.
/// </summary>
public class BranchLifecycleEpisode : DomainEnumeration
{
    /// <summary>Apertura: la sucursal se da de alta y empieza a operar.</summary>
    public static readonly BranchLifecycleEpisode Opened = new(1, nameof(Opened));

    /// <summary>Cierre TEMPORAL y reversible: la sucursal deja de operar pero sigue existiendo.</summary>
    public static readonly BranchLifecycleEpisode Deactivated = new(2, nameof(Deactivated));

    /// <summary>Reapertura: la sucursal vuelve a operar. Abre una época nueva.</summary>
    public static readonly BranchLifecycleEpisode Reactivated = new(3, nameof(Reactivated));

    /// <summary>
    /// Cierre DEFINITIVO (estado terminal, ADR-0164 §2.1). No se revierte y no admite reapertura.
    /// Se numera 4 por simetría con <c>UserStatus.Deleted</c> y <c>TemplateStatus.Deleted</c>, el
    /// patrón de borrado lógico ya establecido en el repositorio.
    /// </summary>
    public static readonly BranchLifecycleEpisode Closed = new(4, nameof(Closed));

    private BranchLifecycleEpisode(int id, string name) : base(id, name) { }
}
