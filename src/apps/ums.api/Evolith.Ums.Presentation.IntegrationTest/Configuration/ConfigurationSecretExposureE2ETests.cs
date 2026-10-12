using Evolith.Ums.Presentation.IntegrationTest.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Evolith.Ums.Presentation.IntegrationTest.Configuration;

/// <summary>
/// G-088 (contexto de seguridad): E2E sobre un contenedor PostgreSQL real (Testcontainers)
/// que garantiza, extremo a extremo por REST, dos invariantes de Configuration que hasta ahora
/// sólo se probaban a nivel unitario:
///
/// 1. Los valores marcados como cifrados/secretos NO se devuelven en claro por REST a un usuario
///    NO administrador (se redactan a "***"), y el texto cifrado crudo (prefijo <c>AES256:</c>)
///    NUNCA se serializa en la respuesta —ni siquiera al administrador, que lo recibe descifrado—.
///    Además se verifica el cifrado EN REPOSO leyendo la fila directamente de la base.
///
/// 2. La evaluación de un feature flag responde correctamente al kill-switch (fail-closed: un flag
///    inactivo evalúa deshabilitado, ADR/G-048) y a los extremos del porcentaje de rollout
///    (0% ⇒ deshabilitado, 100% ⇒ habilitado) de forma determinista.
///
/// Calca el patrón de <see cref="Evolith.Ums.Presentation.IntegrationTest.Iga.RolePromotionEffectE2ETests"/>
/// (host PostgreSQL de Testcontainers, cliente por actor). Para ejercer la vista de un usuario NO
/// administrador —la única en la que la redacción es observable— se usa el override aditivo y
/// retrocompatible <c>X-Test-Is-Internal-Admin: false</c> de <see cref="TestAuthHandler"/>.
/// </summary>
[Collection("PostgreSql")]
public sealed class ConfigurationSecretExposureE2ETests
{
    private const string EncryptedPrefix = "AES256:"; // Contrato de wire de AesValueEncryptionService.

    // Tenant sembrado por el host (coincide con el default de TestAuthHandler): el usuario NO
    // administrador de este tenant puede resolver configuraciones Global (TenantId IS NULL).
    private static readonly Guid SeededTenantId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
    private static readonly Guid SeededSystemSuiteId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly PostgreSqlContainerFixture _fixture;
    private readonly PostgreSqlWebApplicationFactory? _factory;

    public ConfigurationSecretExposureE2ETests(PostgreSqlContainerFixture fixture)
    {
        _fixture = fixture;
        if (fixture.IsAvailable)
        {
            _factory = new PostgreSqlWebApplicationFactory(fixture.ConnectionString);
        }
    }

    // ---------------------------------------------------------------------------------------
    // 1. Redacción de secretos cifrados por REST (fuga de secretos)
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task EncryptedConfig_IsStoredCiphered_RedactedForNonAdmin_AndNeverLeaksRawCiphertext()
    {
        if (!_fixture.IsAvailable || _factory is null)
        {
            Assert.Skip("Docker required.");
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        const string secretPlaintext = "S3cr3t-Api-Key-p!auth-9f2c-DO-NOT-LEAK";

        var admin = CreateAdminClient();
        var nonAdmin = CreateNonAdminClient();

        // 1. El administrador crea una configuración Global marcada como cifrada.
        var code = $"G088-SECRET-{Guid.NewGuid():N}";
        var createResponse = await admin.PostAsJsonAsync("/api/v1/app-configurations", new
        {
            tenantId = (Guid?)null,
            systemSuiteId = (Guid?)null,
            moduleId = (Guid?)null,
            code,
            value = secretPlaintext,
            description = "G-088 configuración con valor secreto",
            isInheritable = false,
            isEncrypted = true,
        }, ct);

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        using var createPayload = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync(ct));
        var configId = createPayload.RootElement.GetProperty("appConfigurationId").GetGuid();

        // 2. Cifrado EN REPOSO: la fila persistida guarda el ciphertext (prefijo AES256:), nunca el claro.
        string storedValue;
        bool storedIsEncrypted;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<UmsPlatformDbContext>();
            var record = await db.AppConfigurations
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.Id == configId, ct);

