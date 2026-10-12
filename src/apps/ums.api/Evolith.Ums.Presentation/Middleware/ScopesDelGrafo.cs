namespace Evolith.Ums.Presentation.Middleware;

using System.Security.Claims;
using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Domain.Authorization.Graph;
using Evolith.Ums.Domain.Identity;

/// <summary>
/// Anade a una identidad de andamiaje los `scope` que el CATALOGO concede a ese usuario (G-280).
///
/// <para>Vive aparte del middleware que la estrenó porque la necesitan DOS suplantadores: el de
/// desarrollo, que resuelve la identidad por cabeceras, y el anfitrion de las pruebas de
/// integracion, que la resuelve por su propio esquema de autenticacion. Con una copia en cada
/// sitio, el dia que una cambie la otra mide otra cosa —y medir otra cosa sin notarlo es
/// exactamente lo que hacia falso el carril de integracion.</para>
///
/// <para>Se ataja la AUTENTICACION —quien eres— y no la AUTORIZACION —que puedes—, que es la
/// distincion que el andamiaje borraba: antes toda peticion suplantada dependia del atajo de Admin
/// Root para atravesar los manejadores guardados.</para>
/// </summary>
public static class ScopesDelGrafo
{
    /// <summary>Inquilino propietario de gestion (EVOLITH): donde viven los administradores internos.</summary>
    public const string InquilinoPropietarioDeGestion = "5f4e3d2c-1b0a-9f8e-7d6c-543210987654";

    /// <summary>
    /// La suite cuyo grafo se pide por omision: la del propio UMS.
    ///
    /// <para>Un usuario puede tener VARIOS perfiles —el administrador sembrado tiene el de la suite
    /// logistica y el de gobierno de UMS—, y sin pedir sistema el constructor devuelve el corriente:
    /// medido, el logistico, que no concede `manage_tenants` ni `manage_users`. Como estas
    /// peticiones van contra la API de administracion de UMS, el grafo que corresponde es el de UMS.
    /// Si el usuario no tiene perfil ahi, se reintenta sin pedir sistema.</para>
    /// </summary>
    public const string SuiteDeGobierno = "UMS";

