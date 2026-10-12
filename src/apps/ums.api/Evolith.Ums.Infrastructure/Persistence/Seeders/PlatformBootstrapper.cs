using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Evolith.Ums.Application.Common.Interfaces;
using Evolith.Ums.Application.Identity.Auth;
using Evolith.Ums.Domain.Enums;
using Evolith.Ums.Domain.Identity;
using Evolith.Ums.Domain.Identity.Tenant;
using Evolith.Ums.Domain.Identity.UserAccount;
using Evolith.Ums.Domain.Kernel.ValueObjects;
using Evolith.Ums.Infrastructure.Persistence.Options;

// Mismos alias que usan los sembradores: el tipo del agregado colisiona con el de su espacio de
// nombres (`Tenant.Tenant`), y sin alias cada referencia tendría que ir cualificada entera.
using TenantAggregate = Evolith.Ums.Domain.Identity.Tenant.Tenant;
using UserAccountAggregate = Evolith.Ums.Domain.Identity.UserAccount.UserAccount;

namespace Evolith.Ums.Infrastructure.Persistence.Seeders;

/// <summary>
/// Arranque en frío de la plataforma (patrón Bootstrapping / First-Run): crea el MÍNIMO con el que
/// UMS puede empezar a operarse a sí mismo, y nada más.
///
/// <para>POR QUÉ EXISTE. Toda la configuración de UMS —suites, roles, plantillas, catálogos— nacía
/// dentro de <see cref="CoreDevDataSeeder"/>, tras la bandera <c>Persistence:SeedDevData</c>, que
/// mezcla datos de referencia con el dataset de demostración de once inquilinos. Production lleva
/// esa bandera en <c>false</c> y además hay una guarda que impide sembrar allí pase lo que pase, de
/// modo que un despliegue productivo quedaba con las tablas vacías: sin ningún usuario con el que
/// entrar y sin nada que administrar. El «Registrar empresa» del login tampoco resuelve el círculo,
/// porque la solicitud que crea necesita un aprobador que todavía no existe.</para>
///
/// <para>QUÉ CREA, Y POR QUÉ SOLO ESO. Un inquilino propietario de gestión y una cuenta activa.
/// Verificado contra <c>TenantScopePolicy.EnsureManagementOwnerScopeAsync</c>: para provisionar por
/// API basta con un inquilino en contexto marcado <c>IsManagementOwner</c> y un usuario autenticado
/// — no hace falta rol, ni perfil, ni plantilla. Y el login tolera una cuenta activa sin perfil
/// (grafo lobby, G-122). Con ese mínimo, el cargador declarativo
/// (<c>src/provisioning/cargar-en-ums.mjs</c>) puede dar de alta la suite UMS entera desde
/// <c>ums-suite.json</c>, que es donde debe vivir la configuración.</para>
///
/// <para>LA CREDENCIAL INICIAL, Y POR QUÉ NO ES UNA CONTRASEÑA. En producción la cuenta nace SIN
/// credencial y lo que se registra es un secreto de un solo uso, de vida corta, del que solo se
/// persiste el SHA-256. Esto satisface «forzar el cambio de contraseña en el primer ingreso» de la
/// forma más fuerte posible: no hay nada que cambiar, porque no hay contraseña hasta que su titular
/// elige una, y hasta entonces la cuenta no puede iniciar sesión. Como efecto lateral, evita añadir
/// un campo <c>MustChangePassword</c> al modelo — que habría costado siete capas y una migración EF
/// en cada uno de los dos proveedores, PostgreSQL y SQL Server, con ensamblados de migración
/// separados.</para>
///
/// <para>DIFERENCIA CON EL SEMBRADOR DE DESARROLLO. Este SÍ puede correr en Production —es su razón
/// de ser—, pero es explícito (apagado por omisión), idempotente, y no arranca si la plataforma ya
/// se inicializó alguna vez. No siembra datos de demostración de ningún tipo.</para>
/// </summary>
public static class PlatformBootstrapper
{
    /// <param name="esProduccion">
    /// Se recibe en vez de resolver <c>IHostEnvironment</c> aquí: Infrastructure no debe depender
    /// del hospedaje, y quien invoca ya tiene el dato. Solo gobierna el rechazo del modo
    /// <see cref="BootstrapCredentialMode.Password"/>.
    /// </param>
    public static async Task BootstrapAsync(
        IServiceProvider serviceProvider,
        bool esProduccion,
        CancellationToken cancellationToken = default)
    {
        var opciones = serviceProvider.GetService<IOptions<PersistenceOptions>>()?.Value;
        var bootstrap = opciones?.Bootstrap;

        if (bootstrap is null || !bootstrap.Enabled)
        {
            return;
        }

        using var scope = serviceProvider.GetRequiredService<IServiceScopeFactory>().CreateScope();
        var sp = scope.ServiceProvider;

        var log = sp.GetService<ILoggerFactory>()?.CreateLogger("Evolith.Ums.Bootstrap");

        var tenantRepository = sp.GetService<ITenantRepository>();
        var userAccountRepository = sp.GetService<IUserAccountRepository>();

        // Sin repositorios persistentes (modo InMemory de las pruebas) no hay nada que arrancar.
        if (tenantRepository is null || userAccountRepository is null)
        {
            return;
        }

        // Una contraseña fijada por el despliegue vive indefinidamente y sobrevive en el entorno del
        // proceso. En desarrollo eso es comodidad; en producción es una credencial por defecto, que
        // es exactamente lo que este patrón existe para no tener.
        if (esProduccion && bootstrap.CredentialMode == BootstrapCredentialMode.Password)
        {
            throw new InvalidOperationException(
                "Persistence:Bootstrap:CredentialMode=Password no está permitido en Production. "
                + "Una contraseña inyectada por el despliegue es una credencial por defecto: no caduca "
                + "y nadie obliga a cambiarla. Usa OneTimeToken.");
        }

        // ── Guarda de PRIMER ARRANQUE ────────────────────────────────────────────────────────
        // La reentrada NO se decide por el correo del administrador. Si se decidiese así, borrar o
        // renombrar esa cuenta bastaría para que el siguiente arranque crease otra con la credencial
        // del entorno —que sigue viva en el gestor de secretos—, reabriendo la puerta sin que nadie
        // lo pidiera. La pregunta correcta es si esta plataforma se inicializó alguna vez.
        if (bootstrap.RequireEmptyUserTable
            && await userAccountRepository.AnyUserAccountExistsAsync(cancellationToken))
        {
            log?.LogInformation(
                "Arranque en frío omitido: la plataforma ya tiene cuentas de usuario, así que ya fue inicializada.");
            return;
        }

        var credencial = LeerCredencialDelEntorno(bootstrap);

        var actor = ActorId.Create(CoreDevDataSeeder.SystemActorId);
        var correo = Environment.GetEnvironmentVariable(bootstrap.AdminEmailEnvVar) is { Length: > 0 } deEntorno
            ? deEntorno
            : bootstrap.AdminEmail;

        var inquilino = await AsegurarInquilinoAsync(tenantRepository, bootstrap, actor, log, cancellationToken);
        if (inquilino is null)
        {
            return;
        }

        await AsegurarConfiguracionGlobalAsync(sp, actor, log, cancellationToken);

        await AsegurarAdministradorAsync(
            sp, userAccountRepository, inquilino, bootstrap, correo, credencial, actor, log, cancellationToken);
    }

