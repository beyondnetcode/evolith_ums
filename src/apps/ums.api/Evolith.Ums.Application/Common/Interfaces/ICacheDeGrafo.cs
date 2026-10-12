using Evolith.Ums.Domain.Authorization.Graph;

namespace Evolith.Ums.Application.Common.Interfaces;

/// <summary>
/// Retención del grafo de autorización de una sesión, para no reconstruirlo en cada petición.
///
/// POR QUÉ EXISTE. Hasta ahora UMS construía el grafo en el login, lo serializaba en la respuesta
/// y lo OLVIDABA: lo único que sobrevivía a la petición eran los claims `scope` del JWT. Eso basta
/// para la autorización de navegación —que es como están etiquetados los handlers— pero deja fuera
/// `domainPermissions`, `featureFlags` y `settings`, que no viajan en el token y por tanto no se
/// pueden evaluar del lado del servidor.
///
/// Y hay un límite duro que lo empuja: el token es hoy el único portador, y una cookie se descarta
/// EN SILENCIO al pasar de 4.096 caracteres. Crecer por la vía del token no es una opción.
///
/// DÓNDE VIVE LO RETENIDO. Esta interfaz no lo decide, y es deliberado. La implementación se apoya
/// en <c>IDistributedCache</c>, que en UMS ya resuelve SIEMPRE: a memoria del proceso por omisión
/// —el mínimo por defecto— y a Redis en cuanto <c>Redis:Connection</c> o <c>REDIS_CONNECTION</c>
/// están definidas (mismo interruptor que gobierna la revocación de tokens y la caché de
/// configuración desde G-169). Un segundo interruptor daría dos verdades sobre dónde vive el estado.
/// </summary>
public interface ICacheDeGrafo
{
    /// <summary>
    /// Devuelve el grafo retenido —en la MISMA forma que UMS emite por cable— o <c>null</c> si no
    /// hay, caducó, o la versión del perfil avanzó.
    ///
    /// Se devuelve el JSON del payload y no el <see cref="AuthorizationGraph"/> del dominio por una
    /// razón medida: ese record se construye con la fábrica <c>Build</c> y propiedades <c>init</c>,
    /// sin constructor sin parámetros ni <c>[JsonConstructor]</c>, así que <c>System.Text.Json</c>
    /// NO SABE deserializarlo. Añadírselo doblaría el dominio para complacer a la infraestructura y
    /// abriría una vía para construir un grafo saltándose las invariantes que <c>Build</c> asegura.
    /// </summary>
    Task<string?> ObtenerAsync(string claveDeGrafo, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retiene el grafo, serializándolo con el mismo mapeador que lo emite por cable
    /// (<c>AuthGraphPayload</c>, único mapeador del grafo por D-025).
    /// </summary>
    Task GuardarAsync(string claveDeGrafo, AuthorizationGraph grafo, CancellationToken cancellationToken = default);

    /// <summary>
    /// Construye la clave de un grafo. Depende de lo que hace al grafo distinto —el perfil, el
    /// sistema pedido y la proyección— y de las TRES versiones que pueden dejarlo obsoleto: la
    /// global, la del inquilino y la del perfil.
    ///
    /// <para><paramref name="tenantId"/> puede ser <c>null</c>: es el caso del administrador
    /// interno que opera entre inquilinos. Ahí solo cuentan la versión global y la del perfil.</para>
    /// </summary>
    Task<string> ClaveAsync(
        Guid? tenantId, Guid profileId, string? systemCode, string? proyeccion, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalida TODO lo retenido de un perfil avanzando su versión.
    ///
    /// SE HACE ASÍ Y NO BORRANDO PORQUE <c>IDistributedCache</c> NO SABE BORRAR POR PREFIJO: no
    /// enumera claves, así que no hay forma de encontrar las variantes de un mismo perfil —una por
    /// sistema y proyección pedidos—. Avanzar un contador las deja a todas inalcanzables de golpe,
    /// y las huérfanas caducan solas.
    ///
    /// Es la mitad que convierte una caché en un agujero si falta: sin invalidar al asignar una
    /// plantilla, desactivar un perfil o cambiar un rol, un permiso REVOCADO seguiría concediendo
    /// hasta que el grafo caducase por su cuenta.
    /// </summary>
    Task InvalidarPerfilAsync(Guid profileId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalida todo lo retenido de un INQUILINO.
    ///
    /// <para>POR QUÉ ESTE NIVEL EXISTE. Casi ningún cambio es de un solo perfil: tocar un rol, una
    /// plantilla o una suite altera el grafo de todos los perfiles que cuelgan de ellos. Cubrirlo
    /// por perfil exigiría enumerar los afectados en cada uno de los 43 manejadores que pueden
    /// cambiar un grafo, y olvidarse de uno significa servir un permiso ya retirado —el fallo
    /// abierto—. Un contador por inquilino se avanza en UN sitio y no se puede olvidar.</para>
    ///
    /// <para>ES SEGURO ACOTARLO AL INQUILINO porque todo el modelo de autorización lo lleva:
    /// <c>SystemSuite</c>, <c>Role</c>, <c>PermissionTemplate</c> y <c>Profile</c> tienen
    /// <c>TenantId</c>, y <c>Profile</c> RECHAZA una plantilla de otro inquilino. Un cambio en uno
    /// no puede alterar el grafo de otro.</para>
    /// </summary>
    Task InvalidarInquilinoAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalida todo lo retenido, de todos los inquilinos.
    ///
    /// <para>HACE FALTA porque no todo lo que alimenta al grafo está acotado a un inquilino:
    /// <c>FeatureFlagProps.TenantId</c> y <c>AppConfiguration.TenantId</c> son ANULABLES, así que
    /// una bandera o un parámetro sin inquilino cambian el grafo de todos. Con solo el contador por
    /// inquilino, ese cambio no invalidaría nada.</para>
    ///
    /// <para>También es el destino del administrador interno operando entre inquilinos, donde no
    /// hay uno al que acotar: se invalida de más, que es el lado seguro del error.</para>
    /// </summary>
    Task InvalidarGlobalAsync(CancellationToken cancellationToken = default);
}