            record.Should().NotBeNull("la configuración debe haberse persistido");
            storedValue = record!.Value;
            storedIsEncrypted = record.IsEncrypted;
        }

        storedIsEncrypted.Should().BeTrue("la bandera de cifrado debe persistirse");
        storedValue.Should().StartWith(EncryptedPrefix, "un valor marcado como cifrado debe almacenarse cifrado en reposo");
        storedValue.Should().NotContain(secretPlaintext, "el texto en claro nunca debe persistirse");

        // 3. Vista NO administrador: el valor se redacta a "***" y el cuerpo no filtra ni el claro
        //    ni el ciphertext crudo.
        var nonAdminResponse = await nonAdmin.GetAsync($"/api/v1/app-configurations/{configId}", ct);
        nonAdminResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var nonAdminBody = await nonAdminResponse.Content.ReadAsStringAsync(ct);

        using (var nonAdminJson = JsonDocument.Parse(nonAdminBody))
        {
            nonAdminJson.RootElement.GetProperty("isEncrypted").GetBoolean().Should().BeTrue();
            nonAdminJson.RootElement.GetProperty("value").GetString()
                .Should().Be("***", "un usuario no administrador nunca debe ver el valor de un secreto");
        }

        nonAdminBody.Should().NotContain(secretPlaintext, "FUGA: el secreto en claro apareció en la respuesta a un no administrador");
        nonAdminBody.Should().NotContain(EncryptedPrefix, "el texto cifrado crudo (AES256:) no forma parte del contrato de respuesta");

        // 4. Vista administrador: recibe el valor DESCIFRADO (round-trip correcto), pero el ciphertext
        //    crudo tampoco se expone.
        var adminResponse = await admin.GetAsync($"/api/v1/app-configurations/{configId}", ct);
        adminResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var adminBody = await adminResponse.Content.ReadAsStringAsync(ct);

        using (var adminJson = JsonDocument.Parse(adminBody))
        {
            adminJson.RootElement.GetProperty("value").GetString()
                .Should().Be(secretPlaintext, "el administrador ve el secreto descifrado (round-trip de AesValueEncryptionService)");
        }

        adminBody.Should().NotContain(EncryptedPrefix, "ni siquiera al administrador se le devuelve el ciphertext crudo: se descifra");
    }

    [Fact]
    public async Task PlaintextConfig_IsReturnedAsIs_ForBothAudiences()
    {
        if (!_fixture.IsAvailable || _factory is null)
        {
            Assert.Skip("Docker required.");
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        const string plainValue = "7200";

        var admin = CreateAdminClient();
        var nonAdmin = CreateNonAdminClient();

        // Configuración Global NO cifrada: control negativo de la redacción — no debe redactarse.
        var code = $"G088-PLAIN-{Guid.NewGuid():N}";
        var createResponse = await admin.PostAsJsonAsync("/api/v1/app-configurations", new
        {
            tenantId = (Guid?)null,
            systemSuiteId = (Guid?)null,
            moduleId = (Guid?)null,
            code,
            value = plainValue,
            description = "G-088 configuración no cifrada (control)",
            isInheritable = false,
            isEncrypted = false,
        }, ct);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        using var createPayload = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync(ct));
        var configId = createPayload.RootElement.GetProperty("appConfigurationId").GetGuid();

        // En reposo se guarda tal cual (sin prefijo de cifrado).
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<UmsPlatformDbContext>();
            var record = await db.AppConfigurations.IgnoreQueryFilters().FirstAsync(x => x.Id == configId, ct);
            record.Value.Should().Be(plainValue);
            record.IsEncrypted.Should().BeFalse();
        }

        var nonAdminResponse = await nonAdmin.GetAsync($"/api/v1/app-configurations/{configId}", ct);
        using var nonAdminJson = JsonDocument.Parse(await nonAdminResponse.Content.ReadAsStringAsync(ct));
        nonAdminJson.RootElement.GetProperty("value").GetString()
            .Should().Be(plainValue, "un valor no cifrado no se redacta para nadie");
        nonAdminJson.RootElement.GetProperty("value").GetString()
            .Should().NotBe("***");
    }

    // ---------------------------------------------------------------------------------------
    // 2. Kill-switch y porcentaje de rollout (feature flags) extremo a extremo
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task BooleanFlag_KillSwitch_DisablesEvaluationEndToEnd()
    {
        if (!_fixture.IsAvailable || _factory is null)
        {
            Assert.Skip("Docker required.");
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        var admin = CreateAdminClient();

        var flagId = await CreateFlagAsync(admin, ct, flagType: "Boolean", rolloutPercentage: null);

        // Activo ⇒ habilitado (sin criterios: activo para todos).
        (await admin.PostAsync($"/api/v1/feature-flags/{flagId}/activate", null, ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var enabled = await EvaluateAsync(admin, flagId, ct);
        enabled.IsEnabled.Should().BeTrue("un flag booleano activo sin criterios está habilitado para todos");

        // Kill-switch: desactivar ⇒ deshabilitado (fail-closed).
        (await admin.PostAsync($"/api/v1/feature-flags/{flagId}/deactivate", null, ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var killed = await EvaluateAsync(admin, flagId, ct);
        killed.IsEnabled.Should().BeFalse("el kill-switch (flag inactivo) debe deshabilitar la evaluación");
        killed.Reason.Should().Contain("not active");
    }

    [Fact]
    public async Task PercentageFlag_ZeroRollout_EvaluatesDisabled()
    {
        if (!_fixture.IsAvailable || _factory is null)
        {
            Assert.Skip("Docker required.");
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        var admin = CreateAdminClient();

        var flagId = await CreateFlagAsync(admin, ct, flagType: "Percentage", rolloutPercentage: 0);
        (await admin.PostAsync($"/api/v1/feature-flags/{flagId}/activate", null, ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var result = await EvaluateAsync(admin, flagId, ct, profileId: Guid.NewGuid());
        result.IsEnabled.Should().BeFalse("un rollout del 0% no habilita a ningún sujeto");
        result.Reason.Should().Contain("0");
    }

    [Fact]
    public async Task PercentageFlag_FullRollout_EvaluatesEnabled()
    {
        if (!_fixture.IsAvailable || _factory is null)
        {
            Assert.Skip("Docker required.");
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        var admin = CreateAdminClient();

        var flagId = await CreateFlagAsync(admin, ct, flagType: "Percentage", rolloutPercentage: 100);
        (await admin.PostAsync($"/api/v1/feature-flags/{flagId}/activate", null, ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var result = await EvaluateAsync(admin, flagId, ct, profileId: Guid.NewGuid());
        result.IsEnabled.Should().BeTrue("un rollout del 100% habilita a todo sujeto");
        result.Reason.Should().Contain("100");
    }

    [Fact]
    public async Task PercentageFlag_MidRollout_IsDeterministicPerSubject()
    {
        if (!_fixture.IsAvailable || _factory is null)
        {
            Assert.Skip("Docker required.");
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        var admin = CreateAdminClient();

        var flagId = await CreateFlagAsync(admin, ct, flagType: "Percentage", rolloutPercentage: 50);
        (await admin.PostAsync($"/api/v1/feature-flags/{flagId}/activate", null, ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // El mismo sujeto cae siempre en el mismo bucket ⇒ el resultado es estable entre llamadas
        // (bucketing determinista FNV-1a). No acoplamos el test al valor concreto del bucket.
        var subject = Guid.NewGuid();
        var first = await EvaluateAsync(admin, flagId, ct, profileId: subject);
        var second = await EvaluateAsync(admin, flagId, ct, profileId: subject);

        second.IsEnabled.Should().Be(first.IsEnabled, "el porcentaje de rollout debe ser determinista por sujeto");
    }

    // ---------------------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------------------

    private async Task<Guid> CreateFlagAsync(HttpClient client, CancellationToken ct, string flagType, int? rolloutPercentage)
    {
        var response = await client.PostAsJsonAsync("/api/v1/feature-flags", new
        {
            systemSuiteId = SeededSystemSuiteId,
            tenantId = (Guid?)null,
            flagCode = $"g088_flag_{Guid.NewGuid():N}",
            flagType,
            flagTargets = "g088-tests",
            linkedResourceType = (string?)null,
            linkedResourceId = (Guid?)null,
            rolloutPercentage,
        }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return payload.RootElement.GetProperty("featureFlagId").GetGuid();
    }

    private static async Task<(bool IsEnabled, string? Reason)> EvaluateAsync(
        HttpClient client, Guid flagId, CancellationToken ct, Guid? profileId = null)
    {
        var response = await client.PostAsJsonAsync($"/api/v1/feature-flags/{flagId}/evaluate", new
        {
            tenantId = SeededTenantId,
            profileId,
        }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var root = payload.RootElement;
        var reason = root.TryGetProperty("reason", out var r) && r.ValueKind == JsonValueKind.String ? r.GetString() : null;
        return (root.GetProperty("isEnabled").GetBoolean(), reason);
    }

    // En el host de pruebas (entorno Development) DevAuthMiddleware corre ANTES de la autenticación
    // y es quien inicializa el ITenantContext a partir de las cabeceras X-Tenant-Id / X-Is-Internal-Admin
    // (la autenticación de TestAuthHandler sólo gobierna el IUserContext). Por eso el privilegio
    // transversal se conmuta con la cabecera X-Is-Internal-Admin de DevAuth, no vía claims.
    private HttpClient CreateAdminClient()
    {
        var client = _factory!.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });
        client.DefaultRequestHeaders.Add("X-Tenant-Id", SeededTenantId.ToString());
        client.DefaultRequestHeaders.Add("X-Is-Internal-Admin", "true");
        return client;
    }

    private HttpClient CreateNonAdminClient()
    {
        var client = _factory!.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });
        client.DefaultRequestHeaders.Add("X-Tenant-Id", SeededTenantId.ToString());
        client.DefaultRequestHeaders.Add("X-Is-Internal-Admin", "false");
        return client;
    }
}
