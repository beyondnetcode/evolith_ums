namespace Evolith.Ums.Infrastructure.Persistence.Options;

public sealed class PersistenceOptions
{
    public const string SectionName = "Persistence";

    public PersistenceProvider Provider { get; init; } = PersistenceProvider.PostgreSql;

    /// <summary>
    /// <c>true</c> cuando el proveedor es una base relacional real (PostgreSQL o SQL Server),
    /// por oposición a <see cref="PersistenceProvider.InMemory"/>.
    ///
    /// <para>POR QUÉ EXISTE. Casi todas las guardas del registro de dependencias comparaban
    /// contra <c>PostgreSql</c>, pero lo que preguntaban en realidad era «¿hay una base
    /// relacional detrás?»: si registrar DbContext, interceptores, unidad de trabajo y los
    /// repositorios EF, o caer a los almacenes en memoria. Al reintroducir SQL Server esa
    /// comparación pasó a ser incorrecta —habría dejado UAT sin repositorios—, así que la
    /// pregunta se hace explícita aquí en vez de repetir la disyunción en cada punto.</para>
    ///
    /// <para>Las ramas que SÍ dependen del motor concreto —<c>UseNpgsql</c> frente a
    /// <c>UseSqlServer</c>, el interceptor de RowVersion, el proveedor de bloqueos— siguen
    /// preguntando por <see cref="Provider"/>.</para>
    /// </summary>
    public bool IsRelational =>
        Provider is PersistenceProvider.PostgreSql or PersistenceProvider.SqlServer;

    public AggregateStoreMode AggregateStoreMode { get; init; } = AggregateStoreMode.InMemory;

    public bool UsePostgreSqlIdentityStores { get; init; } = false;

    public bool UsePostgreSqlAuthorizationStores { get; init; } = false;

    public bool UsePostgreSqlConfigurationStores { get; init; } = false;

    public bool UsePostgreSqlApprovalsStores { get; init; } = false;

    public bool UsePostgreSqlIgaStores { get; init; } = false;

    /// <summary>
    /// G-127: cuando es <c>true</c>, siembra el dataset determinista FS-25 (datos de referencia
    /// —roles, plantillas de permiso, suites, catálogos— + datos demo —tenants, usuarios,
    /// aprobaciones—) al arrancar. Está DESACOPLADO del entorno: habilita el stage UAT (usuarios
    /// reales con datos visibles al ingresar), no solo Development. Nunca se siembra en Production
    /// (guarda de defensa en profundidad en <c>InitializeUmsPlatformAsync</c>). La siembra es
    /// idempotente (guarda por tenant ancla + seeders idempotentes), segura ante reinicios.
    /// </summary>
    public bool SeedDevData { get; init; } = true;

    /// <summary>
    /// Fuerza la siembra aunque el inquilino ancla ya exista.
    ///
    /// <para>POR QUÉ HACE FALTA. La guarda de idempotencia de <c>SeedAllAsync</c> omite <b>toda</b>
    /// la siembra en cuanto detecta el inquilino ancla, para que un reinicio de pod no re-ejecute
    /// los siete sembradores. El efecto secundario es que el conjunto sembrado queda congelado en
    /// lo que hubiera el primer día: si más tarde se añade un sistema, un rol o una plantilla al
    /// catálogo, ninguna base existente lo recibe jamás. Así se quedó EVOLITH sin la suite UMS.</para>
    ///
    /// <para>Hasta ahora la única salida era un <c>reset</c> que dropea el esquema, lo cual también
    /// borra lo que NO siembra el código —el Tablero SDLC se carga por API— y obliga a rehacerlo.
    /// Esta bandera permite converger sin perder nada.</para>
    ///
    /// <para>Es de un solo uso deliberado: se enciende para un despliegue, se comprueba el
    /// resultado y se apaga. Dejarla encendida devolvería el coste que la guarda evita —los siete
    /// sembradores en cada arranque de cada réplica— y convertiría en rutina algo que conviene
    /// mirar mientras pasa.</para>
    /// </summary>
    public bool ForceReseed { get; init; } = false;

    /// <summary>
    /// Arranque en frío: el mínimo con el que una base recién migrada puede empezar a operarse
    /// —un inquilino propietario de gestión y una cuenta administradora—, sin ningún dato de
    /// demostración. Es lo que permite inicializar QA o Production, donde <see cref="SeedDevData"/>
    /// está apagado y no debe encenderse.
    /// </summary>
    public BootstrapOptions Bootstrap { get; init; } = new();

    public bool EnableOutbox { get; init; } = true;

    public bool InitializePlatformStoreOnStartup { get; init; } = false;

    /// <summary>
    /// Cuando es <c>true</c>, el proceso migra, siembra y <b>termina</b> sin levantar el servidor.
    ///
    /// <para>Existe para sacar la migración del arranque de cada réplica (G-169). Con una sola
    /// réplica no se nota; con dos o más, las dos arrancan a la vez y las dos intentan migrar la
    /// misma base. EF Core toma un bloqueo de aviso, así que no corrompe nada, pero la segunda
    /// espera a la primera y puede agotar su sonda de vivacidad antes de servir una sola
    /// petición.</para>
    ///
    /// <para>Con esto, el chart lo ejecuta UNA vez como Job previo al despliegue y las réplicas
    /// arrancan contra una base ya migrada.</para>
    /// </summary>
    public bool MigrateAndExit { get; init; } = false;
}
