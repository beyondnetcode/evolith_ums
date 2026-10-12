#pragma warning disable S1144
namespace Evolith.Ums.Infrastructure.Persistence.Seeders;

using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Evolith.Ums.Domain.Authorization;
using Evolith.Ums.Domain.Authorization.Profile;
using Evolith.Ums.Domain.Authorization.Role;
using Evolith.Ums.Domain.Authorization.SystemSuite;
using Evolith.Ums.Domain.Authorization.SystemSuite.DomainResource;
using Evolith.Ums.Domain.Authorization.SystemSuite.MenuNode;
using Evolith.Ums.Domain.Authorization.Template;
using Evolith.Ums.Domain.Kernel.ValueObjects;
using Evolith.Ums.Infrastructure.Persistence.Authorization.Entities;
using Evolith.Ums.Infrastructure.Persistence.Reflection;
using ProfileAggregate = Evolith.Ums.Domain.Authorization.Profile.Profile;
using SystemSuiteAggregate = Evolith.Ums.Domain.Authorization.SystemSuite.SystemSuite;
using PermissionTemplateAggregate = Evolith.Ums.Domain.Authorization.Template.PermissionTemplate;
using RoleAggregate = Evolith.Ums.Domain.Authorization.Role.Role;
using ModuleEntity = Evolith.Ums.Domain.Authorization.SystemSuite.Module.Module;
using MenuNodeEntity = Evolith.Ums.Domain.Authorization.SystemSuite.MenuNode.MenuNode;
using Evolith.Ums.Domain.Enums;

