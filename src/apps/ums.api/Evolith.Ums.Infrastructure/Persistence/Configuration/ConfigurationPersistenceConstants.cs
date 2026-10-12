namespace Evolith.Ums.Infrastructure.Persistence.Configuration;

internal static class ConfigurationPersistenceConstants
{
    public const string Schema = "ums_configuration";

    /// <summary>
    /// Predicado SQL de «fila viva» para los índices únicos PARCIALES de este esquema.
    ///
    /// ADR-0164 §2.3 dejó los índices únicos SIN filtrar por borrado, de modo que la clave natural
    /// quedaba ocupada para siempre. El propietario del producto acotó esa regla el 2026-08-04: vale
    /// para lo que identifica algo del mundo real —una sucursal, un sistema— pero NO para una RANURA
    /// de configuración. El código de una configuración sale de un catálogo cerrado
    /// (`MFA_REQUIRED_FOR_ADMIN` es *el* nombre del parámetro, no uno que se elija), así que dejar la
    /// ranura ocupada equivale a impedir para siempre volver a configurar ese parámetro.
    ///
    /// El filtro se escribe en SQL y no como expresión C# porque un índice parcial vive en la base;
    /// el literal 4 es <see cref="ConfigStatus.Deleted"/>. Se interpola desde el enum, no se teclea:
    /// si ese id cambiara, la foto del modelo dejaría de cuadrar y la migración de verificación
    /// saldría NO vacía, que es exactamente la señal que se quiere.
    /// </summary>
    public static readonly string LiveRowIndexFilter =
        $"\"StatusId\" != {ConfigStatus.Deleted.Id}";
}
