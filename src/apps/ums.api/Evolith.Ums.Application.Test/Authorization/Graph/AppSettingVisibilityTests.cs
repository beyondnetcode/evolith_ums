namespace Evolith.Ums.Application.Test.Authorization.Graph;

using Xunit;
using FluentAssertions;
using Evolith.Ums.Domain.Authorization.SystemSuite.AppSetting;
using Evolith.Ums.Domain.Configuration;
using Evolith.Ums.Domain.Kernel;
using SystemSuiteAggregate = Evolith.Ums.Domain.Authorization.SystemSuite.SystemSuite;

/// <summary>
/// La marca de exposición de los ajustes del sistema (G-178).
///
/// `AppSetting` es una bolsa clave/valor sin tipo: junto al color de la marca puede haber una
/// cadena de conexión o el secreto de una integración. Estas pruebas fijan que la exposición se
/// decide ajuste a ajuste y que el default no publica — si alguien invierte ese default, aquí
/// se entera antes de que un secreto salga en el cable.
/// </summary>
public class AppSettingVisibilityTests
{
    private static SystemSuiteAggregate Suite()
        => SystemSuiteAggregate.Create(
            TenantId.Load(Guid.NewGuid()),
            Code.Create("SDLC"),
            Name.Create("Tablero"),
            Description.Create("Tablero de gobierno"),
            ActorId.Create("system")).Value;

    [Fact]
    public void Un_ajuste_no_es_visible_salvo_que_se_diga()
    {
        var ajuste = AppSetting.Create(
            ConfigurationKey.Create("OPS_CONNECTION_STRING"),
            ConfigurationValue.Create("Host=db;Password=secreto"),
            ConfigurationScope.Global).Value;

        ajuste.IsClientVisible.Should().BeFalse(
            "el default debe ser no publicar: la bolsa contiene también ajustes operativos");
    }

    [Fact]
    public void La_marca_viaja_al_agregado()
    {
        var suite = Suite();
        var actor = ActorId.Create("system");

        suite.AddAppSetting(ConfigurationKey.Create("BRAND_LOGO_URL"), ConfigurationValue.Create("/logo.svg"),
            ConfigurationScope.Global, actor, isClientVisible: true);
        suite.AddAppSetting(ConfigurationKey.Create("OPS_INTERVAL"), ConfigurationValue.Create("30"),
            ConfigurationScope.Global, actor);

        suite.AppSettings.Single(a => a.Key.GetValue() == "BRAND_LOGO_URL").IsClientVisible.Should().BeTrue();
        suite.AppSettings.Single(a => a.Key.GetValue() == "OPS_INTERVAL").IsClientVisible.Should().BeFalse();
    }

    [Fact]
    public void Cambiar_el_valor_no_cambia_la_visibilidad()
    {
        var suite = Suite();
        var actor = ActorId.Create("system");

        suite.AddAppSetting(ConfigurationKey.Create("OPS_INTERVAL"), ConfigurationValue.Create("30"),
            ConfigurationScope.Global, actor);
        suite.UpdateAppSetting(ConfigurationKey.Create("OPS_INTERVAL"), ConfigurationValue.Create("60"), actor);

        // Editar un valor no es decidir publicarlo: si esto se rompiera, cualquier edición
        // rutinaria podría sacar al cable un ajuste operativo.
        suite.AppSettings.Single(a => a.Key.GetValue() == "OPS_INTERVAL").IsClientVisible.Should().BeFalse();
    }
}
