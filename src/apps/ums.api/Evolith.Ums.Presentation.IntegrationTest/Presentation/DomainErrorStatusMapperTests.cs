using Evolith.Ums.Domain.Kernel;
using Evolith.Ums.Presentation.Extensions;

namespace Evolith.Ums.Presentation.IntegrationTest.Presentation;

/// <summary>
/// G-104 (patrón latente de G-100). Fija, sobre la función pura <see cref="DomainErrorStatusMapper"/>,
/// la decisión caso-por-caso de la semántica HTTP de los códigos de dominio <c>*_not_found</c>:
///
/// <list type="bullet">
///   <item><description><b>404</b> cuando el código direcciona un recurso por id/clave que no existe
///   (recurso apuntado por la URI/segmento de ruta ausente).</description></item>
///   <item><description><b>400</b> cuando el código NO direcciona el recurso de la URI, sino que valida
///   una referencia colgante en el CUERPO de un POST de creación (integridad referencial del payload:
///   un <c>FallbackToId</c>/<c>parentResourceId</c> inexistente). El recurso objetivo del POST sí
///   existe/se crea; forzar 404 sería incorrecto.</description></item>
/// </list>
///
/// El mapeador es la ÚNICA pieza que cambia el mapeo código→status; probarlo directamente es la
/// evidencia más fiel y determinista del cambio (SD-05), sin depender del arranque del host.
/// </summary>
public sealed class DomainErrorStatusMapperTests
{
    // ── 404: «no encontrado por id/clave» ────────────────────────────────────
    // Los tres primeros son los que G-104 cablea; el resto son regresión (G-100 y previos).
    [Theory]
    [InlineData("configuration.criteria_not_found")]      // DomainErrors.Configuration.CriteriaNotFound (G-104)
    [InlineData("user_account.mfa_enrollment_not_found")] // DomainErrors.UserAccount.MfaEnrollmentNotFound (G-104)
    [InlineData("tenant_parameter.not_found")]            // DomainErrors.TenantParameter.NotFound (G-104)
    [InlineData("iga.role_promotion_request_not_found")]  // DomainErrors.IGA.RolePromotionRequestNotFound (G-100)
    [InlineData("common.not_found")]                      // DomainErrors.Common.NotFound
    [InlineData("system_suite.configuration_key_not_found")] // DomainErrors.SystemSuite.ConfigurationKeyNotFound
    public void Map_NotFoundByKeyDomainCodes_ShouldReturn404(string errorCode)
    {
        var (status, _) = DomainErrorStatusMapper.Map(errorCode);

        status.Should().Be(404, "un código de dominio que direcciona un recurso ausente por id/clave debe resolver 404 Not Found");
    }

    // ── 400: referencia colgante en el cuerpo (NO 404) ───────────────────────
    // Excluidos deliberadamente del brazo 404: validan integridad referencial del payload de un POST.
    [Theory]
    [InlineData("configuration.idp_fallback_not_found")]     // DomainErrors.Configuration.IdpFallbackNotFound
    [InlineData("authorization.parent_resource_not_found")]  // DomainErrors.Authorization.ParentResourceNotFound
    public void Map_DanglingReferenceInBodyDomainCodes_ShouldReturn400_NotFound(string errorCode)
    {
        var (status, _) = DomainErrorStatusMapper.Map(errorCode);

        status.Should().Be(400, "una referencia colgante en el cuerpo de un POST de creación es validación del payload (400), no un recurso de URI ausente (404)");
    }

    // ── 409: guardias de cascada y estados terminales del borrado lógico ─────
    // Solo existe borrado lógico. Intentar eliminar algo con referencias VIVAS —o volver a eliminar
    // lo ya eliminado— es un conflicto con el estado actual del recurso, no una validación de payload.
    // El TenantParameter no tiene endpoint DELETE hoy; el mapeo se fija aquí para que el 409 sea el
    // contrato desde el primer día en que se cablee, y no un 400 heredado del brazo por defecto.
    [Theory]
    [InlineData("TEMPLATE_HAS_ACTIVE_PROFILES")]          // DomainErrors.Authorization.TemplateHasActiveProfiles
    [InlineData("authorization.template_already_deleted")] // DomainErrors.Authorization.TemplateAlreadyDeleted
    [InlineData("TENANT_PARAMETER_HAS_ACTIVE_BINDING")]    // DomainErrors.TenantParameter.HasActiveBinding
    [InlineData("tenant_parameter.already_deleted")]       // DomainErrors.TenantParameter.AlreadyDeleted
    public void Map_SoftDeleteGuardCodes_ShouldReturn409(string errorCode)
    {
        var (status, _) = DomainErrorStatusMapper.Map(errorCode);

        status.Should().Be(409, "una referencia viva que impide el borrado lógico es un conflicto con el estado del recurso");
    }

    // ── Anclas explícitas: los símbolos coinciden con las constantes de dominio ─
    // Evita que un renombrado del literal en DomainErrors deje huérfano el mapeo probado arriba.
    [Fact]
    public void SoftDeleteGuardConstants_ShouldMatchTheStringsUnderTest()
    {
        DomainErrors.Authorization.TemplateHasActiveProfiles.Should().Be("TEMPLATE_HAS_ACTIVE_PROFILES");
        DomainErrors.Authorization.TemplateAlreadyDeleted.Should().Be("authorization.template_already_deleted");
        DomainErrors.TenantParameter.HasActiveBinding.Should().Be("TENANT_PARAMETER_HAS_ACTIVE_BINDING");
        DomainErrors.TenantParameter.AlreadyDeleted.Should().Be("tenant_parameter.already_deleted");
    }

    [Fact]
    public void DomainErrorConstants_ShouldMatchTheStringsUnderTest()
    {
        DomainErrors.Configuration.CriteriaNotFound.Should().Be("configuration.criteria_not_found");
        DomainErrors.UserAccount.MfaEnrollmentNotFound.Should().Be("user_account.mfa_enrollment_not_found");
        DomainErrors.TenantParameter.NotFound.Should().Be("tenant_parameter.not_found");
        DomainErrors.Configuration.IdpFallbackNotFound.Should().Be("configuration.idp_fallback_not_found");
        DomainErrors.Authorization.ParentResourceNotFound.Should().Be("authorization.parent_resource_not_found");
    }
}