    /// <summary>
    /// Lee el secreto de arranque de la variable declarada y lo valida. Nunca sale de aquí hacia un
    /// log ni hacia una respuesta.
    /// </summary>
    private static string LeerCredencialDelEntorno(BootstrapOptions bootstrap)
    {
        var variable = bootstrap.CredentialMode == BootstrapCredentialMode.OneTimeToken
            ? bootstrap.InitialSecretEnvVar
            : bootstrap.AdminPasswordEnvVar;

        var valor = Environment.GetEnvironmentVariable(variable);

        if (string.IsNullOrWhiteSpace(valor))
        {
            throw new InvalidOperationException(
                $"Persistence:Bootstrap está habilitado pero {variable} no está definida. "
                + "La credencial de arranque se inyecta por entorno o gestor de secretos, nunca por appsettings.");
        }

        // Solo se exige longitud al token: es un secreto generado por máquina y, si es corto, deja de
        // ser inadivinable. La contraseña del modo de desarrollo pasa por el validador de la
        // aplicación cuando su titular la cambia; aquí no se le impone una regla que solo estorbaría.
        if (bootstrap.CredentialMode == BootstrapCredentialMode.OneTimeToken
            && valor.Length < bootstrap.InitialSecretMinLength)
        {
            throw new InvalidOperationException(
                $"El secreto de {variable} tiene {valor.Length} caracteres y se exigen al menos "
                + $"{bootstrap.InitialSecretMinLength}. Genera uno con al menos 256 bits de entropía.");
        }

        return valor;
    }

