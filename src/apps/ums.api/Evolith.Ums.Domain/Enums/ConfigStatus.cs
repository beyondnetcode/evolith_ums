namespace Evolith.Ums.Domain.Enums;

public class ConfigStatus : DomainEnumeration
{
    public static readonly ConfigStatus Draft     = new(1, nameof(Draft));
    public static readonly ConfigStatus Published = new(2, nameof(Published));
    public static readonly ConfigStatus Archived  = new(3, nameof(Archived));

    /// <summary>
    /// Estado TERMINAL de borrado lógico. La fila nunca se elimina de la base: una configuración
    /// retirada hace un año debe seguir explicando por qué el sistema se comportó como se comportó,
    /// y las consultas históricas se harían imposibles si la fila desapareciera.
    ///
    /// Es un estado distinto de <see cref="Archived"/> a propósito: archivar es una transición
    /// legítima del ciclo de vida (la configuración sigue siendo visible y consultable), mientras
    /// que eliminar la oculta de toda lectura (GET → 404, fuera de listados). Espejo de
    /// <c>UserStatus.Deleted</c>, el patrón de soft-delete que ya existe en Identity.
    /// </summary>
    public static readonly ConfigStatus Deleted   = new(4, nameof(Deleted));

    private ConfigStatus(int id, string name) : base(id, name) { }
}
