namespace Evolith.Ums.Domain.Test.Authorization.SystemSuite;

using Evolith.Ums.Domain.Authorization.SystemSuite;
using Evolith.Ums.Domain.Authorization.SystemSuite.MenuNode;
using Xunit;

/// <summary>
/// La presentación de la navegación —icono del módulo (G-182) e icono/ruta de los nodos (D-028)—
/// tiene que sobrevivir al alta. Estas pruebas existen porque no sobrevivía: el módulo aceptaba una
/// presentación para sus nodos raíz y la descartaba en silencio al crearlos, de modo que TODOS los
/// menús de primer nivel —que son justo los que pinta la barra— quedaban sin icono y sin ruta.
/// </summary>
public class PresentacionDeNavegacionTests
{
    private static readonly ActorId Actor = ActorId.Create("test-user");

    private static (SystemSuite Suite, IdValueObject ModuleId) SuiteConModulo(string? icono = null)
    {
        var suite = SystemSuite.Create(
            TenantId.Load(Guid.NewGuid().ToString()),
            Code.Create("WMS"),
            Name.Create("Almacén"),
            Description.Create("Sistema de almacén"),
            Actor).Value;

        suite.AddModule(Code.Create("INV"), Name.Create("Inventario"), Description.Create("Inventario"), 1, Actor, icono);
        var moduleId = suite.Modules.First().Props.Id;
        suite.ActivateModule(moduleId, Actor);
        return (suite, moduleId);
    }

    [Fact]
    public void El_modulo_conserva_el_icono_con_el_que_se_dio_de_alta()
    {
        var (suite, _) = SuiteConModulo("package");

        Assert.Equal("package", suite.Modules.First().Icon);
    }

    [Fact]
    public void Un_modulo_sin_icono_no_inventa_ninguno()
    {
        // Nulo y no una cadena por defecto: el cliente debe poder distinguir «sin configurar»
        // de «configurado a algo», y elegir su respaldo.
        var (suite, _) = SuiteConModulo();

        Assert.Null(suite.Modules.First().Icon);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void El_icono_en_blanco_equivale_a_no_tener_icono(string icono)
    {
        var (suite, _) = SuiteConModulo(icono);

        Assert.Null(suite.Modules.First().Icon);
    }

    [Fact]
    public void El_icono_del_modulo_se_puede_cambiar_y_borrar()
    {
        var (suite, moduleId) = SuiteConModulo("package");

        suite.SetModuleIcon(moduleId, " truck ", Actor);
        Assert.Equal("truck", suite.Modules.First().Icon);

        suite.SetModuleIcon(moduleId, null, Actor);
        Assert.Null(suite.Modules.First().Icon);
    }

    [Fact]
    public void El_nodo_raiz_conserva_su_icono_y_su_ruta()
    {
        // La regresión que estas pruebas cierran: el nodo raíz recibía la presentación y la perdía,
        // así que el menú que ve el usuario se quedaba sin icono y sin destino.
        var (suite, moduleId) = SuiteConModulo("package");

        suite.AddModuleRootNode(
            moduleId,
            NodeKind.Menu,
            Code.Create("STOCK"),
            Name.Create("Stock"),
            Description.Create("Stock"),
            1,
            Actor,
            presentation: MenuNodePresentation.Create("package", "/stock"));

        var nodo = suite.Modules.First().Nodes.First();
        Assert.Equal("package", nodo.Props.Presentation.Icon);
        Assert.Equal("/stock", nodo.Props.Presentation.Route);
    }

    [Fact]
    public void El_nodo_hijo_conserva_su_icono_y_su_ruta()
    {
        var (suite, moduleId) = SuiteConModulo();

        suite.AddModuleRootNode(moduleId, NodeKind.Menu, Code.Create("STOCK"), Name.Create("Stock"),
            Description.Create("Stock"), 1, Actor);
        var menuId = suite.Modules.First().Nodes.First().GetId();

        suite.AddModuleChildNode(moduleId, menuId, NodeKind.Option, Code.Create("STOCK_VIEW"),
            Name.Create("Ver Stock"), Description.Create("Ver Stock"), 1, Actor,
            presentation: MenuNodePresentation.Create(null, "/stock/ver"));

        var hijo = suite.Modules.First().Nodes.First().Children.First();
        Assert.Null(hijo.Props.Presentation.Icon);
        Assert.Equal("/stock/ver", hijo.Props.Presentation.Route);
    }
}