    /// <summary>
    /// Siembra las configuraciones GLOBALES de la plataforma.
    ///
    /// <para>POR QUÉ ES IMPRESCINDIBLE. No son adorno ni datos de ejemplo: el resolvedor de método
    /// de autenticación lee <c>AUTH_USE_EXTERNAL_IDP</c> del ámbito global y, si no está,
    /// devuelve <c>REQUIRED_PARAMETER_NOT_CONFIGURED</c>. Esa excepción cae en el <c>catch</c>
    /// general del manejador de login, que la convierte en un mensaje sin código —y de ahí a un
    /// <c>401 AUTH_000</c> genérico—. Medido el 2026-08-15: un inquilino y una cuenta perfectamente
    /// válidos no podían entrar, y el motivo solo aparecía en el registro de auditoría (G-259).</para>
    ///
    /// <para>Se reutiliza el constructor del sembrador de desarrollo en lugar de duplicar la lista:
    /// dos definiciones de la misma línea base terminarían divergiendo, que es precisamente el
    /// accidente que este trabajo vino a corregir en el catálogo de menús.</para>
    /// </summary>
    private static async Task AsegurarConfiguracionGlobalAsync(
        IServiceProvider sp,
        ActorId actor,
        ILogger? log,
        CancellationToken cancellationToken)
    {
        var repositorio = sp.GetService<Evolith.Ums.Domain.Configuration.IAppConfigurationRepository>();
        if (repositorio is null)
        {
            return;
        }

        var globales = ConfigurationDevDataSeeder.BuildSeedAppConfigurations(actor);
        await ConfigurationDevDataSeeder.SeedAppConfigurationsAsync(repositorio, globales, cancellationToken);

        log?.LogInformation("Arranque en frío: {Cantidad} configuraciones globales aseguradas.", globales.Count);
    }

    /// <summary>
    /// Crea el inquilino propietario de gestión si no existe. Idempotente por código.
    /// </summary>
    private static async Task<TenantAggregate?> AsegurarInquilinoAsync(
        ITenantRepository tenantRepository,
        BootstrapOptions bootstrap,
        ActorId actor,
        ILogger? log,
        CancellationToken cancellationToken)
    {
        var existente = await tenantRepository.GetByCodeAsync(bootstrap.TenantCode, cancellationToken);
        if (existente is not null)
        {
            log?.LogInformation(
                "Arranque en frío: el inquilino {Codigo} ya existe; no se recrea.", bootstrap.TenantCode);
            return existente;
        }

        var resultado = TenantAggregate.Create(
            Code.Create(bootstrap.TenantCode),
            Name.Create(bootstrap.TenantName),
            OrganizationType.INTERNAL,
            actor,
            IdpStrategy.InternalBcrypt,
            CompanyReference.Create(bootstrap.CompanyReference),
            parentTenantId: null,
            tenantId: null,
            isManagementOwner: true);

        if (resultado.IsFailure)
        {
            throw new InvalidOperationException(
                $"Arranque en frío: no se pudo crear el inquilino: {resultado.Error}");
        }

        await tenantRepository.AddAsync(resultado.Value, cancellationToken);
        await tenantRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        log?.LogInformation(
            "Arranque en frío: inquilino {Codigo} creado como propietario de gestión.", bootstrap.TenantCode);

        return resultado.Value;
    }

    /// <summary>
    /// Crea la cuenta administradora y le da su credencial inicial según el modo configurado.
    ///
    /// <para>Se activa explícitamente porque una cuenta nace <c>Pending</c> y una cuenta pendiente
    /// no puede iniciar sesión: el login responde <c>AUTH_005</c>. Dejarla sin activar produciría
    /// el peor desenlace — un arranque que informa de éxito y una cuenta con la que nadie entra.</para>
    ///
    /// <para>La idempotencia se decide por «¿tiene ya credencial activa?», no por «¿existe?». Una
    /// cuenta existente SIN credencial es un arranque a medio terminar —el token venció antes de
    /// canjearse— y debe poder reintentarse; una cuenta CON credencial ya la controla alguien, y
    /// volver a emitir un secreto sería regalar una vía de entrada.</para>
    /// </summary>
    private static async Task AsegurarAdministradorAsync(
        IServiceProvider sp,
        IUserAccountRepository userAccountRepository,
        TenantAggregate inquilino,
        BootstrapOptions bootstrap,
        string correo,
        string credencial,
        ActorId actor,
        ILogger? log,
        CancellationToken cancellationToken)
    {
        var cuenta = await userAccountRepository.GetByEmailAsync(Email.Create(correo), cancellationToken);

        if (cuenta is not null && cuenta.ActivePasswordHash is not null)
        {
            log?.LogInformation(
                "Arranque en frío: la cuenta {Correo} ya tiene credencial activa; no se toca.", correo);
            return;
        }

        if (cuenta is null)
        {
            var resultado = UserAccountAggregate.Create(
                TenantId.Load(inquilino.Props.Id.GetValue()),
                Email.Create(correo),
                UserCategory.Internal,
                null,
                null,
                actor);

            if (resultado.IsFailure)
            {
                throw new InvalidOperationException(
                    $"Arranque en frío: no se pudo crear la cuenta: {resultado.Error}");
            }

            cuenta = resultado.Value;
            cuenta.Activate(actor);

            await userAccountRepository.AddAsync(cuenta, cancellationToken);
            await userAccountRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

            log?.LogInformation(
                "Arranque en frío: cuenta {Correo} creada y activada en {Inquilino}.", correo, bootstrap.TenantCode);
        }

        if (bootstrap.CredentialMode == BootstrapCredentialMode.Password)
        {
            await FijarContrasenaAsync(sp, userAccountRepository, cuenta, credencial, actor, log, cancellationToken);
            return;
        }

        await RegistrarSecretoDeUnSoloUsoAsync(sp, inquilino, cuenta, bootstrap, credencial, log, cancellationToken);
    }

