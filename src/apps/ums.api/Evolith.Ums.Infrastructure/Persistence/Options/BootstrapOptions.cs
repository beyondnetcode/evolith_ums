namespace Evolith.Ums.Infrastructure.Persistence.Options;

/// <summary>
/// Con qué credencial nace el administrador inicial.
/// </summary>
public enum BootstrapCredentialMode
{
    /// <summary>
    /// La cuenta nace SIN contraseña y se registra un secreto de un solo uso que el operador ya
    /// conoce —lo generó él y lo inyectó por el gestor de secretos—. Solo se persiste su SHA-256.
    /// Para canjearlo se usa el flujo de restablecimiento que ya existe, y hasta entonces la cuenta
    /// no puede iniciar sesión.
    ///
    /// <para>Es el modo de PRODUCCIÓN, y el valor por omisión. Cierra por construcción el problema
    /// de la contraseña temporal que nunca caduca: no hay contraseña temporal en ningún momento, y
    /// el «cambio forzado en el primer ingreso» no necesita ningún campo nuevo en el modelo porque
    /// no existe credencial que cambiar hasta que su titular la elige.</para>
    /// </summary>
    OneTimeToken = 0,

    /// <summary>
    /// La cuenta nace con contraseña, leída de una variable de entorno. Conveniente para DESARROLLO
    /// y UAT, donde interesa poder entrar sin canjear nada.
    ///
    /// <para>No usar en producción: la contraseña queda viva indefinidamente y sobrevive en el
    /// entorno del proceso. El arranque lo rechaza explícitamente cuando el entorno es Production.</para>
    /// </summary>
    Password = 1,
}

/// <summary>
/// Opciones del arranque en frío de la plataforma (<c>Persistence:Bootstrap</c>).
///
/// <para>A diferencia de <c>Persistence:SeedDevData</c>, esto SÍ puede correr en Production: no
/// crea datos de demostración, sino el mínimo imprescindible para que alguien pueda entrar por
/// primera vez y provisionar el resto por API. Está apagado por omisión: encenderlo es una
/// decisión del despliegue, no un valor heredado.</para>
///
/// <para>NINGÚN secreto vive en esta clase. Se declaran NOMBRES de variables de entorno, nunca
/// valores: un secreto en <c>appsettings</c> acaba commiteado, y un secreto commiteado acaba
/// siendo el de todos los entornos (G-203).</para>
/// </summary>
public sealed class BootstrapOptions
{
    /// <summary>
    /// Cuando es <c>true</c>, se crea el inquilino y la cuenta de arranque si la plataforma nunca
    /// se inicializó. Idempotente.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>Código del inquilino propietario de gestión.</summary>
    public string TenantCode { get; init; } = "EVOLITH";

    /// <summary>Razón social del inquilino.</summary>
    public string TenantName { get; init; } = "Evolith UMS";

    /// <summary>Identificador fiscal o referencia de la empresa.</summary>
    public string CompanyReference { get; init; } = "RUC-20100412447";

    /// <summary>
    /// Variable de entorno con el correo del administrador inicial. Tiene prioridad sobre
    /// <see cref="AdminEmail"/>: quién administra un despliegue es dato del despliegue, no del
    /// repositorio.
    /// </summary>
    public string AdminEmailEnvVar { get; init; } = "UMS_BOOTSTRAP_EMAIL";

    /// <summary>
    /// Correo de respaldo si la variable de <see cref="AdminEmailEnvVar"/> no está definida.
    /// Un correo no es un secreto, así que un valor por omisión aquí es admisible; una contraseña
    /// no lo sería.
    /// </summary>
    public string AdminEmail { get; init; } = "admin@evolith.example";

    /// <summary>Con qué credencial nace la cuenta. Ver <see cref="BootstrapCredentialMode"/>.</summary>
    public BootstrapCredentialMode CredentialMode { get; init; } = BootstrapCredentialMode.OneTimeToken;

    /// <summary>
    /// Variable de entorno con el secreto de un solo uso (modo <see cref="BootstrapCredentialMode.OneTimeToken"/>).
    ///
    /// <para>Lo genera el OPERADOR, no UMS. Es deliberado: si lo generase UMS habría que
    /// entregárselo a alguien, y todos los canales disponibles lo filtran — la respuesta HTTP no
    /// existe en el arranque, el log es el sitio equivocado para un secreto, y el adaptador de
    /// notificación registrado vuelca cuerpo y asunto al log (G-197). Generándolo el operador, el
    /// secreto ya está donde debe estar y UMS solo guarda su SHA-256.</para>
    /// </summary>
    public string InitialSecretEnvVar { get; init; } = "UMS_BOOTSTRAP_TOKEN";

    /// <summary>
    /// Longitud mínima exigida al secreto de un solo uso. 32 caracteres es lo que produce
    /// <c>PasswordResetToken.Generate()</c> como mínimo práctico; por debajo de eso el secreto deja
    /// de ser inadivinable y el modo pierde su razón de ser.
    /// </summary>
    public int InitialSecretMinLength { get; init; } = 32;

    /// <summary>
    /// Vida del secreto de arranque, en minutos. Más larga que los 15 minutos del restablecimiento
    /// ordinario porque aquí no hay un humano esperando un correo: hay un despliegue que puede
    /// tardar en completarse. Sigue siendo finita a propósito — si vence sin canjearse no queda
    /// ninguna puerta abierta, y hay que reejecutar el arranque con un secreto nuevo.
    /// </summary>
    public int InitialSecretLifetimeMinutes { get; init; } = 60;

    /// <summary>
    /// Variable de entorno con la contraseña inicial (modo <see cref="BootstrapCredentialMode.Password"/>).
    /// Si el modo es <c>Password</c> y la variable no está definida, el arranque falla en vez de
    /// inventar un valor.
    /// </summary>
    public string AdminPasswordEnvVar { get; init; } = "UMS_BOOTSTRAP_PASSWORD";

    /// <summary>
    /// Exigir que NO exista ninguna cuenta en la plataforma para arrancar en frío. Es la guarda
    /// del patrón First-Run y debe quedarse en <c>true</c>.
    ///
    /// <para>Existe como bandera únicamente para poder desactivarla en una recuperación
    /// deliberada —una plataforma cuyas cuentas se perdieron— y esa decisión tiene que ser
    /// explícita y quedar escrita en la configuración del despliegue, no ser algo que ocurra sin
    /// que nadie lo pida.</para>
    /// </summary>
    public bool RequireEmptyUserTable { get; init; } = true;
}
