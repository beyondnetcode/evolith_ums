namespace Evolith.Ums.Domain.Enums;

public class SystemStatus : DomainEnumeration
{
    public static readonly SystemStatus Active = new(1, nameof(Active));
    public static readonly SystemStatus Maintenance = new(2, nameof(Maintenance));

    /// <summary>Archivado: el sistema ya no está en servicio, pero sigue en el catálogo y se lee.</summary>
    public static readonly SystemStatus Deprecated = new(3, nameof(Deprecated));

    /// <summary>
    /// Eliminado lógicamente. Estado TERMINAL: de aquí no se sale.
    ///
    /// <para>Es el análogo de <see cref="UserStatus.Deleted"/> para el catálogo de sistemas. La fila
    /// permanece en la base —las consultas sobre datos antiguos siguen necesitando saber a qué
    /// sistema pertenecía un rol, un permiso o un registro de auditoría de hace dos años—, pero
    /// desaparece de toda lectura: el filtro global de <c>SystemSuiteRecord</c> la oculta, así que un
    /// GET devuelve 404 y el listado no la trae.</para>
    ///
    /// <para>No se alcanza por <c>PUT /status</c>: solo por <c>SystemSuite.Delete</c>, que es quien
    /// exige que ninguna referencia viva quede apuntando al sistema.</para>
    /// </summary>
    public static readonly SystemStatus Deleted = new(4, nameof(Deleted));

    private SystemStatus(int id, string name) : base(id, name) { }
}
