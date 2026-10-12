namespace Evolith.Ums.Application.Test.Infrastructure.Notifications;

using Evolith.Ums.Infrastructure.Services.Notifications;
using Xunit;

/// <summary>
/// G-270: la configuración del relay se juzga al arrancar, no en el primer envío.
///
/// <para>Cada caso de aquí es una forma en que un despliegue puede quedar sin correo sin que nadie
/// lo note hasta que alguien pide un restablecimiento. El validador los convierte en un fallo de
/// arranque, que bajo IIS se ve mientras se despliega y no meses después.</para>
/// </summary>
public class NotificationOptionsValidatorTests
{
    private static readonly NotificationOptionsValidator Validador = new();

    private static NotificationOptions Smtp(Action<SmtpOptionsBuilder>? ajuste = null)
    {
        var builder = new SmtpOptionsBuilder();
        ajuste?.Invoke(builder);
        return new NotificationOptions { Provider = NotificationProvider.Smtp, Smtp = builder.Construir() };
    }

    private sealed class SmtpOptionsBuilder
    {
        public string Host { get; set; } = "smtp.sendgrid.net";
        public int Port { get; set; } = 25;
        public string User { get; set; } = "apikey";
        public string Password { get; set; } = "no-es-una-clave-real";
        public string FromAddress { get; set; } = "sistemas@evolith.example";
        public int TimeoutSeconds { get; set; } = 30;

        public SmtpOptions Construir() => new()
        {
            Host = Host,
            Port = Port,
            User = User,
            Password = Password,
            FromAddress = FromAddress,
            TimeoutSeconds = TimeoutSeconds,
        };
    }

    [Fact]
    public void Simulado_NoExigeNada()
    {
        // Es el transporte de desarrollo y de la suite: pedirle relay dejaría sin arrancar a
        // quien solo quiere correr las pruebas.
        var resultado = Validador.Validate(null, new NotificationOptions());

        Assert.True(resultado.Succeeded);
    }

    [Fact]
    public void Smtp_ConTodoDeclarado_Aprueba()
    {
        var resultado = Validador.Validate(null, Smtp());

        Assert.True(resultado.Succeeded);
    }

    [Fact]
    public void Smtp_SinHost_Falla()
    {
        var resultado = Validador.Validate(null, Smtp(b => b.Host = ""));

        Assert.True(resultado.Failed);
        Assert.Contains(resultado.Failures, f => f.Contains("Host", StringComparison.Ordinal));
    }

    [Fact]
    public void Smtp_ConUsuarioYSinContrasena_Falla()
    {
        // El modo real de fallar: el despliegue trae el appsettings versionado —que la deja
        // vacía— y nadie puso el valor vivo en el servidor.
        var resultado = Validador.Validate(null, Smtp(b => b.Password = ""));

        Assert.True(resultado.Failed);
        Assert.Contains(resultado.Failures, f => f.Contains("Password", StringComparison.Ordinal));
    }

    [Fact]
    public void Smtp_ConContrasenaYSinUsuario_Falla()
    {
        var resultado = Validador.Validate(null, Smtp(b => b.User = ""));

        Assert.True(resultado.Failed);
        Assert.Contains(resultado.Failures, f => f.Contains("User", StringComparison.Ordinal));
    }

    [Fact]
    public void Smtp_SinRemitente_Falla()
    {
        var resultado = Validador.Validate(null, Smtp(b => b.FromAddress = ""));

        Assert.True(resultado.Failed);
        Assert.Contains(resultado.Failures, f => f.Contains("FromAddress", StringComparison.Ordinal));
    }

    [Fact]
    public void Smtp_ConRemitenteQueNoEsUnaDireccion_Falla()
    {
        var resultado = Validador.Validate(null, Smtp(b => b.FromAddress = "sistemas-arroba-evolith"));

        Assert.True(resultado.Failed);
        Assert.Contains(resultado.Failures, f => f.Contains("FromAddress", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(70000)]
    public void Smtp_ConPuertoFueraDeRango_Falla(int puerto)
    {
        var resultado = Validador.Validate(null, Smtp(b => b.Port = puerto));

        Assert.True(resultado.Failed);
        Assert.Contains(resultado.Failures, f => f.Contains("Port", StringComparison.Ordinal));
    }

    [Fact]
    public void Smtp_ConTiempoDeEsperaNoPositivo_Falla()
    {
        var resultado = Validador.Validate(null, Smtp(b => b.TimeoutSeconds = 0));

        Assert.True(resultado.Failed);
        Assert.Contains(resultado.Failures, f => f.Contains("TimeoutSeconds", StringComparison.Ordinal));
    }

    [Fact]
    public void Smtp_ReportaTodosLosFallosDeUnaVez()
    {
        // Un arranque que falla debe decir TODO lo que falta: descubrirlo de uno en uno cuesta
        // un despliegue por cada campo.
        var resultado = Validador.Validate(null, Smtp(b =>
        {
            b.Host = "";
            b.FromAddress = "";
            b.Password = "";
        }));

        Assert.True(resultado.Failed);
        Assert.Equal(3, resultado.Failures!.Count());
    }
}
