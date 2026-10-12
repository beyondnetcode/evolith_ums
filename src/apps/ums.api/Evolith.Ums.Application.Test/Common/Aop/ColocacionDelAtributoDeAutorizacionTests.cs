using System;
using System.Linq;
using System.Reflection;
using Evolith.Ums.Application.Common.Aop;
using Evolith.Ums.Application.Configuration.IdpConfiguration.Commands;
using Evolith.Ums.Application.Identity.Tenant.Queries;
using FluentAssertions;
using Xunit;

namespace Evolith.Ums.Application.Test.Common.Aop;

/// <summary>
/// El atributo `[AuthorizationAspect]` puesto en la CLASE no protege nada.
///
/// <para>`PointCut.CanApply` --quien decide si el aspecto intercepta-- solo mira los atributos del
/// MÉTODO objetivo. Con el atributo en la clase, el aspecto no se ejecuta nunca: el endpoint queda
/// sin control de permisos y todo responde 2xx, que es exactamente lo que hace que nadie lo note.
/// Ocurrió con `GetAllTenantsQueryHandler` y `CreateIdpConfigurationCommandHandler`, los dos únicos
/// manejadores marcados de todo el repositorio, y ninguna prueba lo detectaba porque las de
/// integración autentican con `is_internal_admin=true`, que concede cualquier permiso.</para>
///
/// <para>Estas pruebas son de COLOCACIÓN, no de comportamiento: no comprueban que el aspecto
/// deniegue --de eso se ocupa <see cref="AuthorizationAspectTests"/>-- sino que llegue a
/// ejecutarse. Un control que no corre no falla: concede.</para>
/// </summary>
public class ColocacionDelAtributoDeAutorizacionTests
{
    [Fact]
    public void NingunManejador_LlevaElAtributoSoloEnLaClase()
    {
        var ensamblado = typeof(GetAllTenantsQueryHandler).Assembly;

        var infractores = ensamblado.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t => t.GetCustomAttribute<AuthorizationAspectAttribute>() is not null)
            .Where(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                         .All(m => m.GetCustomAttribute<AuthorizationAspectAttribute>() is null))
            .Select(t => t.FullName)
            .ToList();

        infractores.Should().BeEmpty(
            "un `[AuthorizationAspect]` en la clase sin equivalente en ningún método no llega a "
            + "ejecutarse: el pointcut solo mira el método, y el endpoint queda sin control de acceso");
    }

    [Theory]
    [InlineData(typeof(GetAllTenantsQueryHandler), "view_tenants", "view")]
    [InlineData(typeof(CreateIdpConfigurationCommandHandler), "manage_config", "manage")]
    public void LosManejadoresProtegidos_DeclaranSuPermisoEnElMetodo(Type manejador, string recurso, string accion)
    {
        var handle = manejador.GetMethod("Handle");
        handle.Should().NotBeNull($"{manejador.Name} tiene que exponer `Handle`");

        var atributo = handle!.GetCustomAttribute<AuthorizationAspectAttribute>();
        atributo.Should().NotBeNull(
            $"{manejador.Name} declara estar protegido; si el atributo no está en `Handle`, no lo está");

        // Los códigos importan tanto como la colocación: `UserContext.HasPermission` normaliza
        // `recurso:accion` a `recurso.accion` y lo busca entre los claims `scope`. El código
        // anterior de este manejador pedía un permiso que NO EXISTE en el catálogo de la suite UMS
        // --dieciocho scopes con la forma `view_X.view` / `manage_X.manage`--, así que encender el
        // gate sin corregirlo habría denegado a todo el mundo salvo al administrador interno.
        atributo!.ResourceCode.Should().Be(recurso);
        atributo.ActionCode.Should().Be(accion);
    }
}
