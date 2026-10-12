namespace Evolith.Ums.Infrastructure.Persistence.Seeders;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Evolith.Ums.Infrastructure.Persistence.Options;
using Evolith.Ums.Domain.Identity;

public static class CoreDevDataSeeder
{
    public const string SystemActorId = "00000000-0000-0000-0000-000000000001";

    // ── Internal Admin Tenant (global administration) ─────────────────────────
    public const string InternalAdminTenantId = "11111111-1111-1111-1111-111111111111";
    public const string InternalAdminTenantCode = "INTERNAL_ADMIN";
    public const string InternalAdminTenantName = "Internal Admin Tenant";

    // ── SuperAdmin User (global admin) ─────────────────────────────────────────
    public const string SuperAdminUserId = "22222222-2222-2222-2222-222222222222";
    public const string SuperAdminUsername = "admin";
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Security", "S2068:Hard-coded credentials are security-sensitive",
        Justification = "Contraseña de datos SEMILLA de desarrollo, nunca de producción: solo se aplica bajo " +
                        "SeedDevData && !IsProduction. No es un secreto real (RB-06, stage UAT determinista).")]
    public const string SuperAdminPassword = "root"; // Default password for INTERNAL admin (change in production)
    public const string InternalAdminPendingUserId = "11111103-1111-1111-1111-111111111111";

    // ── GlobalAdmin Role & Profile ─────────────────────────────────────────────
    public const string GlobalAdminRoleId = "33333333-3333-3333-3333-333333333333";
    public const string GlobalAdminProfileId = "44444444-4444-4444-4444-444444444444";
    public const string GlobalAdminTemplateId = "55555555-5555-5555-5555-555555555555";

    // ── Ransa Tenant (existing commercial tenant) ─────────────────────────────
    public const string RansaTenantId = "3fa85f64-5717-4562-b3fc-2c963f66afa6";
    // Derived from RansaTenantId bytes: bytes[0] replaced by index (little-endian Data1)
    public const string RansaAdminUserId = "3fa85f01-5717-4562-b3fc-2c963f66afa6";   // gerente.operaciones — DeriveGuid(1)
    public const string RansaAnalystUserId = "3fa85f02-5717-4562-b3fc-2c963f66afa6"; // analista.inventario — DeriveGuid(2)

    // Fixed placeholder GUIDs for cross-context dev references
    public const string DemoAdminRoleId = "aaaa0001-0000-0000-0000-000000000001";
    public const string DemoOperatorRoleId = "aaaa0002-0000-0000-0000-000000000001";
    public const string DemoAdminProfileId = "bbbb0001-0000-0000-0000-000000000001";
    public const string RansaEntraIdProviderId = "cccc0001-0000-0000-0000-000000000001";
    public const string DemoSystemSuiteId = "dddd0001-0000-0000-0000-000000000001";
    public const string InternalAdminInboxWorkflowId = "88888888-3333-3333-3333-333333333333";

    // ── EVOLITH operator (SUPPLIER) — FS-25 seed dataset ───────────────────────
    // Existing stable tenant GUID; reconciled by upsert (RUC + branches), never recreated.
    public const string EvolithTenantId = "5f4e3d2c-1b0a-9f8e-7d6c-543210987654";
    public const string EvolithRuc = "RUC-20100412447";
    // Uniform dev password (min. 12 chars) for EVOLITH internal and client users (FS-25 §4.4).
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Security", "S2068:Hard-coded credentials are security-sensitive",
        Justification = "Contraseña de datos SEMILLA de desarrollo (FS-25 §4.4), nunca de producción: solo bajo " +
                        "SeedDevData && !IsProduction. No es un secreto real (RB-06, stage UAT determinista).")]
    public const string EvolithDevPassword = "Evolith.Dev.2026";

    // EVOLITH client tenants (importer/exporter) added by FS-25 §4.1.
    public const string ComexAndinaTenantId = "c0e1a000-1111-4c0e-a000-000000000001"; // COMEX_ANDINA (impo Lima)
    public const string AgronorteTenantId = "a9701e00-2222-4a97-b000-000000000002";   // AGRONORTE (expo Paita)
    public const string FrupiuraTenantId = "f3401a00-3333-4f34-c000-000000000003";    // FRUPIURA (expo Paita, opcional)

    // "Cliente de mi cliente": a CLIENT tenant that hangs off COMEX_ANDINA (ParentTenantId),
    // to exercise the tenant hierarchy. Gets the same scoped treatment as the external clients
    // (PORTAL_CLIENTE-only + CLIENTE_EXTERNO). GUID is deterministic and distinct from its parent.
    public const string ImpoAndinaSubTenantId = "c0e1b000-1111-4c0e-b000-000000000011"; // IMPO_ANDINA_SUB (hijo de COMEX_ANDINA)
    public const string ImpoAndinaSubTenantCode = "IMPO_ANDINA_SUB";
    public const string ImpoAndinaSubTenantName = "Importadora Sub-Cliente de Comex Andina S.A.C.";
    public const string ImpoAndinaSubBranchCode = "IASUB_LIMA";
    public const string ImpoAndinaSubBranchName = "Almacén Lima Sub-Cliente";
    public const string ImpoAndinaSubUserEmail = "usuario@impo-subcliente.com.pe";

    // EVOLITH internal user deterministic GUIDs are derived from the EVOLITH tenant GUID
    // by replacing byte[0] with the index below (same little-endian scheme used by the
    // Identity and Authorization seeders). Profiles link to users through these indices.
    //   1 admin.callao · 2 agente.aduanas.callao · 3 despachador.callao · 4 jefe.almacen.callao
    //   5 coordinador.transporte.callao · 6 ejecutivo.cuenta.callao · 7 analista.doc.callao
    //   8 auditor.callao · 9 jefe.almacen.paita · 10 operario.almacen.paita
    //   11 agente.aduanas.paita · 12 ejecutivo.cuenta.paita · 20 admin (root EVOLITH, sin sucursal)
    public const byte EvolithJefeAlmacenPaitaUserIndex = 9; // FS-25 criterio 7

    // EVOLITH ROOT ADMIN: transversal operator admin (no branch), distinct from the per-branch
    // admin (index 1) and from the platform super-admin (admin@ums.local). Same derived-guid scheme.
    public const byte EvolithRootAdminUserIndex = 20;
    public const string EvolithRootAdminEmail = "admin@evolith.example";

    // ── Credenciales del super usuario de DESARROLLO y UAT ────────────────────
    //
    // Las constantes de arriba son el valor POR OMISIÓN, no el valor final. Un entorno puede
    // sustituirlas por variable sin recompilar, que es lo que permite montar un UAT compartido sin
    // que su contraseña sea la que está escrita —y publicada— en el repositorio.
    //
    // Estos valores solo se aplican bajo `SeedDevData`, que Production rechaza de plano. Para
    // inicializar un despliegue productivo está `Persistence:Bootstrap`, que no admite contraseñas
    // fijadas por el despliegue.
    //
    // Se resuelven en UNA sola pareja de propiedades, y no en cada llamador, porque las pruebas de
    // integración se autentican con estos mismos valores: si el sembrador leyera el entorno y las
    // pruebas la constante, bastaría definir la variable para que la suite entera dejara de entrar.

    /// <summary>Variable de entorno que sustituye el correo del administrador sembrado.</summary>
    public const string EvolithRootAdminEmailEnvVar = "UMS_DEV_ADMIN_EMAIL";

    /// <summary>Variable de entorno que sustituye la contraseña de los usuarios sembrados.</summary>
    public const string EvolithDevPasswordEnvVar = "UMS_DEV_ADMIN_PASSWORD";

    /// <summary>Correo efectivo del administrador sembrado: entorno si está definido, constante si no.</summary>
    public static string EffectiveEvolithRootAdminEmail =>
        LeerDelEntorno(EvolithRootAdminEmailEnvVar) ?? EvolithRootAdminEmail;

    /// <summary>Contraseña efectiva de los usuarios sembrados: entorno si está definida, constante si no.</summary>
    public static string EffectiveEvolithDevPassword =>
        LeerDelEntorno(EvolithDevPasswordEnvVar) ?? EvolithDevPassword;

    private static string? LeerDelEntorno(string variable) =>
        Environment.GetEnvironmentVariable(variable) is { Length: > 0 } valor && !string.IsNullOrWhiteSpace(valor)
            ? valor
            : null;

    // Tenant ancla usado como marca de "ya sembrado" por la guarda de idempotencia.
    public const string SeedAnchorTenantCode = "RANSA_PERU";

    public static async Task SeedAllAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        // G-127: guarda de idempotencia a nivel de orquestador. Si el dataset ya está sembrado
        // (tenant ancla RANSA_PERU presente), se omite TODA la siembra → los reinicios de pod no
        // re-ejecutan los 7 seeders ni ensucian los logs con conflictos de PK esperados. Los
        // seeders siguen siendo idempotentes por sí mismos (GUID fijos + reconciliación por clave
        // natural), así que un `reset` —que dropea el esquema— siempre re-siembra limpio. Solo
        // aplica al backend persistente (PostgreSQL); en modo in-memory ITenantRepository no está
        // registrado y siempre se siembra desde cero. Recuperación de una siembra inicial parcial
        // (un seeder falló en el primer arranque): ejecutar `reset` (ver runbook UAT).
        // `Persistence:ForceReseed` salta la guarda. Sin esa escotilla, el conjunto sembrado queda
        // congelado en lo que hubiera el primer día: añadir después un sistema al catálogo no lo
        // hace llegar a ninguna base existente, y la única salida era un `reset` que dropea el
        // esquema —y con él lo que NO siembra el código, como el Tablero SDLC, que se carga por
        // API—. Los sembradores reconcilian por clave natural, así que volver a pasarlos añade lo
        // que falta sin duplicar lo que ya está.
        var forzar = serviceProvider.GetService<IOptions<PersistenceOptions>>()?.Value.ForceReseed ?? false;

        if (!forzar && await IsAlreadySeededAsync(serviceProvider, cancellationToken))
        {
            Console.WriteLine(
                $"Seed skipped: anchor tenant '{SeedAnchorTenantCode}' already present (idempotent restart).");
            return;
        }

        if (forzar)
        {
            Console.WriteLine(
                "Seed forced: Persistence:ForceReseed=true — se ejecutan los sembradores aunque el "
                + "dataset ya exista. Añaden lo que falta por clave natural; apágalo tras comprobar.");
        }

        await RunSeederAsync(serviceProvider, "Identity", IdentityDevDataSeeder.SeedAsync, cancellationToken);
        await RunSeederAsync(serviceProvider, "Authorization", AuthorizationDevDataSeeder.SeedAsync, cancellationToken);
        await RunSeederAsync(serviceProvider, "Configuration", ConfigurationDevDataSeeder.SeedAsync, cancellationToken);
        await RunSeederAsync(serviceProvider, "Parameter catalog", ParameterCatalogSeeder.SeedAsync, cancellationToken);
        await RunSeederAsync(serviceProvider, "Approvals", ApprovalsDevDataSeeder.SeedAsync, cancellationToken);
        await RunSeederAsync(serviceProvider, "IGA", IgaDevDataSeeder.SeedAsync, cancellationToken);
        await RunSeederAsync(serviceProvider, "Audit", AuditDevDataSeeder.SeedAsync, cancellationToken);
    }

    private static async Task<bool> IsAlreadySeededAsync(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        using var scope = scopeFactory.CreateScope();

        // En modo in-memory el repositorio persistente no está registrado → nunca se omite.
        var tenantRepository = scope.ServiceProvider.GetService<ITenantRepository>();
        if (tenantRepository is null)
        {
            return false;
        }

        try
        {
            var anchor = await tenantRepository.GetByCodeAsync(SeedAnchorTenantCode, cancellationToken);
            return anchor is not null;
        }
        catch
        {
            // Ante cualquier fallo del chequeo (p. ej. esquema aún migrándose) se cae a sembrar:
            // los seeders son idempotentes, de modo que sembrar de más es seguro y no duplica.
            return false;
        }
    }

    private static async Task RunSeederAsync(
        IServiceProvider serviceProvider,
        string seederName,
        Func<IServiceProvider, CancellationToken, Task> seedAsync,
        CancellationToken cancellationToken)
    {
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        using var scope = scopeFactory.CreateScope();

        try
        {
            await seedAsync(scope.ServiceProvider, cancellationToken);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{seederName} seeder failed: {ex.Message}");
        }
    }
}
