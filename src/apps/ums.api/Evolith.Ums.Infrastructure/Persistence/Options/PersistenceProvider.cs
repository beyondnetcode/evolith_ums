namespace Evolith.Ums.Infrastructure.Persistence.Options;

public enum PersistenceProvider
{
    InMemory = 0,

    /// <summary>
    /// SQL Server 2019+. Reintroducido para el stage UAT, que corre sobre una instancia
    /// existente en el servidor IIS. El valor 1 es el que ocupaba antes de D-008, de modo
    /// que cualquier configuración numérica antigua sigue resolviendo al mismo proveedor.
    /// </summary>
    SqlServer = 1,

    PostgreSql = 3,
}