public static class AuthorizationDevDataSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        var suiteRepository = serviceProvider.GetService<ISystemSuiteRepository>();
        var inMemorySuiteRepository = serviceProvider.GetService<InMemorySystemSuiteRepository>();

        var roleRepository = serviceProvider.GetService<IRoleRepository>();
        var inMemoryRoleRepository = serviceProvider.GetService<InMemoryRoleRepository>();

        var templateRepository = serviceProvider.GetService<IPermissionTemplateRepository>();
        var inMemoryTemplateRepository = serviceProvider.GetService<InMemoryPermissionTemplateRepository>();

        var profileRepository = serviceProvider.GetService<IProfileRepository>();
        var inMemoryProfileRepository = serviceProvider.GetService<InMemoryProfileRepository>();

        var actor = ActorId.Create(CoreDevDataSeeder.SystemActorId);

        var allTenantIds = new[]
        {
            TenantId.Load(Guid.Parse(CoreDevDataSeeder.InternalAdminTenantId)),           // INTERNAL_ADMIN
            TenantId.Load(Guid.Parse(CoreDevDataSeeder.RansaTenantId)),                  // RANSA_PERU
            TenantId.Load(Guid.Parse("c9b736b4-6a84-48f8-b34d-176bc5a6d542")),           // NEPTUNIA
            TenantId.Load(Guid.Parse("a3f5b9d2-7c3d-4c8e-a9b0-123456789abc")),           // APM_CALLAO
            TenantId.Load(Guid.Parse("9e8d7c6b-5a4f-3e2d-1c0b-9876543210fe")),           // PAITA_PORT
            TenantId.Load(Guid.Parse(CoreDevDataSeeder.EvolithTenantId)),                 // EVOLITH
            TenantId.Load(Guid.Parse("f3e2d1c0-b9a8-7f6e-5d4c-321098765432")),           // INTRADEVCO
            TenantId.Load(Guid.Parse(CoreDevDataSeeder.ComexAndinaTenantId)),            // COMEX_ANDINA (EVOLITH client)
            TenantId.Load(Guid.Parse(CoreDevDataSeeder.AgronorteTenantId)),             // AGRONORTE (EVOLITH client)
            TenantId.Load(Guid.Parse(CoreDevDataSeeder.ImpoAndinaSubTenantId)),         // IMPO_ANDINA_SUB (cliente de COMEX_ANDINA)
        };

        foreach (var tenantId in allTenantIds)
        {
            // Seed SystemSuites
            var suites = BuildSeedSystemSuites(tenantId, actor);
            if (inMemorySuiteRepository is not null)
            {
                foreach (var suite in suites) inMemorySuiteRepository.Seed(suite);
            }
            else if (suiteRepository is not null)
            {
                // La guarda es POR SUITE, no por inquilino.
                //
                // Antes la condición era «el inquilino no tiene ninguna suite»: todo o nada.
                // Bastaba que tuviera UNA de cualquier otro sembrador para que este no añadiera
                // ninguna de las suyas, ni entonces ni nunca. Así se quedó EVOLITH sin la suite
                // UMS —tenía las siete de su catálogo, luego la cuenta no era cero, luego nada
                // que hacer— mientras los seis inquilinos genéricos sí la recibían. Un sembrador
                // que solo actúa sobre una base vacía no siembra: inicializa una vez y después
                // miente.
                var existing = await suiteRepository.GetByTenantIdAsync(tenantId.GetValue(), cancellationToken);
                var yaPresentes = existing.Select(s => s.Code.GetValue()).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var faltantes = suites.Where(s => !yaPresentes.Contains(s.Code.GetValue())).ToList();

                if (faltantes.Count > 0)
                {
                    foreach (var suite in faltantes) await suiteRepository.AddAsync(suite, cancellationToken);
                    await suiteRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);
                    existing = await suiteRepository.GetByTenantIdAsync(tenantId.GetValue(), cancellationToken);
                }

                await EnsureDomainResourcesAsync(existing, actor, suiteRepository, cancellationToken);
                suites = existing; // se usan las persistidas para que los ids casen con roles y plantillas
            }

            // Seed Roles
            var roles = BuildSeedRoles(tenantId, suites, actor);
            if (inMemoryRoleRepository is not null)
            {
                foreach (var role in roles) inMemoryRoleRepository.Seed(role);
            }
            else if (roleRepository is not null)
            {
                // Igual que arriba: por código de rol, no por inquilino. Los roles de una suite
                // recién añadida no llegarían nunca si basta con que el inquilino tenga otros.
                var existing = await roleRepository.GetByTenantIdAsync(tenantId.GetValue(), cancellationToken);
                var yaPresentes = existing.Select(r => r.Code.GetValue()).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var faltantes = roles.Where(r => !yaPresentes.Contains(r.Code.GetValue())).ToList();

                if (faltantes.Count > 0)
                {
                    foreach (var role in faltantes) await roleRepository.AddAsync(role, cancellationToken);
                    await roleRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);
                    existing = await roleRepository.GetByTenantIdAsync(tenantId.GetValue(), cancellationToken);
                }

                roles = existing;
            }

            // Seed PermissionTemplates
            var templates = BuildSeedPermissionTemplates(tenantId, suites, roles, actor);
            if (inMemoryTemplateRepository is not null)
            {
                foreach (var template in templates) inMemoryTemplateRepository.Seed(template);
            }
            else if (templateRepository is not null)
            {
                // Por ROL: una plantilla pertenece a un rol, así que un rol nuevo necesita la suya
                // aunque el inquilino ya tenga plantillas de otros roles.
                var existing = await templateRepository.GetByTenantIdAsync(tenantId.GetValue(), cancellationToken);
                var rolesConPlantilla = existing.Select(p => p.RoleId.GetValue()).ToHashSet();
                var faltantes = templates.Where(p => !rolesConPlantilla.Contains(p.RoleId.GetValue())).ToList();

                if (faltantes.Count > 0)
                {
                    foreach (var template in faltantes) await templateRepository.AddAsync(template, cancellationToken);
                    await templateRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);
                    existing = await templateRepository.GetByTenantIdAsync(tenantId.GetValue(), cancellationToken);
                }

                templates = existing;
            }

            // Seed Profiles
            var profiles = BuildSeedProfiles(tenantId, roles, templates, actor);
            if (inMemoryProfileRepository is not null)
            {
                foreach (var profile in profiles) inMemoryProfileRepository.Seed(profile);
            }
            else if (profileRepository is not null)
            {
                // Por par (usuario, rol): es la clave natural de un perfil. Sin esto, el usuario
                // que ya tiene perfil en otra suite jamás recibiría el de una suite nueva —que es
                // justo lo que hacía falta para que quien administra UMS pudiera entrar a UMS.
                var existing = await profileRepository.GetByTenantIdAsync(tenantId.GetValue(), cancellationToken);
                var yaAsignados = existing
                    .Select(p => (Usuario: p.UserId.GetValue(), Rol: p.RoleId.GetValue()))
                    .ToHashSet();
                var faltantes = profiles
                    .Where(p => !yaAsignados.Contains((p.UserId.GetValue(), p.RoleId.GetValue())))
                    .ToList();

                if (faltantes.Count > 0)
                {
                    foreach (var profile in faltantes) await profileRepository.AddAsync(profile, cancellationToken);
                    await profileRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);
                }

                if (tenantId.GetValue() == Guid.Parse(CoreDevDataSeeder.InternalAdminTenantId))
                {
                    await EnsureInternalAdminProfileAsync(profileRepository, roleRepository, templateRepository, cancellationToken);
                }
            }
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Security Hotspot", "S3011:Reflection should not be used to increase accessibility of classes, methods, or fields",
        Justification = "ADR-UMS-099: reflexión sancionada en seeder de desarrollo/UAT. La siembra determinista con " +
                        "IDs bien conocidos exige fijar identidades sobre miembros no públicos; no se ejecuta en " +
                        "producción (SeedDevData && !IsProduction).")]
    private static readonly BindingFlags PrivateInstanceFlags = BindingFlags.Instance | BindingFlags.NonPublic;

    // ── FS-25 dispatch: EVOLITH gets the 6 domain suites/roles/templates/profiles; its
    //    client tenants get a Portal-only subset; every other tenant keeps the generic set.
    private static readonly Guid[] EvolithClientTenantIds =
    {
        Guid.Parse(CoreDevDataSeeder.ComexAndinaTenantId),
        Guid.Parse(CoreDevDataSeeder.AgronorteTenantId),
        Guid.Parse(CoreDevDataSeeder.FrupiuraTenantId),
        Guid.Parse(CoreDevDataSeeder.ImpoAndinaSubTenantId), // cliente de mi cliente — mismo trato acotado
    };

    private static bool IsEvolith(TenantId tenantId) => tenantId.GetValue() == Guid.Parse(CoreDevDataSeeder.EvolithTenantId);

    private static bool IsEvolithClient(TenantId tenantId) => Array.IndexOf(EvolithClientTenantIds, tenantId.GetValue()) >= 0;

    private static IReadOnlyList<SystemSuiteAggregate> BuildSeedSystemSuites(TenantId tenantId, ActorId actor)
    {
        if (IsEvolith(tenantId))
        {
            return BuildEvolithSystemSuites(tenantId, actor);
        }

        if (IsEvolithClient(tenantId))
        {
            var portal = BuildPortalClienteSuite(tenantId, actor);
            return portal is null ? Array.Empty<SystemSuiteAggregate>() : new[] { portal };
        }

        return BuildGenericSystemSuites(tenantId, actor);
    }

    private static IReadOnlyList<RoleAggregate> BuildSeedRoles(TenantId tenantId, IReadOnlyList<SystemSuiteAggregate> suites, ActorId actor)
    {
        if (IsEvolith(tenantId))
        {
            return BuildEvolithRoles(tenantId, suites, actor);
        }

        if (IsEvolithClient(tenantId))
        {
            return BuildClienteExternoRoles(tenantId, suites, actor);
        }

        return BuildGenericRoles(tenantId, suites, actor);
    }

    private static IReadOnlyList<PermissionTemplateAggregate> BuildSeedPermissionTemplates(TenantId tenantId, IReadOnlyList<SystemSuiteAggregate> suites, IReadOnlyList<RoleAggregate> roles, ActorId actor)
    {
        if (IsEvolith(tenantId) || IsEvolithClient(tenantId))
        {
            return BuildRoleAnchoredPermissionTemplates(tenantId, suites, roles, actor);
        }

        return BuildGenericPermissionTemplates(tenantId, suites, roles, actor);
    }

    private static IReadOnlyList<ProfileAggregate> BuildSeedProfiles(TenantId tenantId, IReadOnlyList<RoleAggregate> roles, IReadOnlyList<PermissionTemplateAggregate> templates, ActorId actor)
    {
        if (IsEvolith(tenantId))
        {
            return BuildEvolithProfiles(tenantId, roles, templates, actor);
        }

        if (IsEvolithClient(tenantId))
        {
            return BuildClienteProfiles(tenantId, roles, templates, actor);
        }

        return BuildGenericProfiles(tenantId, roles, templates, actor);
    }

    // ── FS-25 suite tree builder (module → menu → submenu → options) ─────────────
    private sealed record OptionSpec(string Code, string Name, string Action);
    private sealed record MenuSpec(string Code, string Name, OptionSpec[] Options);
    private sealed record ModuleSpec(string Code, string Name, MenuSpec[] Menus);
    private sealed record SuiteSpec(string Code, string Name, string Description, ModuleSpec[] Modules);

    private static readonly (string Code, string Name)[] StandardSuiteActions =
    {
        ("VIEW", "Ver"),
        ("CREATE", "Crear"),
        ("READ", "Leer"),
        ("UPDATE", "Actualizar"),
        ("DELETE", "Eliminar"),
        ("APPROVE", "Aprobar"),
        ("SEARCH", "Buscar"),
    };

    // A menu with the two standard options (view + manage). Option codes stay unique
    // per suite because menu codes are unique per suite.
    private static MenuSpec Menu(string code, string name) => new(code, name, new[]
    {
        new OptionSpec("VIEW_" + code, "Ver " + name, "VIEW"),
        new OptionSpec("MANAGE_" + code, "Gestionar " + name, "UPDATE"),
    });

    private static SuiteSpec PortalClienteSuiteSpec() => new(
        "PORTAL_CLIENTE", "Portal del Cliente", "Portal de consulta acotado para clientes externos",
        new[]
        {
            new ModuleSpec("QUERY", "Consultas", new[] { Menu("FILE_STATUS", "Estado de Expediente"), Menu("DOCUMENTS", "Documentos") }),
            new ModuleSpec("NOTIF", "Notificaciones", new[] { Menu("NOTICES", "Avisos") }),
        });

    private static IReadOnlyList<SuiteSpec> EvolithSuiteSpecs() => new[]
    {
        new SuiteSpec("TMS", "Transporte", "Sistema de gestión de transporte", new[]
        {
            new ModuleSpec("PLAN", "Planificación", new[] { Menu("TORDERS", "Órdenes de Transporte"), Menu("ROUTES", "Rutas") }),
            new ModuleSpec("FLEET", "Flota", new[] { Menu("VEHICLES", "Vehículos"), Menu("DRIVERS", "Conductores") }),
            new ModuleSpec("TRACK", "Seguimiento", new[] { Menu("MONITOR", "Monitoreo"), Menu("MILESTONES", "Hitos") }),
        }),
        new SuiteSpec("WMS", "Almacén", "Sistema de gestión de almacén", new[]
        {
            new ModuleSpec("INV", "Inventario", new[] { Menu("STOCK", "Stock"), Menu("OPS", "Operaciones") }),
            new ModuleSpec("RCV", "Recepción y Despacho", new[] { Menu("RECEIPTS", "Recepciones"), Menu("DISPATCHES", "Despachos") }),
            new ModuleSpec("REPORTS", "Reportes", new[] { Menu("INV_REPORTS", "Reportes de Inventario"), Menu("IO", "Importar / Exportar") }),
        }),
        // SIL se retiró del catálogo de EVOLITH: era un sistema de PRUEBAS y su presencia tenía un
        // efecto que no se veía al escribirlo. Su rol ADMINISTRADOR y el ADMIN_UMS comparten nivel
        // de jerarquía 0, así que el desempate del grafo caía al código de suite por orden
        // alfabético —«SIL» antes que «UMS»— y el administrador de la suite aterrizaba en un
        // sistema de demostración en vez de en UMS. Sus menús además apuntaban a rutas que la
        // aplicación web no declara, de modo que ninguna opción abría formulario ni lista.
        new SuiteSpec("ADUANAS", "Aduanas", "Declaraciones, canales y agentes de aduana", new[]
        {
            new ModuleSpec("DECL", "Declaraciones", new[] { Menu("DUA_IMPO", "DUA Importación"), Menu("DAM_EXPO", "DAM Exportación") }),
            new ModuleSpec("CHANNEL", "Canales", new[] { Menu("CH_ROJO", "Canal Rojo"), Menu("CH_NARANJA", "Canal Naranja"), Menu("CH_VERDE", "Canal Verde") }),
            new ModuleSpec("AGENT", "Agentes", new[] { Menu("CUSTOMS_AGENTS", "Agentes de Aduana"), Menu("POWERS", "Poderes") }),
        }),
        PortalClienteSuiteSpec(),
        new SuiteSpec("FACTURACION", "Facturación", "Facturación, liquidación y cobranzas", new[]
        {
            new ModuleSpec("BILL", "Facturación", new[] { Menu("INVOICES", "Facturas"), Menu("CREDIT_NOTES", "Notas de Crédito") }),
            new ModuleSpec("SETTLE", "Liquidación", new[] { Menu("SETTLEMENTS", "Liquidaciones") }),
            new ModuleSpec("COLLECT", "Cobranzas", new[] { Menu("ACCOUNT_STATE", "Estado de Cuenta") }),
        }),
        // UMS NO se declara aquí. Lo aporta `BuildEvolithSystemSuites` reutilizando la suite
        // canónica de `BuildGenericSystemSuites`, y la razón es que este mecanismo de `SuiteSpec`
        // DERIVA las rutas de navegación del código de módulo y menú (`/idm/view-users`,
        // `/auth/view-templates`…), que es correcto para los sistemas de demostración —no tienen
        // interfaz propia— pero no para UMS, cuya aplicación web declara rutas reales y distintas
        // (`/users`, `/permission-templates`, `/system-suites`…).
        //
        // Cuando UMS se declaraba aquí, el grafo entregaba menús cuyas rutas la aplicación no
        // conocía: el enrutador caía al comodín y ninguna opción abría formulario ni lista. Y sus
        // códigos de menú (`TEMPLATES`, `SUITES`, `PARAMS`, `FLAGS`, `APPCONF`) tampoco coincidían
        // con los que exige el `AccessGuard` (`PERMISSION_TEMPLATES`, `SYSTEM_SUITES`,
        // `PARAM_CATALOG`, `FEATURE_FLAGS`, `APP_CONFIG`), así que la autorización habría fallado
        // aunque las rutas existieran.
    };

    /// <summary>
    /// Ajustes VISIBLES para el cliente: branding, tema, disposición e idioma.
    ///
    /// Son los que permiten al frontend inicializar la aplicación sin llamadas adicionales
    /// (G-178). Van marcados uno a uno con `isClientVisible: true` a propósito: el default es no
    /// publicar, porque esta misma bolsa alberga ajustes operativos que no deben salir del
    /// servidor.
    ///
    /// La convención de clave es `ESPACIO_RESTO`, que el grafo proyecta como
    /// `settings.espacio.resto`.
    /// </summary>
    private static void SembrarAjustesDeCliente(SystemSuiteAggregate suite, SuiteSpec spec, ActorId actor)
    {
        void Visible(string clave, string valor) =>
            suite.AddAppSetting(
                ConfigurationKey.Create(clave),
                ConfigurationValue.Create(valor),
                ConfigurationScope.Global,
                actor,
                isClientVisible: true);

        Visible("BRAND_DISPLAY_NAME", spec.Name);
        Visible("BRAND_SHORT_NAME", spec.Code);
        // Descriptor bajo el nombre comercial. Es la descripción del sistema, no una frase de
        // marketing: quien lee la barra quiere saber en qué sistema está, no que se lo vendan.
        Visible("BRAND_TAGLINE", spec.Description);
        Visible("BRAND_LOGO_URL", $"/branding/{spec.Code.ToLowerInvariant()}/logo.svg");
        Visible("BRAND_ICON_URL", $"/branding/{spec.Code.ToLowerInvariant()}/icon.svg");

        // Azul corporativo de Evolith; el acento y la superficie derivan de él.
        Visible("THEME_PRIMARY", "#0f3e67");
        Visible("THEME_ACCENT", "#27ae60");
        Visible("THEME_MODE", "system");

        // Disposición del shell. El cliente entiende `nav-rail` y `nav-rail-compact`; cualquier
        // otro valor cae en la primera en vez de dejar la pantalla sin navegación.
        Visible("UI_LAYOUT", "nav-rail");
        Visible("UI_HOME_ROUTE", "/");
        Visible("UI_DENSITY", "comfortable");

        Visible("LOCALE_LANGUAGE", "es-PE");
        Visible("LOCALE_TIMEZONE", "America/Lima");
        Visible("LOCALE_CURRENCY", "PEN");

        // Contraejemplo deliberado: un ajuste operativo del mismo sistema que NO se publica.
        // Si alguien vuelca la bolsa entera por comodidad, esta clave aparecería en el cable.
        suite.AddAppSetting(
            ConfigurationKey.Create("OPS_HEALTHCHECK_INTERVAL_S"),
            ConfigurationValue.Create("30"),
            ConfigurationScope.Global,
            actor);
    }

    private static SystemSuiteAggregate? BuildSuiteFromSpec(TenantId tenantId, ActorId actor, SuiteSpec spec)
    {
        var result = SystemSuiteAggregate.Create(tenantId, Code.Create(spec.Code), Name.Create(spec.Name), Description.Create(spec.Description), actor);
        if (result.IsFailure)
        {
            return null;
        }

        var suite = result.Value;

        foreach (var action in StandardSuiteActions)
        {
            suite.RegisterAction(ActionCode.Create(action.Code), Name.Create(action.Name), actor);
        }

        SembrarAjustesDeCliente(suite, spec, actor);

        var moduleOrder = 1;
        foreach (var moduleSpec in spec.Modules)
        {
            var moduleResult = suite.AddModule(Code.Create(moduleSpec.Code), Name.Create(moduleSpec.Name), Description.Create(moduleSpec.Name), moduleOrder++, actor, IconoDeModulo(moduleSpec.Code));
            if (moduleResult.IsFailure)
            {
                continue;
            }

            var module = suite.Modules.First(m => m.Code.GetValue() == moduleSpec.Code);
            suite.ActivateModule(module.Props.Id, actor);

            var menuOrder = 1;
            foreach (var menuSpec in moduleSpec.Menus)
            {
                var subMenuCode = menuSpec.Code + "_LIST";
                SeedNavMenu(
                    suite, module, menuOrder++,
                    menuSpec.Code, menuSpec.Name,
                    subMenuCode, menuSpec.Name,
                    menuSpec.Options.Select(o => (o.Code, o.Name, o.Action)).ToArray(),
                    actor);
            }
        }

        return suite;
    }

    /// <summary>
    /// Siembra un menú de navegación como árbol de nodos (ADR-0090): un nodo raíz
    /// <see cref="NodeKind.Menu"/>, un submenú <see cref="NodeKind.SubMenu"/> y
    /// las opciones hoja <see cref="NodeKind.Option"/> con su funcionalidad N:M.
    /// Reemplaza la antigua cadena rígida Menú/Submenú/Opción.
    /// </summary>
    private static void SeedNavMenu(
        SystemSuiteAggregate suite,
        ModuleEntity module,
        int menuOrder,
        string menuCode,
        string menuName,
        string subCode,
        string subName,
        (string Code, string Name, string Action)[] options,
        ActorId actor,
        string? rutaMenu = null)
    {
        // Icono en el menú —es el nivel que el usuario ve en la barra lateral— y ruta en la
        // opción, que es la que navega. Un submenú solo agrupa: ni icono ni ruta.
        suite.AddModuleRootNode(module.Props.Id, NodeKind.Menu, Code.Create(menuCode), Name.Create(menuName), Description.Create(menuName), menuOrder, actor,
            presentation: MenuNodePresentation.Create(IconoDeMenu(menuCode), rutaMenu));
        var menuNode = module.Nodes.First(n => n.Code.GetValue() == menuCode);

        suite.AddModuleChildNode(module.Props.Id, menuNode.GetId(), NodeKind.SubMenu, Code.Create(subCode), Name.Create(subName), Description.Create(subName), 1, actor);
        var subNode = menuNode.Children.First(n => n.Code.GetValue() == subCode);

        var optionOrder = 1;
        foreach (var opt in options)
        {
            // Con ruta real en el menú, las opciones son PERMISOS sobre esa pantalla, no destinos
            // distintos: darles una ruta inventada llenaría el menú de enlaces rotos.
            var ruta = rutaMenu is null
                ? $"/{module.Code.GetValue().ToLowerInvariant()}/{opt.Code.ToLowerInvariant().Replace('_', '-')}"
                : null;
            suite.AddModuleChildNode(module.Props.Id, subNode.GetId(), NodeKind.Option, Code.Create(opt.Code), Name.Create(opt.Name), Description.Create(opt.Name), optionOrder++, actor,
                presentation: MenuNodePresentation.Create(null, ruta));
            var optNode = subNode.Children.First(n => n.Code.GetValue() == opt.Code);
            suite.LinkModuleNodeAction(module.Props.Id, optNode.GetId(), ActionCode.Create(opt.Action), actor);
        }
    }

    /// <summary>
    /// Icono del módulo por convención sobre su código, con el mismo criterio que el del menú: un
    /// IDENTIFICADOR que el cliente resuelve, nunca un recurso.
    /// </summary>
    private static string IconoDeModulo(string moduleCode) => moduleCode switch
    {
        // Portal de administración de UMS.
        "IDM" => "shield-check",
        "AUTH" => "shield-check",
        "SYS" => "settings",
        "IGA" => "user-check",
        // Sistemas satélite del grupo.
        "INV" or "RCV" => "package",
        "REPORTS" => "file-text",
        "PLAN" or "FLEET" => "truck",
        "TRACK" or "TRACE" => "activity",
        "FILES" => "folder-kanban",
        "DECL" or "CHANNEL" => "file-text",
        "AGENT" => "users",
        "BILL" or "SETTLE" or "COLLECT" or "COST" => "receipt",
        "QUERY" => "inbox",
        "NOTIF" => "bell",
        _ => "layout-grid",
    };

    /// <summary>
    /// Icono del menú por convención sobre su código.
    ///
    /// Es un IDENTIFICADOR, no un recurso: el catálogo gráfico lo resuelve el cliente. Guardar
    /// aquí una URL o un SVG ataría el servidor a la biblioteca de iconos de un frontend concreto,
    /// y cambiarla obligaría a migrar datos.
    /// </summary>
    private static string IconoDeMenu(string menuCode) => menuCode switch
    {
        "SATELITES" or "TORDERS" or "VEHICLES" => "truck",
        "STOCK" or "INV_REPORTS" or "RECEIPTS" or "DISPATCHES" => "package",
        "FILES_IMPO" or "FILES_EXPO" or "DUA_IMPO" or "DAM_EXPO" => "file-text",
        "INVOICES" or "CREDIT_NOTES" or "SETTLEMENTS" or "ACCOUNT_STATE" => "receipt",
        "MONITOR" or "MILESTONES" or "TIMELINE" => "activity",
        "CUSTOMS_AGENTS" or "POWERS" or "DRIVERS" => "users",
        "NOTICES" => "bell",
        // Portal de administración de UMS: los identificadores casan con el registro del shell.
        "TENANTS" => "building",
        "USERS" => "users",
        "DELEGATIONS" => "git-merge",
        "SYSTEM_SUITES" => "cpu",
        "PERMISSION_TEMPLATES" or "PROFILES" => "shield-check",
        "FEATURE_FLAGS" => "flag",
        "APP_CONFIG" or "PARAM_CATALOG" => "settings",
        _ => "layout-grid",
    };

    /// <summary>Añade un nodo (raíz si <paramref name="parent"/> es nulo) y lo devuelve.</summary>
    private static MenuNodeEntity AddNode(
        SystemSuiteAggregate suite,
        ModuleEntity module,
        MenuNodeEntity? parent,
        NodeKind kind,
        string code,
        string name,
        int order,
        ActorId actor)
    {
        if (parent is null)
            suite.AddModuleRootNode(module.Props.Id, kind, Code.Create(code), Name.Create(name), Description.Create(name), order, actor);
        else
            suite.AddModuleChildNode(module.Props.Id, parent.GetId(), kind, Code.Create(code), Name.Create(name), Description.Create(name), order, actor);

        return FindNodeByCode(module, code)!;
    }

    /// <summary>Busca un nodo por código en todo el árbol recursivo del módulo (ADR-0090).</summary>
    private static MenuNodeEntity? FindNodeByCode(ModuleEntity module, string code)
    {
        static MenuNodeEntity? Search(IEnumerable<MenuNodeEntity> nodes, string code)
        {
            foreach (var n in nodes)
            {
                if (n.Code.GetValue() == code) return n;
                var found = Search(n.Children, code);
                if (found is not null) return found;
            }
            return null;
        }
        return Search(module.Nodes, code);
    }

    /// <summary>Enumera todos los nodos del árbol del módulo en preorden.</summary>
    private static IEnumerable<MenuNodeEntity> EnumerateNodes(ModuleEntity module)
    {
        static IEnumerable<MenuNodeEntity> Walk(IEnumerable<MenuNodeEntity> nodes)
        {
            foreach (var n in nodes)
            {
                yield return n;
                foreach (var c in Walk(n.Children))
                    yield return c;
            }
        }
        return Walk(module.Nodes);
    }

    private static IReadOnlyList<SystemSuiteAggregate> BuildEvolithSystemSuites(TenantId tenantId, ActorId actor)
    {
        var suites = EvolithSuiteSpecs()
            .Select(spec => BuildSuiteFromSpec(tenantId, actor, spec))
            .Where(suite => suite is not null)
            .Select(suite => suite!)
            .ToList();

        // EVOLITH opera el sistema de identidad de toda la suite, así que necesita UMS en su
        // catálogo. Se toma la MISMA suite que reciben los inquilinos genéricos en vez de
        // declararla por `SuiteSpec`: es la única que trae la navegación real de la aplicación web
        // —rutas y códigos de menú literales— en lugar de rutas derivadas del código de módulo.
        //
        // Reutilizarla, y no copiarla, es deliberado: un mismo sistema debe verse igual desde
        // cualquier inquilino. Dos definiciones paralelas ya divergieron una vez y el resultado fue
        // un UMS con menús que no llevaban a ninguna parte.
        var umsCanonica = BuildGenericSystemSuites(tenantId, actor)
            .FirstOrDefault(suite => suite.Code.GetValue() == "UMS");

        if (umsCanonica is not null)
        {
            suites.Add(umsCanonica);
        }

        return suites;
    }

    private static SystemSuiteAggregate? BuildPortalClienteSuite(TenantId tenantId, ActorId actor)
        => BuildSuiteFromSpec(tenantId, actor, PortalClienteSuiteSpec());

    // ── FS-25 §4.3 roles ─────────────────────────────────────────────────────────
    private static IReadOnlyList<RoleAggregate> BuildEvolithRoles(TenantId tenantId, IReadOnlyList<SystemSuiteAggregate> suites, ActorId actor)
    {
        var roles = new List<RoleAggregate>();

        void Add(string code, string name, string description, string suiteCode)
        {
            var suite = suites.FirstOrDefault(s => s.Code.GetValue() == suiteCode);
            if (suite is null)
            {
                return;
            }

            var result = RoleAggregate.Create(tenantId, suite.GetId(), Code.Create(code), Name.Create(name), Description.Create(description), null, 0, 0, actor);
            if (result.IsSuccess)
            {
                roles.Add(result.Value);
            }
        }

        Add("AGENTE_ADUANAS", "Agente de Aduanas", "Gestiona declaraciones y trámites aduaneros", "ADUANAS");
        Add("DESPACHADOR", "Despachador", "Ejecuta despachos aduaneros y de expedientes", "ADUANAS");
        Add("JEFE_ALMACEN", "Jefe de Almacén", "Dirige las operaciones de almacén", "WMS");
        Add("OPERARIO_ALMACEN", "Operario de Almacén", "Ejecuta operaciones físicas de almacén", "WMS");
        Add("COORD_TRANSPORTE", "Coordinador de Transporte", "Coordina la planificación y la flota de transporte", "TMS");
        Add("CLIENTE_EXTERNO", "Cliente Externo", "Acceso acotado de solo lectura al Portal del Cliente", "PORTAL_CLIENTE");
        // Los roles EJECUTIVO_CUENTA, ANALISTA_DOC, AUDITOR y ADMINISTRADOR colgaban del SIL,
        // retirado del catálogo por ser un sistema de pruebas. `Add` ignora los roles cuya suite
        // no existe y el mapeo de perfiles descarta los que no resuelven, así que su ausencia no
        // rompe la siembra: simplemente esos perfiles de demostración dejan de crearse.

        // Administración del propio UMS. Se separa en tres y no en uno porque quien da de alta a
        // un operario de almacén no es quien decide qué permisos concede un rol: la primera es
        // tarea diaria de mesa de ayuda, la segunda cambia el modelo de acceso de toda la suite.
        // Un único rol «administrador de UMS» obligaría a conceder lo segundo para poder hacer lo
        // primero.
        Add("ADMIN_UMS", "Administrador de UMS", "Gobierna identidad, autorización y configuración de la suite", "UMS");
        Add("MESA_ACCESOS", "Mesa de Accesos", "Da de alta usuarios y asigna perfiles; no altera roles ni plantillas", "UMS");
        Add("AUDITOR_ACCESOS", "Auditor de Accesos", "Consulta quién pudo hacer qué, sin poder cambiarlo", "UMS");

        return roles;
    }

    private static IReadOnlyList<RoleAggregate> BuildClienteExternoRoles(TenantId tenantId, IReadOnlyList<SystemSuiteAggregate> suites, ActorId actor)
    {
        var roles = new List<RoleAggregate>();
        var portal = suites.FirstOrDefault(s => s.Code.GetValue() == "PORTAL_CLIENTE");
        if (portal is null)
        {
            return roles;
        }

        var result = RoleAggregate.Create(tenantId, portal.GetId(), Code.Create("CLIENTE_EXTERNO"), Name.Create("Cliente Externo"), Description.Create("Acceso acotado de solo lectura al Portal del Cliente"), null, 0, 0, actor);
        if (result.IsSuccess)
        {
            roles.Add(result.Value);
        }

        return roles;
    }

    // ── FS-25 permission templates — one published template per role, anchored to
    //    the role's suite; a role's template never references another suite. ────────
    private static IReadOnlyList<PermissionTemplateAggregate> BuildRoleAnchoredPermissionTemplates(TenantId tenantId, IReadOnlyList<SystemSuiteAggregate> suites, IReadOnlyList<RoleAggregate> roles, ActorId actor)
    {
        var templates = new List<PermissionTemplateAggregate>();

        foreach (var role in roles)
        {
            var suite = suites.FirstOrDefault(s => s.GetId().GetValue() == role.SystemSuiteId.GetValue());
            if (suite is null)
            {
                continue;
            }

            var templateResult = PermissionTemplateAggregate.Create(tenantId, role.GetId(), suite.GetId(), actor);
            if (templateResult.IsFailure)
            {
                continue;
            }

            var template = templateResult.Value;
            AddFullSuiteNavigation(template, suite, actor, ModulosVisiblesPara(role.Code.GetValue()));

            if (EsRolDeAdministracionTotal(role.Code.GetValue()))
            {
                AddFullDomainResourceAccess(template, suite, actor);
            }

            // El orden importa: una plantilla publicada queda congelada y ya no admite ítems.
            template.Publish(actor);
            templates.Add(template);
        }

        return templates;
    }

    /// <summary>
    /// Módulos que ve un rol, o <c>null</c> para «todos».
    ///
    /// <para>Por omisión cada rol recibe la navegación completa de su suite, que es lo que hace
    /// falta para que la siembra sea navegable de inmediato. Pero en UMS eso dejaba a los tres
    /// roles viendo exactamente lo mismo —24 alcances cada uno—, y tres roles indistinguibles no
    /// permiten comprobar lo único que un sistema de permisos tiene que demostrar: que la interfaz
    /// cambia según quién entra. Aquí la diferencia es el dato, no un adorno.</para>
    ///
    /// <para>El corte es por módulo y no por acción porque es el eje que el grafo proyecta al
    /// frontend: quien no tiene el módulo no ve su menú. Un corte más fino exigiría modelar
    /// acciones por nodo, que es otra decisión.</para>
    /// </summary>
    private static string[]? ModulosVisiblesPara(string roleCode) => roleCode switch
    {
        // Gobierna la identidad de la suite: identidad, autorización y configuración.
        "ADMIN_UMS" => null,
        // Da de alta usuarios y asigna perfiles. No toca la configuración del sistema: cambiar un
        // parámetro global no es tarea de mesa de ayuda.
        "MESA_ACCESOS" => new[] { "IDM", "AUTH" },
        // Consulta quién pudo hacer qué. Vive en el modelo de acceso —roles, perfiles, plantillas—
        // y no necesita el padrón de usuarios para responder esa pregunta.
        "AUDITOR_ACCESOS" => new[] { "AUTH" },
        _ => null,
    };

    /// <summary>
    /// Roles que la siembra de desarrollo trata como ADMINISTRACIÓN TOTAL de su suite: además de
    /// ver toda la navegación, pueden operar sobre todos los objetos de dominio.
    ///
    /// <para>Solo aplica a DESARROLLO y UAT. El propósito es que quien monta un entorno desde cero
    /// pueda ejercitar el sistema entero sin ir concediendo permisos a mano; en producción los
    /// permisos se otorgan por plantilla explícita, no por pertenecer a una lista de códigos.</para>
    /// </summary>
    private static bool EsRolDeAdministracionTotal(string roleCode) =>
        roleCode.Equals("ADMIN_UMS", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Concede toda acción sobre todo objeto de dominio de la suite.
    ///
    /// <para>Se concede CADA acción real y no un <c>ActionId</c> cualquiera: el grafo es
    /// fail-closed (G-039) y evalúa el efecto por el par (recurso, acción), así que un identificador
    /// de acción inventado no concedería absolutamente nada — y la plantilla parecería completa
    /// mientras el usuario se topa con un «no autorizado» en cada pantalla.</para>
    ///
    /// <para>Es la misma construcción que ya usaba la plantilla de administrador del dataset
    /// genérico; se extrae aquí para que la de UMS no quede con navegación pero sin dominio, que era
    /// la diferencia real entre «ve todos los menús» y «puede hacer todo».</para>
    /// </summary>
    private static void AddFullDomainResourceAccess(
        PermissionTemplateAggregate template,
        SystemSuiteAggregate suite,
        ActorId actor)
    {
        foreach (var resource in suite.DomainResources)
        {
            var targetType = resource.Type == DomainResourceType.Aggregate
                ? ExclusiveArcTarget.Aggregate
                : ExclusiveArcTarget.Entity;

            foreach (var action in suite.Actions)
            {
                template.AddItem(targetType, resource.Id, action.GetId(), true, false, actor);
            }
        }
    }

    private static void AddFullSuiteNavigation(
        PermissionTemplateAggregate template,
        SystemSuiteAggregate suite,
        ActorId actor,
        string[]? modulosVisibles = null)
    {
        template.AddItem(ExclusiveArcTarget.SystemSuite, suite.GetId(), ActionId.Create(), true, false, actor);
        foreach (var module in suite.Modules)
        {
            if (modulosVisibles is not null
                && !modulosVisibles.Contains(module.Code.GetValue(), StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            template.AddItem(ExclusiveArcTarget.Module, module.Props.Id, ActionId.Create(), true, false, actor);
            foreach (var root in module.Nodes)
            {
                AddNodeNavigation(template, root, suite, actor);
            }
        }
    }

    // Recorre el árbol de nodos (ADR-0090) mapeando el rol del nodo al destino
    // del permiso: Menú → Submodule; Submenú/Opción → Option.
    private static void AddNodeNavigation(PermissionTemplateAggregate template, MenuNodeEntity node, SystemSuiteAggregate suite, ActorId actor)
    {
        if (node.Kind == NodeKind.Option)
        {
            // Opción hoja: el grafo evalúa el efecto aquí casando (optionId, actionId).
            // Debe usar el ActionId REAL de cada acción vinculada (N:M, ADR-0090); un
            // ActionId aleatorio nunca casaría y todo resolvería NotGranted (G-038).
            AddOptionActionItems(template, node, suite, actor);
        }
        else
        {
            // Nodo estructural (Menú → Submodule; Submenú → Option): concede la
            // navegación al nodo. El grafo no evalúa efecto a este nivel, por lo que
            // el ActionId es indiferente.
            var target = node.Kind == NodeKind.Menu ? ExclusiveArcTarget.Submodule : ExclusiveArcTarget.Option;
            template.AddItem(target, node.GetId(), ActionId.Create(), true, false, actor);
        }

        foreach (var child in node.Children)
        {
            AddNodeNavigation(template, child, suite, actor);
        }
    }

    // Añade una fila de permiso de opción por cada acción REAL vinculada al nodo
    // (N:M, ADR-0090), usando el ActionId de la Action registrada en la suite. Es
    // la clave del concesión efectiva: AuthorizationGraphBuilderService casa
    // (optionId, actionId real) — con un ActionId aleatorio todo sería NotGranted (G-038).
    private static void AddOptionActionItems(
        PermissionTemplateAggregate template,
        MenuNodeEntity optionNode,
        SystemSuiteAggregate suite,
        ActorId actor,
        bool isAllowed = true,
        bool isDenied = false)
    {
        foreach (var actionCode in optionNode.ActionCodes)
        {
            var action = suite.Actions.FirstOrDefault(a => a.Code.GetValue() == actionCode.GetValue());
            if (action is null)
            {
                continue;
            }

            template.AddItem(ExclusiveArcTarget.Option, optionNode.GetId(), action.GetId(), isAllowed, isDenied, actor);
        }
    }

    // ── FS-25 §4.4 profiles — one per internal user, linked by deterministic GUID.
    //    Each profile groups the conceptual "perfil" roles via their published templates.
    private static IReadOnlyList<ProfileAggregate> BuildEvolithProfiles(TenantId tenantId, IReadOnlyList<RoleAggregate> roles, IReadOnlyList<PermissionTemplateAggregate> templates, ActorId actor)
    {
        var profiles = new List<ProfileAggregate>();

        var baseBytes = tenantId.GetValue().ToByteArray();
        Guid UserGuid(byte idx)
        {
            var b = (byte[])baseBytes.Clone();
            b[0] = idx;
            return new Guid(b);
        }

        var roleByCode = roles
            .GroupBy(r => r.Code.GetValue())
            .ToDictionary(g => g.Key, g => g.First());
        var templateByRoleId = templates
            .GroupBy(t => t.RoleId.GetValue())
            .ToDictionary(g => g.Key, g => g.First());

        // (user index, primary role, roles grouped by the conceptual profile) — FS-25 §4.3/§4.4.
        var opAduanas = new[] { "AGENTE_ADUANAS", "DESPACHADOR", "ANALISTA_DOC" };
        var almacen = new[] { "JEFE_ALMACEN", "OPERARIO_ALMACEN" };
        var admin = new[] { "ADMINISTRADOR", "AUDITOR" };

        // Los tres perfiles de UMS son ESCALONADOS y no equivalentes: quien gobierna ve lo de la
        // mesa y lo del auditor; la mesa ve lo del auditor; el auditor solo lo suyo. Es lo que
        // hace comparable el grafo entre ellos — si los tres cargaran las mismas plantillas, la
        // prueba de que la interfaz cambia por rol no probaría nada.
        var umsAuditoria = new[] { "AUDITOR_ACCESOS" };
        var umsMesa = new[] { "MESA_ACCESOS", "AUDITOR_ACCESOS" };
        var umsGobierno = new[] { "ADMIN_UMS", "MESA_ACCESOS", "AUDITOR_ACCESOS" };
        var mapping = new (byte Index, string RoleCode, string[] TemplateRoleCodes)[]
        {
            (1, "ADMINISTRADOR", admin),               // admin.callao — PERF_ADMIN (admin por sucursal)
            (2, "AGENTE_ADUANAS", opAduanas),          // agente.aduanas.callao — PERF_OP_ADUANAS
            (3, "DESPACHADOR", opAduanas),             // despachador.callao — PERF_OP_ADUANAS
            (4, "JEFE_ALMACEN", almacen),              // jefe.almacen.callao — PERF_ALMACEN
            (5, "COORD_TRANSPORTE", new[] { "COORD_TRANSPORTE" }),   // coordinador.transporte.callao — PERF_TRANSPORTE
            (6, "EJECUTIVO_CUENTA", new[] { "EJECUTIVO_CUENTA" }),   // ejecutivo.cuenta.callao — PERF_COMERCIAL
            (7, "ANALISTA_DOC", opAduanas),            // analista.doc.callao — PERF_OP_ADUANAS
            (8, "AUDITOR", admin),                     // auditor.callao — PERF_ADMIN
            (CoreDevDataSeeder.EvolithJefeAlmacenPaitaUserIndex, "JEFE_ALMACEN", almacen), // jefe.almacen.paita — criterio 7
            (10, "OPERARIO_ALMACEN", almacen),         // operario.almacen.paita — PERF_ALMACEN
            (11, "AGENTE_ADUANAS", opAduanas),         // agente.aduanas.paita — PERF_OP_ADUANAS
            (12, "EJECUTIVO_CUENTA", new[] { "EJECUTIVO_CUENTA" }), // ejecutivo.cuenta.paita — PERF_COMERCIAL
            (CoreDevDataSeeder.EvolithRootAdminUserIndex, "ADMINISTRADOR", admin), // admin@evolith.example — root EVOLITH transversal

            // Perfiles sobre el propio UMS. Van como entradas APARTE y no añadiendo el rol a las
            // de arriba: administrar la suite logística y administrar la identidad de la suite son
            // dos responsabilidades distintas, y separarlas en dos perfiles del mismo usuario es lo
            // que permite comprobar el cambio de perfil —la interfaz debe cambiar al conmutar—.
            (CoreDevDataSeeder.EvolithRootAdminUserIndex, "ADMIN_UMS", umsGobierno), // admin@evolith.example
            (1, "MESA_ACCESOS", umsMesa),                                            // admin.callao
            (8, "AUDITOR_ACCESOS", umsAuditoria),                                    // auditor.callao
        };

        foreach (var entry in mapping)
        {
            if (!roleByCode.TryGetValue(entry.RoleCode, out var primaryRole))
            {
                continue;
            }

            var profileResult = ProfileAggregate.Create(tenantId, UserId.Load(UserGuid(entry.Index)), primaryRole.GetId(), null, actor);
            if (profileResult.IsFailure)
            {
                continue;
            }

            var profile = profileResult.Value;
            foreach (var templateRoleCode in entry.TemplateRoleCodes)
            {
                if (roleByCode.TryGetValue(templateRoleCode, out var templateRole)
                    && templateByRoleId.TryGetValue(templateRole.GetId().GetValue(), out var template))
                {
                    profile.AssignTemplate(template, actor);
                }
            }

            profiles.Add(profile);
        }

        return profiles;
    }

    private static IReadOnlyList<ProfileAggregate> BuildClienteProfiles(TenantId tenantId, IReadOnlyList<RoleAggregate> roles, IReadOnlyList<PermissionTemplateAggregate> templates, ActorId actor)
    {
        var profiles = new List<ProfileAggregate>();

        var role = roles.FirstOrDefault(r => r.Code.GetValue() == "CLIENTE_EXTERNO");
        if (role is null)
        {
            return profiles;
        }

        var template = templates.FirstOrDefault(t => t.RoleId.GetValue() == role.GetId().GetValue());

        var baseBytes = tenantId.GetValue().ToByteArray();
        baseBytes[0] = 1; // single external user per client tenant
        var userId = UserId.Load(new Guid(baseBytes));

        var profileResult = ProfileAggregate.Create(tenantId, userId, role.GetId(), null, actor);
        if (profileResult.IsSuccess)
        {
            if (template is not null)
            {
                profileResult.Value.AssignTemplate(template, actor);
            }

            profiles.Add(profileResult.Value);
        }

        return profiles;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Security Hotspot", "S3011:Reflection should not be used to increase accessibility of classes, methods, or fields",
        Justification = "ADR-UMS-099: reflexión sancionada en seeder de desarrollo/UAT. Fija el ID de rol semilla " +
                        "(RiskScoreCalculator lo resuelve por id) sobre la propiedad Id de setter privado; no se " +
                        "ejecuta en producción (SeedDevData && !IsProduction).")]
    private static void SetRoleId(RoleAggregate role, Guid id)
    {
        // G-058: SetRoleId nunca fijaba el id — doble fallo enmascarado. (1) `_props` vive en la base
        // AggregateRoot<> y `GetField` no busca en clases base → el campo devolvía null; (2) RoleProps.Id
        // es una propiedad PÚBLICA (setter privado) y buscarla solo con NonPublic devolvía null. El
        // SetValue nunca corría (no-op silencioso), así que los roles demo (DemoAdminRoleId/
        // DemoOperatorRoleId) conservaban su GUID aleatorio y el RiskScoreCalculator no los resolvía por
        // id → `submit` IGA fallaba con 400. Fix: usar el getter PÚBLICO Props (RoleProps es clase, misma
        // instancia) y buscar la propiedad Id incluyendo Public.
        var idProperty = typeof(RoleProps).GetProperty(
            "Id", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        idProperty?.SetValue(role.Props, RoleId.Load(id));
    }

    private static IReadOnlyList<RoleAggregate> BuildGenericRoles(TenantId tenantId, IReadOnlyList<SystemSuiteAggregate> suites, ActorId actor)
    {
        var roles = new List<RoleAggregate>();
        if (suites.Count == 0) return roles;

        // ── UMS Suite Roles ──────────────────────────────────────────────────────
        var adminRoleResult = RoleAggregate.Create(tenantId, suites[0].GetId(), Code.Create("ADMIN"), Name.Create("Administrador del Sistema"), Description.Create("Acceso administrativo completo"), null, 0, 0, actor);
        if (adminRoleResult.IsSuccess)
        {
            var role = adminRoleResult.Value;
            if (tenantId.GetValue() == Guid.Parse(CoreDevDataSeeder.RansaTenantId))
                SetRoleId(role, Guid.Parse(CoreDevDataSeeder.DemoAdminRoleId));
            roles.Add(role);
        }

        var supervisorRoleResult = RoleAggregate.Create(tenantId, suites[0].GetId(), Code.Create("SUPERVISOR"), Name.Create("Supervisor"), Description.Create("Supervisa las operaciones principales"), null, 0, 0, actor);
        if (supervisorRoleResult.IsSuccess) roles.Add(supervisorRoleResult.Value);

        var auditorRoleResult = RoleAggregate.Create(tenantId, suites[0].GetId(), Code.Create("AUDITOR"), Name.Create("Auditor de Cumplimiento"), Description.Create("Acceso de solo lectura para auditorías"), null, 0, 0, actor);
        if (auditorRoleResult.IsSuccess) roles.Add(auditorRoleResult.Value);

        var readonlyRoleResult = RoleAggregate.Create(tenantId, suites[0].GetId(), Code.Create("READONLY"), Name.Create("Solo Lectura"), Description.Create("Acceso de solo visualización, sin modificaciones"), null, 0, 0, actor);
        if (readonlyRoleResult.IsSuccess) roles.Add(readonlyRoleResult.Value);

        var dataEntryRoleResult = RoleAggregate.Create(tenantId, suites[0].GetId(), Code.Create("DATA_ENTRY"), Name.Create("Digitador"), Description.Create("Solo crear y actualizar registros"), null, 0, 0, actor);
        if (dataEntryRoleResult.IsSuccess) roles.Add(dataEntryRoleResult.Value);

        // ── WMS Suite Roles ──────────────────────────────────────────────
        if (suites.Count > 1)
        {
            var operatorRoleResult = RoleAggregate.Create(tenantId, suites[1].GetId(), Code.Create("OPERATOR"), Name.Create("Operario de Almacén"), Description.Create("Operaciones estándar de almacén"), null, 0, 0, actor);
            if (operatorRoleResult.IsSuccess)
            {
                var role = operatorRoleResult.Value;
                if (tenantId.GetValue() == Guid.Parse(CoreDevDataSeeder.RansaTenantId))
                    SetRoleId(role, Guid.Parse(CoreDevDataSeeder.DemoOperatorRoleId));
                roles.Add(role);
            }

            var inspectorRoleResult = RoleAggregate.Create(tenantId, suites[1].GetId(), Code.Create("INSPECTOR"), Name.Create("Inspector de Calidad"), Description.Create("Control de calidad e inspecciones"), null, 0, 0, actor);
            if (inspectorRoleResult.IsSuccess) roles.Add(inspectorRoleResult.Value);

            var managerRoleResult = RoleAggregate.Create(tenantId, suites[1].GetId(), Code.Create("WMS_MANAGER"), Name.Create("Jefe de Almacén"), Description.Create("Gestiona todas las operaciones de almacén"), null, 0, 0, actor);
            if (managerRoleResult.IsSuccess) roles.Add(managerRoleResult.Value);

            var dispatcherRoleResult = RoleAggregate.Create(tenantId, suites[1].GetId(), Code.Create("DISPATCHER"), Name.Create("Coordinador de Despacho"), Description.Create("Gestiona transferencias y despachos de stock"), null, 0, 0, actor);
            if (dispatcherRoleResult.IsSuccess) roles.Add(dispatcherRoleResult.Value);

            var reporterRoleResult = RoleAggregate.Create(tenantId, suites[1].GetId(), Code.Create("REPORTER"), Name.Create("Analista de Reportes"), Description.Create("Genera y exporta reportes de almacén"), null, 0, 0, actor);
            if (reporterRoleResult.IsSuccess) roles.Add(reporterRoleResult.Value);
        }

        return roles;
    }

    private static IReadOnlyList<SystemSuiteAggregate> BuildGenericSystemSuites(TenantId tenantId, ActorId actor)
    {
        var suites = new List<SystemSuiteAggregate>();

        var coreResult = SystemSuiteAggregate.Create(
            tenantId,
            Code.Create("UMS"),
            Name.Create("Sistema de Gestión de Usuarios"),
            Description.Create("Funcionalidad principal del UMS"),
            actor);

        if (coreResult.IsSuccess)
        {
            var suite = coreResult.Value;

            // Register standard actions
            suite.RegisterAction(ActionCode.Create("VIEW"), Name.Create("Ver Núcleo Logístico"), actor);
            suite.RegisterAction(ActionCode.Create("MANAGE"), Name.Create("Gestionar Núcleo Logístico"), actor);
            suite.RegisterAction(ActionCode.Create("APPROVE"), Name.Create("Aprobar Operaciones"), actor);
            // La promoción de roles no se gobierna con VIEW/MANAGE: aprobar, certificar y ejecutar
            // son tres actos distintos y separados a propósito —quien aprueba no certifica—, y el
            // producto los guarda con acciones propias. Sin registrarlas aquí, sus nodos quedan sin
            // acción que conceder y esos permisos no los tiene nadie (G-286).
            suite.RegisterAction(ActionCode.Create("CERTIFY"), Name.Create("Certificar Promociones"), actor);
            suite.RegisterAction(ActionCode.Create("EXECUTE"), Name.Create("Ejecutar Promociones"), actor);

            // Register standard domain actions
            suite.RegisterAction(ActionCode.Create("CREATE"), Name.Create("Crear Registro"), actor);
            suite.RegisterAction(ActionCode.Create("READ"), Name.Create("Leer Registro"), actor);
            suite.RegisterAction(ActionCode.Create("UPDATE"), Name.Create("Actualizar Registro"), actor);
            suite.RegisterAction(ActionCode.Create("DELETE"), Name.Create("Eliminar Registro"), actor);
            suite.RegisterAction(ActionCode.Create("SEARCH"), Name.Create("Buscar Registros"), actor);

            // Add app settings
            suite.AddAppSetting(
                ConfigurationKey.Create("SessionTimeout"),
                ConfigurationValue.Create("30"),
                ConfigurationScope.Global,
                actor);
            suite.AddAppSetting(
                ConfigurationKey.Create("MaxRetries"),
                ConfigurationValue.Create("5"),
                ConfigurationScope.Global,
                actor);

            // Add IDM module
            var modIdm = suite.AddModule(Code.Create("IDM"), Name.Create("Identidad y Accesos"), Description.Create("Gestión de empresas, usuarios y delegaciones"), 1, actor, IconoDeModulo("IDM"));
            if (modIdm.IsSuccess)
            {
                var module = suite.Modules.First(m => m.Code.GetValue() == "IDM");
                suite.ActivateModule(module.Props.Id, actor);

                SeedNavMenu(suite, module, 1, "TENANTS", "Empresas", "TENANTS_LIST", "Lista de Empresas",
                    new[] { ("VIEW_TENANTS", "Ver Empresas", "VIEW"), ("MANAGE_TENANTS", "Gestionar Empresas", "MANAGE") }, actor, "/tenants");
                SeedNavMenu(suite, module, 2, "USERS", "Usuarios", "USERS_LIST", "Lista de Usuarios",
                    new[] { ("VIEW_USERS", "Ver Usuarios", "VIEW"), ("MANAGE_USERS", "Gestionar Usuarios", "MANAGE") }, actor, "/users");
                SeedNavMenu(suite, module, 3, "DELEGATIONS", "Delegaciones", "DELEGATIONS_LIST", "Lista de Delegaciones",
                    new[] { ("VIEW_DELEGATIONS", "Ver Delegaciones", "VIEW"), ("MANAGE_DELEGATIONS", "Gestionar Delegaciones", "MANAGE") }, actor, "/delegations");
            }

            // Add AUTH module
            var modAuth = suite.AddModule(Code.Create("AUTH"), Name.Create("Autorización"), Description.Create("Perfiles, plantillas y sistemas"), 2, actor, IconoDeModulo("AUTH"));
            if (modAuth.IsSuccess)
            {
                var module = suite.Modules.First(m => m.Code.GetValue() == "AUTH");
                suite.ActivateModule(module.Props.Id, actor);

                SeedNavMenu(suite, module, 1, "SYSTEM_SUITES", "Sistemas", "SUITES_LIST", "Lista de Sistemas",
                    new[] { ("VIEW_SUITES", "Ver Sistemas", "VIEW"), ("MANAGE_SUITES", "Gestionar Sistemas", "MANAGE") }, actor, "/system-suites");
                SeedNavMenu(suite, module, 2, "PERMISSION_TEMPLATES", "Plantillas de Permisos", "TEMPLATES_LIST", "Lista de Plantillas",
                    new[] { ("VIEW_TEMPLATES", "Ver Plantillas", "VIEW"), ("MANAGE_TEMPLATES", "Gestionar Plantillas", "MANAGE") }, actor, "/permission-templates");
                SeedNavMenu(suite, module, 3, "PROFILES", "Perfiles", "PROFILES_LIST", "Lista de Perfiles",
                    new[] { ("VIEW_PROFILES", "Ver Perfiles", "VIEW"), ("MANAGE_PROFILES", "Gestionar Perfiles", "MANAGE") }, actor, "/profiles");
                // Los roles FALTABAN, y el catálogo sembrado se quedaba corto frente a lo que el
                // producto guarda: `manage_roles` y `view_roles` protegen manejadores reales, así que
                // un administrador sembrado no podía crear ni listar roles en cuanto el aspecto
                // gobernó de verdad — sin que ninguna pantalla lo explicara (G-286).
                SeedNavMenu(suite, module, 4, "ROLES", "Roles", "ROLES_LIST", "Lista de Roles",
                    new[] { ("VIEW_ROLES", "Ver Roles", "VIEW"), ("MANAGE_ROLES", "Gestionar Roles", "MANAGE") }, actor, "/roles");
            }

            // Módulo de GOBIERNO DE IDENTIDAD (IGA) — promoción de roles y aprobaciones.
            //
            // No estaba en el catálogo, y sus manejadores sí están guardados: cinco permisos de
            // promoción —incluidas las acciones propias APPROVE, CERTIFY y EXECUTE, que no son
            // VIEW/MANAGE— más los cuatro de aprobaciones y sus reglas. Sembrar el catálogo sin ellos
            // dejaba doce de los treinta permisos del producto sin nadie que pudiera ejercerlos.
            var modIga = suite.AddModule(Code.Create("IGA"), Name.Create("Gobierno de Identidad"), Description.Create("Promoción de roles, aprobaciones y auditoría"), 4, actor, IconoDeModulo("IGA"));
            if (modIga.IsSuccess)
            {
                var module = suite.Modules.First(m => m.Code.GetValue() == "IGA");
                suite.ActivateModule(module.Props.Id, actor);

                SeedNavMenu(suite, module, 1, "ROLE_PROMOTIONS", "Promoción de Roles", "PROMOTIONS_LIST", "Solicitudes de Promoción",
                    new[]
                    {
                        ("VIEW_ROLE_PROMOTIONS", "Ver Promociones", "VIEW"),
                        ("MANAGE_ROLE_PROMOTIONS", "Gestionar Promociones", "MANAGE"),
                        ("APPROVE_ROLE_PROMOTIONS", "Aprobar Promociones", "APPROVE"),
                        ("CERTIFY_ROLE_PROMOTIONS", "Certificar Promociones", "CERTIFY"),
                        ("EXECUTE_ROLE_PROMOTIONS", "Ejecutar Promociones", "EXECUTE"),
                    }, actor, "/role-promotions");
                SeedNavMenu(suite, module, 2, "APPROVALS", "Aprobaciones", "APPROVALS_LIST", "Solicitudes de Aprobación",
                    new[] { ("VIEW_APPROVALS", "Ver Aprobaciones", "VIEW"), ("MANAGE_APPROVALS", "Gestionar Aprobaciones", "MANAGE") }, actor, "/approval-requests");
                SeedNavMenu(suite, module, 3, "APPROVAL_RULES", "Reglas de Aprobación", "APPROVAL_RULES_LIST", "Lista de Reglas",
                    new[] { ("VIEW_APPROVAL_RULES", "Ver Reglas", "VIEW"), ("MANAGE_APPROVAL_RULES", "Gestionar Reglas", "MANAGE") }, actor, "/approval-rules");
                SeedNavMenu(suite, module, 4, "AUDIT", "Auditoría", "AUDIT_LIST", "Registros de Auditoría",
                    new[] { ("VIEW_AUDIT", "Ver Auditoría", "VIEW") }, actor, "/audit-records");
            }

            // Add SYS module
            var modSys = suite.AddModule(Code.Create("SYS"), Name.Create("Configuración del Sistema"), Description.Create("Propiedades globales, ajustes y banderas"), 3, actor, IconoDeModulo("SYS"));
            if (modSys.IsSuccess)
            {
                var module = suite.Modules.First(m => m.Code.GetValue() == "SYS");
                suite.ActivateModule(module.Props.Id, actor);

                SeedNavMenu(suite, module, 1, "FEATURE_FLAGS", "Banderas de Función", "FLAGS_LIST", "Lista de Banderas",
                    new[] { ("VIEW_FLAGS", "Ver Banderas", "VIEW"), ("MANAGE_FLAGS", "Gestionar Banderas", "MANAGE") }, actor, "/feature-flags");
                SeedNavMenu(suite, module, 2, "APP_CONFIG", "Configuraciones", "CONFIG_LIST", "Lista de Configuraciones",
                    new[] { ("VIEW_CONFIG", "Ver Configuraciones", "VIEW"), ("MANAGE_CONFIG", "Gestionar Configuraciones", "MANAGE") }, actor, "/app-configurations");
                SeedNavMenu(suite, module, 3, "PARAM_CATALOG", "Catálogo de Parámetros", "PARAM_LIST", "Lista de Parámetros",
                    new[] { ("VIEW_PARAMS", "Ver Parámetros", "VIEW"), ("MANAGE_PARAMS", "Gestionar Parámetros", "MANAGE") }, actor, "/parameter-catalog");
            }

            // Add domain resources linked to modules
            var idmMod = suite.Modules.First(m => m.Code.GetValue() == "IDM");
            var authMod = suite.Modules.First(m => m.Code.GetValue() == "AUTH");
            var sysMod = suite.Modules.First(m => m.Code.GetValue() == "SYS");

            suite.AddDomainResource(idmMod.GetId(), null, DomainResourceType.Aggregate, Code.Create("TENANT"), Name.Create("Agregado de Empresa"), Description.Create("Raíz del agregado de empresa"), actor);
            suite.AddDomainResource(idmMod.GetId(), null, DomainResourceType.Aggregate, Code.Create("USER"), Name.Create("Agregado de Usuario"), Description.Create("Raíz del agregado de usuario"), actor);
            suite.AddDomainResource(idmMod.GetId(), null, DomainResourceType.Aggregate, Code.Create("DELEGATION"), Name.Create("Agregado de Delegación"), Description.Create("Raíz del agregado de delegación"), actor);

            suite.AddDomainResource(authMod.GetId(), null, DomainResourceType.Aggregate, Code.Create("SYSTEM_SUITE"), Name.Create("Agregado de Sistema"), Description.Create("Raíz del agregado de sistema"), actor);
            suite.AddDomainResource(authMod.GetId(), null, DomainResourceType.Aggregate, Code.Create("PERMISSION_TEMPLATE"), Name.Create("Agregado de Plantilla"), Description.Create("Raíz del agregado de plantilla"), actor);
            suite.AddDomainResource(authMod.GetId(), null, DomainResourceType.Aggregate, Code.Create("PROFILE"), Name.Create("Agregado de Perfil"), Description.Create("Raíz del agregado de perfil"), actor);

            suite.AddDomainResource(sysMod.GetId(), null, DomainResourceType.Aggregate, Code.Create("FEATURE_FLAG"), Name.Create("Agregado de Bandera de Función"), Description.Create("Raíz del agregado de bandera de función"), actor);
            suite.AddDomainResource(sysMod.GetId(), null, DomainResourceType.Aggregate, Code.Create("APP_CONFIG"), Name.Create("Agregado de Configuración"), Description.Create("Raíz del agregado de configuración"), actor);
            suite.AddDomainResource(sysMod.GetId(), null, DomainResourceType.Aggregate, Code.Create("PARAMETER"), Name.Create("Agregado de Parámetro"), Description.Create("Raíz del agregado de parámetro"), actor);

            suites.Add(suite);
        }

        var wmsResult = SystemSuiteAggregate.Create(
            tenantId,
            Code.Create("WMS"),
            Name.Create("Almacén"),
            Description.Create("Gestión de inventario de almacén"),
            actor);

        if (wmsResult.IsSuccess)
        {
            var suite = wmsResult.Value;

            suite.RegisterAction(ActionCode.Create("INVENTORY_VIEW"), Name.Create("Ver Inventario"), actor);
            suite.RegisterAction(ActionCode.Create("INVENTORY_EDIT"), Name.Create("Editar Inventario"), actor);
            suite.RegisterAction(ActionCode.Create("GENERATE_REPORT"), Name.Create("Generar Reporte"), actor);
            suite.RegisterAction(ActionCode.Create("EXPORT_DATA"), Name.Create("Exportar Datos"), actor);
            suite.RegisterAction(ActionCode.Create("IMPORT_DATA"), Name.Create("Importar Datos"), actor);

            suite.AddAppSetting(
                ConfigurationKey.Create("AllowNegativeStock"),
                ConfigurationValue.Create("false"),
                ConfigurationScope.Global,
                actor);

            var modInv = suite.AddModule(Code.Create("INV"), Name.Create("Inventario"), Description.Create("Gestión y niveles de inventario"), 1, actor, IconoDeModulo("INV"));
            if (modInv.IsSuccess)
            {
                var module = suite.Modules.First(m => m.Code.GetValue() == "INV");
                suite.ActivateModule(module.Props.Id, actor);

                // Menú 1: niveles de stock
                SeedNavMenu(suite, module, 1, "STOCK", "Administración de Stock", "LEVELS", "Niveles en Tiempo Real",
                    new[] { ("VIEW_STOCK", "Ver Niveles de Stock", "INVENTORY_VIEW"), ("ADJUST_STOCK", "Ajustar Conteos de Inventario", "INVENTORY_EDIT") }, actor);

                // Menú 2: operaciones
                SeedNavMenu(suite, module, 2, "OPS", "Operaciones de Almacén", "TRANSFERS", "Transferencias de Almacén",
                    new[] { ("INITIATE_TRANSFER", "Iniciar Transferencia de Stock", "INVENTORY_EDIT"), ("APPROVE_TRANSFER", "Aprobar Transferencia de Ubicación", "INVENTORY_EDIT") }, actor);
            }

            // GAP-7: Add Reports module to WMS
            var modReports = suite.AddModule(Code.Create("REPORTS"), Name.Create("Reportes"), Description.Create("Reportes y analítica de almacén"), 2, actor, IconoDeModulo("REPORTS"));
            if (modReports.IsSuccess)
            {
                var module = suite.Modules.First(m => m.Code.GetValue() == "REPORTS");
                suite.ActivateModule(module.Props.Id, actor);

                // Menú 1: reportes de inventario (dos submenús — se usa el helper de nodos)
                var invReportsMenu = AddNode(suite, module, (MenuNodeEntity?)null, NodeKind.Menu, "INV_REPORTS", "Reportes de Inventario", 1, actor);

                var stockSummary = AddNode(suite, module, invReportsMenu, NodeKind.SubMenu, "STOCK_SUMMARY", "Resumen de Stock", 1, actor);
                var viewStockReport = AddNode(suite, module, stockSummary, NodeKind.Option, "VIEW_STOCK_REPORT", "Ver Reporte de Stock", 1, actor);
                suite.LinkModuleNodeAction(module.Props.Id, viewStockReport.GetId(), ActionCode.Create("GENERATE_REPORT"), actor);
                var exportStock = AddNode(suite, module, stockSummary, NodeKind.Option, "EXPORT_STOCK", "Exportar Datos de Stock", 2, actor);
                suite.LinkModuleNodeAction(module.Props.Id, exportStock.GetId(), ActionCode.Create("EXPORT_DATA"), actor);

                var movement = AddNode(suite, module, invReportsMenu, NodeKind.SubMenu, "MOVEMENT_REPORTS", "Reportes de Movimiento", 2, actor);
                var viewMovement = AddNode(suite, module, movement, NodeKind.Option, "VIEW_MOVEMENT", "Ver Reporte de Movimiento", 1, actor);
                suite.LinkModuleNodeAction(module.Props.Id, viewMovement.GetId(), ActionCode.Create("GENERATE_REPORT"), actor);

                // Menú 2: import/export
                var ioMenu = AddNode(suite, module, (MenuNodeEntity?)null, NodeKind.Menu, "IO", "Importar / Exportar", 2, actor);
                var import = AddNode(suite, module, ioMenu, NodeKind.SubMenu, "IMPORT", "Importación de Datos", 1, actor);
                var runImport = AddNode(suite, module, import, NodeKind.Option, "RUN_IMPORT", "Ejecutar Importación de Datos", 1, actor);
                suite.LinkModuleNodeAction(module.Props.Id, runImport.GetId(), ActionCode.Create("IMPORT_DATA"), actor);
            }

            // Add domain resources for WMS
            var invMod = suite.Modules.First(m => m.Code.GetValue() == "INV");
            suite.AddDomainResource(invMod.GetId(), null, DomainResourceType.Aggregate, Code.Create("INVENTORY_WMS"), Name.Create("Agregado de Inventario de Almacén"), Description.Create("Gestión de inventario de almacén"), actor);
            suite.AddDomainResource(invMod.GetId(), null, DomainResourceType.Entity, Code.Create("STOCK_MOVEMENT"), Name.Create("Entidad de Movimiento de Stock"), Description.Create("Seguimiento de movimientos de stock"), actor);
            suite.AddDomainResource(invMod.GetId(), null, DomainResourceType.Entity, Code.Create("TRANSFER_ORDER"), Name.Create("Entidad de Orden de Transferencia"), Description.Create("Órdenes de transferencia de almacén"), actor);

            suites.Add(suite);
        }

        return suites;
    }

    private static IReadOnlyList<PermissionTemplateAggregate> BuildGenericPermissionTemplates(TenantId tenantId, IReadOnlyList<SystemSuiteAggregate> suites, IReadOnlyList<RoleAggregate> roles, ActorId actor)
    {
        var templates = new List<PermissionTemplateAggregate>();
        if (suites.Count == 0 || roles.Count == 0) return templates;

        var adminRole = roles.FirstOrDefault(r => r.Code.GetValue() == "ADMIN");
        var supervisorRole = roles.FirstOrDefault(r => r.Code.GetValue() == "SUPERVISOR");
        var auditorRole = roles.FirstOrDefault(r => r.Code.GetValue() == "AUDITOR");
        var readonlyRole = roles.FirstOrDefault(r => r.Code.GetValue() == "READONLY");
        var dataEntryRole = roles.FirstOrDefault(r => r.Code.GetValue() == "DATA_ENTRY");
        var operatorRole = roles.FirstOrDefault(r => r.Code.GetValue() == "OPERATOR");
        var inspectorRole = roles.FirstOrDefault(r => r.Code.GetValue() == "INSPECTOR");
        var managerRole = roles.FirstOrDefault(r => r.Code.GetValue() == "WMS_MANAGER");
        var dispatcherRole = roles.FirstOrDefault(r => r.Code.GetValue() == "DISPATCHER");
        var reporterRole = roles.FirstOrDefault(r => r.Code.GetValue() == "REPORTER");

        var coreSuite = suites[0];
        var wmsSuite = suites.Count > 1 ? suites[1] : null;

        // Helper to find a domain resource by code from the core suite
        // Helper to find a domain resource by code from the core suite
        var tenantResource = coreSuite.DomainResources.FirstOrDefault(r => r.Code.GetValue() == "TENANT");
        var userResource = coreSuite.DomainResources.FirstOrDefault(r => r.Code.GetValue() == "USER");

        // ── 1. ADMIN V2 — Published, full suite access ────────────────────
        if (adminRole != null)
        {
            var adminV2 = PermissionTemplateAggregate.Create(tenantId, adminRole.GetId(), coreSuite.GetId(), actor).Value;
            adminV2.Props.Version = TemplateVersion.Create(2, 0, 0);
            // Navigation: full suite
            adminV2.AddItem(ExclusiveArcTarget.SystemSuite, coreSuite.GetId(), ActionId.Create(), true, false, actor);
            foreach (var mod in coreSuite.Modules)
            {
                adminV2.AddItem(ExclusiveArcTarget.Module, mod.Props.Id, ActionId.Create(), true, false, actor);
                foreach (var root in mod.Nodes)
                {
                    AddNodeNavigation(adminV2, root, coreSuite, actor);
                }
            }
            // Domain resources: full access. El grafo (fail-closed, G-039) evalúa el
            // efecto por (resourceId, actionId) sobre TODA acción de la suite, así que
            // se concede cada acción real; un único ActionId aleatorio no concedería nada.
            foreach (var res in coreSuite.DomainResources)
            {
                var targetType = res.Type == DomainResourceType.Aggregate ? ExclusiveArcTarget.Aggregate : ExclusiveArcTarget.Entity;
                foreach (var action in coreSuite.Actions)
                {
                    adminV2.AddItem(targetType, res.Id, action.GetId(), true, false, actor);
                }
            }
            adminV2.Publish(actor);
            templates.Add(adminV2);
        }

        // ── 2. SUPERVISOR — Published, module-level access ────────────────
        if (supervisorRole != null)
        {
            var supervisorTpl = PermissionTemplateAggregate.Create(tenantId, supervisorRole.GetId(), coreSuite.GetId(), actor).Value;
            var idmMod = coreSuite.Modules.FirstOrDefault(m => m.Code.GetValue() == "IDM");
            if (idmMod != null)
            {
                // Navigation: IDM module
                supervisorTpl.AddItem(ExclusiveArcTarget.Module, idmMod.Props.Id, ActionId.Create(), true, false, actor);
            }
            // Domain resources: read tenants, users
            if (tenantResource != null)
                supervisorTpl.AddItem(ExclusiveArcTarget.Aggregate, tenantResource.Id, ActionId.Create(), true, false, actor);
            if (userResource != null)
                supervisorTpl.AddItem(ExclusiveArcTarget.Aggregate, userResource.Id, ActionId.Create(), true, false, actor);

            supervisorTpl.Publish(actor);
            templates.Add(supervisorTpl);
        }

        // ── 3. AUDITOR — Published, read-only on users ───────────────
        if (auditorRole != null)
        {
            var auditorTpl = PermissionTemplateAggregate.Create(tenantId, auditorRole.GetId(), coreSuite.GetId(), actor).Value;
            var idmMod = coreSuite.Modules.FirstOrDefault(m => m.Code.GetValue() == "IDM");
            if (idmMod != null)
            {
                var usersMenu = FindNodeByCode(idmMod, "USERS");
                if (usersMenu != null)
                {
                    var listSubMenu = FindNodeByCode(idmMod, "USERS_LIST");
                    if (listSubMenu != null)
                    {
                        var viewUsersOpt = FindNodeByCode(idmMod, "VIEW_USERS");
                        // Navigation: view users options only
                        auditorTpl.AddItem(ExclusiveArcTarget.Submodule, usersMenu.GetId(), ActionId.Create(), true, false, actor);
                        auditorTpl.AddItem(ExclusiveArcTarget.Option, listSubMenu.GetId(), ActionId.Create(), true, false, actor);
                        if (viewUsersOpt != null) AddOptionActionItems(auditorTpl, viewUsersOpt, coreSuite, actor);
                    }
                }
            }
            // Domain resources: read-only users aggregate
            if (userResource != null)
                auditorTpl.AddItem(ExclusiveArcTarget.Aggregate, userResource.Id, ActionId.Create(), true, false, actor);
            auditorTpl.Publish(actor);
            templates.Add(auditorTpl);
        }

        // ── 4. READONLY — Published, view-only on users list ──────────────
        if (readonlyRole != null)
        {
            var readonlyTpl = PermissionTemplateAggregate.Create(tenantId, readonlyRole.GetId(), coreSuite.GetId(), actor).Value;
            var idmMod = coreSuite.Modules.FirstOrDefault(m => m.Code.GetValue() == "IDM");
            if (idmMod != null)
            {
                var usersMenu = FindNodeByCode(idmMod, "USERS");
                if (usersMenu != null)
                {
                    var listSubMenu = FindNodeByCode(idmMod, "USERS_LIST");
                    if (listSubMenu != null)
                    {
                        var viewUsersOpt = FindNodeByCode(idmMod, "VIEW_USERS");
                        if (viewUsersOpt != null) AddOptionActionItems(readonlyTpl, viewUsersOpt, coreSuite, actor);
                    }
                }
            }
            if (userResource != null)
                readonlyTpl.AddItem(ExclusiveArcTarget.Aggregate, userResource.Id, ActionId.Create(), true, false, actor);
            readonlyTpl.Publish(actor);
            templates.Add(readonlyTpl);
        }

        // ── 5. DATA_ENTRY — Published, create/update on users ─────────────
        if (dataEntryRole != null)
        {
            var dataEntryTpl = PermissionTemplateAggregate.Create(tenantId, dataEntryRole.GetId(), coreSuite.GetId(), actor).Value;
            var idmMod = coreSuite.Modules.FirstOrDefault(m => m.Code.GetValue() == "IDM");
            if (idmMod != null)
            {
                var usersMenu = FindNodeByCode(idmMod, "USERS");
                if (usersMenu != null)
                {
                    var listSubMenu = FindNodeByCode(idmMod, "USERS_LIST");
                    if (listSubMenu != null)
                    {
                        var editUsersOpt = FindNodeByCode(idmMod, "MANAGE_USERS");
                        dataEntryTpl.AddItem(ExclusiveArcTarget.Option, listSubMenu.GetId(), ActionId.Create(), true, false, actor);
                        if (editUsersOpt != null) AddOptionActionItems(dataEntryTpl, editUsersOpt, coreSuite, actor);
                    }
                }
            }
            if (userResource != null)
                dataEntryTpl.AddItem(ExclusiveArcTarget.Aggregate, userResource.Id, ActionId.Create(), true, false, actor);
            dataEntryTpl.Publish(actor);
            templates.Add(dataEntryTpl);
        }

        // ── 6. OPERATOR — Published, stock view + deny adjust ─────────────
        if (operatorRole != null && wmsSuite != null)
        {
            var operatorTpl = PermissionTemplateAggregate.Create(tenantId, operatorRole.GetId(), wmsSuite.GetId(), actor).Value;
            var invMod = wmsSuite.Modules.First(m => m.Code.GetValue() == "INV");
            var levelsSubMenu = FindNodeByCode(invMod, "LEVELS")!;
            var viewStockOpt = FindNodeByCode(invMod, "VIEW_STOCK")!;
            var adjustStockOpt = FindNodeByCode(invMod, "ADJUST_STOCK")!;
            var invWms = wmsSuite.DomainResources.First(x => x.Code.GetValue() == "INVENTORY_WMS");

            operatorTpl.AddItem(ExclusiveArcTarget.Module, invMod.Props.Id, ActionId.Create(), true, false, actor);
            operatorTpl.AddItem(ExclusiveArcTarget.Option, levelsSubMenu.GetId(), ActionId.Create(), true, false, actor);
            AddOptionActionItems(operatorTpl, viewStockOpt, wmsSuite, actor);
            AddOptionActionItems(operatorTpl, adjustStockOpt, wmsSuite, actor, isAllowed: false, isDenied: true);
            operatorTpl.AddItem(ExclusiveArcTarget.Aggregate, invWms.Id, ActionId.Create(), true, false, actor);
            operatorTpl.Publish(actor);
            templates.Add(operatorTpl);
        }

        // ── 7. INSPECTOR — Published, quality control with deny rules ─────
        if (inspectorRole != null && wmsSuite != null)
        {
            var inspectorTpl = PermissionTemplateAggregate.Create(tenantId, inspectorRole.GetId(), wmsSuite.GetId(), actor).Value;
            var invMod = wmsSuite.Modules.First(m => m.Code.GetValue() == "INV");
            var levelsSubMenu = FindNodeByCode(invMod, "LEVELS")!;
            var viewStockOpt = FindNodeByCode(invMod, "VIEW_STOCK")!;

            inspectorTpl.AddItem(ExclusiveArcTarget.Module, invMod.Props.Id, ActionId.Create(), true, false, actor);
            inspectorTpl.AddItem(ExclusiveArcTarget.Option, levelsSubMenu.GetId(), ActionId.Create(), true, false, actor);
            AddOptionActionItems(inspectorTpl, viewStockOpt, wmsSuite, actor);
            inspectorTpl.Publish(actor);
            templates.Add(inspectorTpl);
        }

        // ── 8. WMS_MANAGER — Published, full WMS access ───────────────────
        if (managerRole != null && wmsSuite != null)
        {
            var managerTpl = PermissionTemplateAggregate.Create(tenantId, managerRole.GetId(), wmsSuite.GetId(), actor).Value;
            managerTpl.AddItem(ExclusiveArcTarget.SystemSuite, wmsSuite.GetId(), ActionId.Create(), true, false, actor);
            foreach (var mod in wmsSuite.Modules)
            {
                managerTpl.AddItem(ExclusiveArcTarget.Module, mod.Props.Id, ActionId.Create(), true, false, actor);
                foreach (var root in mod.Nodes)
                {
                    AddNodeNavigation(managerTpl, root, wmsSuite, actor);
                }
            }
            managerTpl.Publish(actor);
            templates.Add(managerTpl);
        }

        // ── 9. DISPATCHER — Published, transfers only ─────────────────────
        if (dispatcherRole != null && wmsSuite != null)
        {
            var dispatcherTpl = PermissionTemplateAggregate.Create(tenantId, dispatcherRole.GetId(), wmsSuite.GetId(), actor).Value;
            var invMod = wmsSuite.Modules.First(m => m.Code.GetValue() == "INV");
            var opsMenu = FindNodeByCode(invMod, "OPS")!;
            var transfersSubMenu = FindNodeByCode(invMod, "TRANSFERS")!;
            var initiateOpt = FindNodeByCode(invMod, "INITIATE_TRANSFER")!;
            var approveOpt = FindNodeByCode(invMod, "APPROVE_TRANSFER")!;
            var transferOrder = wmsSuite.DomainResources.First(x => x.Code.GetValue() == "TRANSFER_ORDER");

            dispatcherTpl.AddItem(ExclusiveArcTarget.Submodule, opsMenu.GetId(), ActionId.Create(), true, false, actor);
            dispatcherTpl.AddItem(ExclusiveArcTarget.Option, transfersSubMenu.GetId(), ActionId.Create(), true, false, actor);
            AddOptionActionItems(dispatcherTpl, initiateOpt, wmsSuite, actor);
            AddOptionActionItems(dispatcherTpl, approveOpt, wmsSuite, actor);
            dispatcherTpl.AddItem(ExclusiveArcTarget.Entity, transferOrder.Id, ActionId.Create(), true, false, actor);
            dispatcherTpl.Publish(actor);
            templates.Add(dispatcherTpl);
        }

        // ── 10. REPORTER — Published, reports module only ─────────────────
        if (reporterRole != null && wmsSuite != null)
        {
            var reporterTpl = PermissionTemplateAggregate.Create(tenantId, reporterRole.GetId(), wmsSuite.GetId(), actor).Value;
            var reportsMod = wmsSuite.Modules.First(m => m.Code.GetValue() == "REPORTS");
            var stockSummarySubMenu = FindNodeByCode(reportsMod, "STOCK_SUMMARY")!;
            var viewReportOpt = FindNodeByCode(reportsMod, "VIEW_STOCK_REPORT")!;
            var exportOpt = FindNodeByCode(reportsMod, "EXPORT_STOCK")!;

            reporterTpl.AddItem(ExclusiveArcTarget.Module, reportsMod.Props.Id, ActionId.Create(), true, false, actor);
            reporterTpl.AddItem(ExclusiveArcTarget.Option, stockSummarySubMenu.GetId(), ActionId.Create(), true, false, actor);
            AddOptionActionItems(reporterTpl, viewReportOpt, wmsSuite, actor);
            AddOptionActionItems(reporterTpl, exportOpt, wmsSuite, actor);
            reporterTpl.Publish(actor);
            templates.Add(reporterTpl);
        }

        return templates;
    }

    private static IReadOnlyList<ProfileAggregate> BuildGenericProfiles(TenantId tenantId, IReadOnlyList<RoleAggregate> roles, IReadOnlyList<PermissionTemplateAggregate> templates, ActorId actor)
    {
        var profiles = new List<ProfileAggregate>();

        var baseBytes = tenantId.GetValue().ToByteArray();
        Guid UserGuid(byte idx)
        {
            var b = (byte[])baseBytes.Clone();
            b[0] = idx;
            return new Guid(b);
        }

        var adminRole = roles.FirstOrDefault(r => r.Code.GetValue() == "ADMIN");
        var supervisorRole = roles.FirstOrDefault(r => r.Code.GetValue() == "SUPERVISOR");
        var auditorRole = roles.FirstOrDefault(r => r.Code.GetValue() == "AUDITOR");
        var readonlyRole = roles.FirstOrDefault(r => r.Code.GetValue() == "READONLY");
        var dataEntryRole = roles.FirstOrDefault(r => r.Code.GetValue() == "DATA_ENTRY");
        var operatorRole = roles.FirstOrDefault(r => r.Code.GetValue() == "OPERATOR");
        var inspectorRole = roles.FirstOrDefault(r => r.Code.GetValue() == "INSPECTOR");
        var managerRole = roles.FirstOrDefault(r => r.Code.GetValue() == "WMS_MANAGER");
        var dispatcherRole = roles.FirstOrDefault(r => r.Code.GetValue() == "DISPATCHER");
        var reporterRole = roles.FirstOrDefault(r => r.Code.GetValue() == "REPORTER");

        var adminTpl = templates.FirstOrDefault(t => t.RoleId.Equals(adminRole?.GetId()) && t.Props.Version.GetValue() == "2.0.0" && t.Status == TemplateStatus.Published);
        var supervisorTpl = templates.FirstOrDefault(t => t.RoleId.Equals(supervisorRole?.GetId()) && t.Status == TemplateStatus.Published);
        var auditorTpl = templates.FirstOrDefault(t => t.RoleId.Equals(auditorRole?.GetId()) && t.Status == TemplateStatus.Published);
        var readonlyTpl = templates.FirstOrDefault(t => t.RoleId.Equals(readonlyRole?.GetId()) && t.Status == TemplateStatus.Published);
        var dataEntryTpl = templates.FirstOrDefault(t => t.RoleId.Equals(dataEntryRole?.GetId()) && t.Status == TemplateStatus.Published);
        var operatorTpl = templates.FirstOrDefault(t => t.RoleId.Equals(operatorRole?.GetId()) && t.Status == TemplateStatus.Published);
        var inspectorTpl = templates.FirstOrDefault(t => t.RoleId.Equals(inspectorRole?.GetId()) && t.Status == TemplateStatus.Published);
        var managerTpl = templates.FirstOrDefault(t => t.RoleId.Equals(managerRole?.GetId()) && t.Status == TemplateStatus.Published);
        var dispatcherTpl = templates.FirstOrDefault(t => t.RoleId.Equals(dispatcherRole?.GetId()) && t.Status == TemplateStatus.Published);
        var reporterTpl = templates.FirstOrDefault(t => t.RoleId.Equals(reporterRole?.GetId()) && t.Status == TemplateStatus.Published);

        void AddProfile(Guid userId, RoleAggregate? role, PermissionTemplateAggregate? tpl)
        {
            if (role == null) return;
            var p = ProfileAggregate.Create(tenantId, UserId.Load(userId), role.GetId(), null, actor);
            if (p.IsSuccess)
            {
                if (tpl != null) p.Value.AssignTemplate(tpl, actor);
                profiles.Add(p.Value);
            }
        }

        // SuperAdmin (INTERNAL_ADMIN tenant only) — fixed UserId 22222222-...
        var internalAdminTenantId = Guid.Parse(CoreDevDataSeeder.InternalAdminTenantId);
        if (tenantId.GetValue() == internalAdminTenantId)
            AddProfile(Guid.Parse(CoreDevDataSeeder.SuperAdminUserId), adminRole, adminTpl);

        // 1. Admin — full suite access (ADMIN V2)
        AddProfile(UserGuid(1), adminRole, adminTpl);

        // 2. Supervisor — module-level access to Security
        AddProfile(UserGuid(6), supervisorRole, supervisorTpl);

        // 3. Auditor — read-only on audit logs
        AddProfile(UserGuid(11), auditorRole, auditorTpl);

        // 4. Read Only — view users list only
        AddProfile(UserGuid(12), readonlyRole, readonlyTpl);

        // 5. Data Entry — create/update users
        AddProfile(UserGuid(2), dataEntryRole, dataEntryTpl);

        // 6. Warehouse Operator — stock view, deny adjust
        AddProfile(UserGuid(7), operatorRole, operatorTpl);

        // 7. Quality Inspector — stock view only
        AddProfile(UserGuid(9), inspectorRole, inspectorTpl);

        // 8. Warehouse Manager — full WMS access
        AddProfile(UserGuid(10), managerRole, managerTpl);

        // 9. Dispatch Coordinator — transfers only
        AddProfile(UserGuid(8), dispatcherRole, dispatcherTpl);

        // 10. Report Analyst — reports module only
        AddProfile(UserGuid(3), reporterRole, reporterTpl);

        return profiles;
    }

    private static async Task EnsureInternalAdminProfileAsync(
        IProfileRepository profileRepository,
        IRoleRepository roleRepository,
        IPermissionTemplateRepository templateRepository,
        CancellationToken cancellationToken)
    {
        var expectedProfileId = Guid.Parse(CoreDevDataSeeder.GlobalAdminProfileId);
        var expectedUserId = Guid.Parse(CoreDevDataSeeder.SuperAdminUserId);
        var internalAdminTenantId = Guid.Parse(CoreDevDataSeeder.InternalAdminTenantId);
        var tenantId = TenantId.Load(internalAdminTenantId);
        var actor = ActorId.Create(CoreDevDataSeeder.SystemActorId);

        var profile = await profileRepository.GetByIdAsync(expectedProfileId, cancellationToken);
        if (profile is null)
        {
            // Create missing profile with admin role and template
            var roles = await roleRepository.GetByTenantIdAsync(internalAdminTenantId, cancellationToken);
            var adminRole = roles.FirstOrDefault(r => r.Code.GetValue() == "ADMIN");
            var templates = await templateRepository.GetByTenantIdAsync(internalAdminTenantId, cancellationToken);
            var adminTpl = templates.FirstOrDefault(t => t.RoleId.Equals(adminRole?.GetId()) && t.Status == TemplateStatus.Published && t.Props.Version.GetValue() == "2.0.0");
            var newProfileResult = ProfileAggregate.Create(tenantId, UserId.Load(expectedUserId), adminRole?.GetId(), null, actor);
            if (newProfileResult.IsSuccess)
            {
                var newProfile = newProfileResult.Value;
                if (adminTpl != null)
                {
                    newProfile.AssignTemplate(adminTpl, actor);
                }
                await profileRepository.AddAsync(newProfile, cancellationToken);
                await profileRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);
            }
            return;
        }

        if (profile.UserId.GetValue() == expectedUserId)
        {
            return;
        }

        var propsField = typeof(ProfileAggregate).GetField("_props", PrivateInstanceFlags);
        var props = propsField?.GetValue(profile) as ProfileProps;
        var userIdProperty = typeof(ProfileProps).GetProperty("UserId", PrivateInstanceFlags);
        userIdProperty?.SetValue(props, UserId.Load(expectedUserId));

        await profileRepository.UpdateAsync(profile, cancellationToken);
        await profileRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);
    }

    private static async Task EnsureDomainResourcesAsync(
        IReadOnlyList<SystemSuiteAggregate> existingSuites,
        ActorId actor,
        ISystemSuiteRepository repository,
        CancellationToken cancellationToken)
    {
        foreach (var suite in existingSuites)
        {
            var existingResources = suite.DomainResources;
            var codesToAdd = GetDomainResourceCodesForSuite(suite);

            foreach (var codeToAdd in codesToAdd)
            {
                if (!existingResources.Any(dr => dr.Code.GetValue() == codeToAdd.Code))
                {
                    suite.AddDomainResource(
                        null,
                        null,
                        codeToAdd.Type,
                        Code.Create(codeToAdd.Code),
                        Name.Create(codeToAdd.Name),
                        Description.Create(codeToAdd.Description),
                        actor);
                }
            }

            await repository.UpdateAsync(suite, cancellationToken);
        }

        await repository.UnitOfWork.SaveEntitiesAsync(cancellationToken);
    }

    private static IReadOnlyList<(string Code, string Name, string Description, DomainResourceType Type)> GetDomainResourceCodesForSuite(SystemSuiteAggregate suite)
    {
        var suiteCode = suite.Code.GetValue();

        return suiteCode switch
        {
            "UMS" => new (string, string, string, DomainResourceType)[]
            {
                ("USERS", "Users Aggregate", "User Management", DomainResourceType.Aggregate),
                ("INVENTORY", "Inventory Aggregate", "Inventory Management", DomainResourceType.Aggregate),
                ("AUDIT_LOG", "Audit Log Entity", "Audit Logs", DomainResourceType.Entity),
                ("STOCK_LEVEL", "Stock Level Entity", "Stock Levels", DomainResourceType.Entity),
            },
            "WMS" => new (string, string, string, DomainResourceType)[]
            {
                ("INVENTORY_WMS", "WMS Inventory Aggregate", "Warehouse Inventory Management", DomainResourceType.Aggregate),
                ("STOCK_MOVEMENT", "Stock Movement Entity", "Stock Movement Tracking", DomainResourceType.Entity),
                ("TRANSFER_ORDER", "Transfer Order Entity", "Warehouse Transfer Orders", DomainResourceType.Entity),
            },
            _ => Array.Empty<(string, string, string, DomainResourceType)>(),
        };
    }
}

#pragma warning restore S1144
