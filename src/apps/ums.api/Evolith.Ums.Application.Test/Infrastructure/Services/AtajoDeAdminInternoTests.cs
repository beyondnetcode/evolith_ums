using System.Collections.Generic;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Moq;
using Evolith.Ums.Infrastructure.Services;
using Xunit;

namespace Evolith.Ums.Application.Test.Infrastructure.Services;

/// <summary>
/// Fija el comportamiento del atajo de Admin Root, que es la última pieza que decide si UMS aplica
/// o no su propio modelo de permisos.
///
/// Importa que esté cubierto por prueba y no solo por comentario: el claim `is_internal_admin` sale
/// del INQUILINO —`isInternalAdmin = ctx.Tenant.IsManagementOwner`—, así que alcanza a TODA cuenta
/// de EVOLITH. Mientras el atajo esté encendido, etiquetar 182 handlers no cambia el comportamiento
/// ni un poco, y eso es exactamente el tipo de cosa que se olvida entre un despliegue y otro.
/// </summary>
public sealed class AtajoDeAdminInternoTests
{
    private static UserContext Contexto(bool adminInterno, string? atajo, params string[] scopes)
    {
        var claims = new List<Claim>();
        if (adminInterno) claims.Add(new Claim("is_internal_admin", "true"));
        foreach (var s in scopes) claims.Add(new Claim("scope", s));

        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "prueba")) };
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.SetupGet(a => a.HttpContext).Returns(http);

        var ajustes = new Dictionary<string, string?>();
        if (atajo is not null) ajustes[UserContext.ClaveAtajo] = atajo;
        var config = new ConfigurationBuilder().AddInMemoryCollection(ajustes).Build();

        return new UserContext(accessor.Object, config);
    }

    [Fact]
    public void Encendido_por_omision_concede_sin_mirar_scopes()
    {
        // El comportamiento HISTÓRICO, y se conserva a propósito: apagarlo sin haber dado perfil
        // real a las cuentas operativas dejaría la instancia sin nadie capaz de administrarla.
        Contexto(adminInterno: true, atajo: null).HasPermission("manage_users:manage").Should().BeTrue();
    }

    [Fact]
    public void Apagado_deja_de_conceder_aunque_sea_admin_interno()
    {
        // El interruptor final de la migración. Sin esto, los 182 handlers etiquetados no aplican
        // nada para ninguna cuenta de EVOLITH.
        Contexto(adminInterno: true, atajo: "false").HasPermission("manage_users:manage").Should().BeFalse();
    }

    [Fact]
    public void Apagado_sigue_concediendo_lo_que_el_scope_otorga()
    {
        // Apagar el atajo no es cerrar la puerta: es exigir que el permiso venga de una concesión.
        Contexto(adminInterno: true, atajo: "false", "manage_users.manage")
            .HasPermission("manage_users:manage").Should().BeTrue();
    }

    [Fact]
    public void Apagado_no_concede_un_scope_distinto()
    {
        Contexto(adminInterno: true, atajo: "false", "view_users.view")
            .HasPermission("manage_users:manage").Should().BeFalse();
    }
}
