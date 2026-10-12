namespace Evolith.Ums.Domain.Enums;

public class TemplateStatus : DomainEnumeration
{
    public static readonly TemplateStatus Draft = new(1, nameof(Draft));
    public static readonly TemplateStatus Published = new(2, nameof(Published));
    public static readonly TemplateStatus Deprecated = new(3, nameof(Deprecated));

    /// <summary>
    /// Estado TERMINAL de borrado lógico. Solo existe borrado lógico: la fila nunca se elimina de la
    /// base porque el negocio consulta datos históricos y una fila borrada de verdad se pierde para
    /// siempre.
    ///
    /// Es un estado NUEVO y no una reutilización de <see cref="Deprecated"/> a propósito: «deprecada»
    /// es un estado VIVO y visible —la plantilla sigue apareciendo en el catálogo y sigue impidiendo
    /// nuevas asignaciones— mientras que «eliminada» desaparece de toda lectura. Confundirlos borraría
    /// la diferencia entre «ya no se ofrece» y «se retiró»; el cargador del Tablero
    /// (src/provisioning/cargar-en-ums.mjs) distingue explícitamente 'Deprecated' al elegir la
    /// plantilla canónica de cada rol.
    ///
    /// Se numera 4 por simetría con <c>UserStatus.Deleted</c> (REC-16), el patrón de borrado lógico ya
    /// establecido en el repositorio. No requiere migración: <c>StatusId</c> ya es un entero libre.
    /// </summary>
    public static readonly TemplateStatus Deleted = new(4, nameof(Deleted));

    private TemplateStatus(int id, string name) : base(id, name) { }
}