    /// <summary>
    /// Resuelve el grafo del usuario suplantado y anade un claim `scope` por cada permiso.
    ///
    /// <para>Silencioso ante el fallo, y a proposito: si el usuario no existe, no tiene perfil o el
    /// grafo no se puede construir, la peticion sigue con las claims que ya traia. Este camino es
    /// andamiaje; romperlo por no poder resolver un grafo dejaria el entorno inservible para
    /// diagnosticar justo lo que se esta diagnosticando.</para>
    /// </summary>
    public static async Task AnadirAsync(
        List<Claim> claims,
        string? userId,
        string? tenantId,
        bool esAdminInterno,
        ITenantContext tenantContext,
        IUserAccountRepository userAccounts,
        IAuthorizationGraphBuilder graphBuilder,
        CancellationToken cancellationToken,
        string? sistema = SuiteDeGobierno)
    {
        if (!Guid.TryParse(userId, out var idUsuario) || !Guid.TryParse(tenantId, out var idInquilino))
        {
            Console.WriteLine($"[DevAuth] identidad no parseable: usuario '{userId}' inquilino '{tenantId}'");
            return;
        }

        try
        {
            var usuario = await userAccounts.GetByIdAsync(idUsuario, cancellationToken);

            // El repositorio filtra por el inquilino EN CONTEXTO, que aqui es el declarado por quien
            // suplanta. Cuando un administrador interno actua sobre otro inquilino, su propia cuenta
            // vive en el de origen y la consulta vuelve vacia: hay que preguntar en su casa.
            if (usuario is null && esAdminInterno
                && Guid.TryParse(InquilinoPropietarioDeGestion, out var idPropietario)
                && idPropietario != idInquilino)
            {
                // `SetOrganizationId`, no `Initialize`: el contexto ya esta inicializado y volver a
                // inicializarlo reescribe el inquilino de ORIGEN, que es lo que distingue a un
                // administrador actuando fuera de su casa de un usuario de la casa ajena.
                tenantContext.SetOrganizationId(idPropietario);
                usuario = await userAccounts.GetByIdAsync(idUsuario, cancellationToken);
                // El contexto NO se restaura aqui: el grafo se construye mas abajo y sus consultas
                // —perfil, plantillas, permisos— tambien filtran por inquilino. Se restaura al final.
            }

            if (usuario is null)
            {
                Console.WriteLine($"[DevAuth] sin cuenta {idUsuario} en {idInquilino}: la identidad llega SIN scopes");
                return;
            }

            // ADR-UMS-105 D5: el grafo se construye en el inquilino del USUARIO, no en el declarado.
            // Un administrador interno que actua sobre otro inquilino sigue teniendo los permisos de
            // su casa: no tiene perfil en el destino y no debe tenerlo.
            var idPropio = usuario.Props.TenantId.GetValue();
            var idParaElGrafo = idPropio == Guid.Empty ? idInquilino : idPropio;

            // El contexto tiene que ESTAR en el inquilino del usuario, no solo pasarse como
            // parametro: los repositorios que el constructor consulta —rol, suite, plantillas—
            // filtran por el inquilino EN CONTEXTO, no por el argumento. Mientras el usuario no se
            // encontraba, el reintento de arriba ya lo dejaba ahi; cuando SI se encuentra a la
            // primera —porque el repositorio de cuentas no filtra igual que el de roles— el contexto
            // se quedaba en el inquilino de la cabecera y el rol del perfil no resolvia: «Role not
            // found for profile», y con el 36 pruebas muertas en un 403 que no era de permisos
            // ([[G-286]]). Es la misma regla de ADR-UMS-105 D5, aplicada donde de verdad manda.
            // Se pregunta al CONTEXTO si el sujeto es administrador interno, no a la cabecera: si el
            // contexto se inicializo antes con otro criterio, reposicionarlo lanza «Regular users
            // cannot access other tenants» y el andamiaje tumbaria la resolucion de un usuario
            // corriente que solo estaba mirando su propia casa.
            if (tenantContext.IsInternalAdmin && idParaElGrafo != tenantContext.OrganizationId)
            {
                tenantContext.SetOrganizationId(idParaElGrafo);
            }

            var grafo = await graphBuilder.BuildAsync(
                usuario, idParaElGrafo, Evolith.Ums.Domain.Identity.Auth.AuthMethod.Local(), sistema, cancellationToken);

            // Sin perfil en la suite pedida se cae al perfil corriente: es lo que tienen los
            // usuarios de inquilinos cliente, que no gobiernan UMS y no deben hacerlo.
            //
            // Y el reintento mira TAMBIEN el grafo vacio, no solo el fallo: pedir una suite donde el
            // usuario no tiene perfil no devuelve error, devuelve un grafo SIN alcances. Medido: los
            // sujetos de otro inquilino, que sostienen su permiso en un catalogo propio, pasaron a
            // llegar sin un solo scope y el aislamiento dejo de poder demostrarse.
            if ((grafo.IsFailure || grafo.Value.Scopes.Count == 0) && sistema is not null)
            {
                grafo = await graphBuilder.BuildAsync(
                    usuario, idParaElGrafo, Evolith.Ums.Domain.Identity.Auth.AuthMethod.Local(), null, cancellationToken);
            }

            if (grafo.IsFailure)
            {
                // El tercer silencio de esta ruta, y el ultimo: cuenta encontrada, grafo imposible.
                // Sin esta linea se ve igual que «no tiene permisos» y manda a buscar donde no es.
                Console.WriteLine($"[DevAuth] grafo irresoluble para {idUsuario} en {idParaElGrafo}: {grafo.Error}");
                return;
            }

            foreach (var scope in grafo.Value.Scopes) claims.Add(new Claim("scope", scope));

            // Se registra cuantos scopes se resolvieron y para quien. Sin esta linea, una identidad
            // suplantada sin permisos y una identidad que no existe se ven IGUAL desde fuera —las
            // dos acaban en 403— y el diagnostico se vuelve adivinanza (G-280).
            Console.WriteLine(
                $"[DevAuth] {grafo.Value.Scopes.Count} scopes para {idUsuario} en {idParaElGrafo}"
                + $" (sistema {sistema ?? "corriente"})");
        }
        catch (Exception ex)
        {
            // Ver el resumen: el andamiaje no puede tumbar la peticion. Pero SILENCIOSO no: un fallo
            // aqui y una cuenta sin permisos acaban igual —403— y sin esta linea no hay forma de
            // distinguirlos. Es la misma clase de silencio que hizo cara la migracion (G-280).
            Console.WriteLine($"[DevAuth] fallo resolviendo el grafo de {idUsuario} en {idInquilino}: {ex.Message}");
        }
        finally
        {
            // El contexto vuelve SIEMPRE al inquilino sobre el que se actua: es el que gobierna los
            // filtros de la peticion de verdad, y dejarlo en el de origen abriria lectura cruzada.
            try { tenantContext.SetOrganizationId(idInquilino); } catch { /* contexto ya coherente */ }
        }
    }
}