    /// <summary>
    /// Modo de desarrollo y UAT: la cuenta nace con contraseña y se puede entrar de inmediato.
    /// </summary>
    private static async Task FijarContrasenaAsync(
        IServiceProvider sp,
        IUserAccountRepository userAccountRepository,
        UserAccountAggregate cuenta,
        string clave,
        ActorId actor,
        ILogger? log,
        CancellationToken cancellationToken)
    {
        var passwordHasher = sp.GetService<IPasswordHashingService>()
            ?? throw new InvalidOperationException(
                "Arranque en frío en modo Password sin IPasswordHashingService registrado.");

        var alta = cuenta.AddPassword(PasswordHash.Create(passwordHasher.Hash(clave)), actor);
        if (alta.IsFailure)
        {
            throw new InvalidOperationException(
                $"Arranque en frío: no se pudo fijar la contraseña: {alta.Error}");
        }

        await userAccountRepository.UpdateAsync(cuenta, cancellationToken);
        await userAccountRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        log?.LogWarning(
            "Arranque en frío en modo Password: la cuenta tiene una contraseña fijada por el despliegue, "
            + "que no caduca y que nadie obliga a cambiar. Aceptable en desarrollo y UAT; nunca en producción.");
    }

    /// <summary>
    /// Modo de producción: la cuenta queda SIN contraseña y se registra el secreto de un solo uso
    /// que el operador ya tiene. Se persiste únicamente su SHA-256.
    ///
    /// <para>El secreto no se genera aquí a propósito. Si UMS lo generase habría que entregárselo a
    /// alguien, y todos los canales disponibles en el arranque lo filtran: no hay respuesta HTTP, el
    /// log es el sitio equivocado para un secreto, y el adaptador de notificación registrado vuelca
    /// asunto y cuerpo al log (G-197). Generándolo el operador, el secreto ya está donde debe estar
    /// —su gestor de secretos— y UMS nunca necesita comunicarlo.</para>
    /// </summary>
    private static async Task RegistrarSecretoDeUnSoloUsoAsync(
        IServiceProvider sp,
        TenantAggregate inquilino,
        UserAccountAggregate cuenta,
        BootstrapOptions bootstrap,
        string secreto,
        ILogger? log,
        CancellationToken cancellationToken)
    {
        var almacen = sp.GetService<IPasswordResetTokenStore>()
            ?? throw new InvalidOperationException(
                "Arranque en frío en modo OneTimeToken sin IPasswordResetTokenStore registrado. "
                + "Sin él la cuenta quedaría sin credencial y sin forma de obtenerla.");

        var emitido = DateTime.UtcNow;

        await almacen.IssueAsync(
            inquilino.Props.Id.GetValue(),
            cuenta.Props.Id.GetValue(),
            PasswordResetToken.Hash(secreto),
            emitido,
            emitido.AddMinutes(bootstrap.InitialSecretLifetimeMinutes),
            cancellationToken);

        // Se registra QUE se emitió y cuándo vence. Nunca el secreto ni su hash: un log es un
        // artefacto que se copia, se envía y se indexa.
        log?.LogInformation(
            "Arranque en frío: secreto de un solo uso registrado para {Correo}; vence en {Minutos} minutos. "
            + "La cuenta NO tiene contraseña: cánjealo en /reset-password para fijarla.",
            cuenta.Email.GetValue(),
            bootstrap.InitialSecretLifetimeMinutes);
    }
}
