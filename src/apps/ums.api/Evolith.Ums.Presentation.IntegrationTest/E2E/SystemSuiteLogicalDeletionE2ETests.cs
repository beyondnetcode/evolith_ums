using Npgsql;
using Evolith.Ums.Presentation.IntegrationTest.Infrastructure;

namespace Evolith.Ums.Presentation.IntegrationTest.E2E;

/// <summary>
/// Eliminación LÓGICA de un sistema del catálogo — G-246.
///
/// <para>La política es del propietario del producto y no admite matices: <b>el borrado físico no
/// existe</b>. Sobre el catálogo se consultan datos antiguos y una fila borrada de verdad se pierde
/// para siempre. Un sistema eliminado se marca <c>Deleted</c>, sigue en la tabla y desaparece de
/// todas las lecturas.</para>
///
/// <para>La segunda mitad de la política es la <b>regla de cascada</b>, análoga a un
/// <c>ON DELETE RESTRICT</c>: no se elimina lógicamente algo a lo que todavía apuntan referencias
/// vivas. Las que ya están eliminadas lógicamente no bloquean.</para>
///
/// <para>Estas pruebas van contra PostgreSQL real porque las dos mitades son exactamente lo que un
/// doble en memoria no puede demostrar: el conteo sobre las tablas que apuntan al sistema, y que la
/// fila siga ahí después del DELETE.</para>
///
/// Requisito: Docker en marcha.
/// </summary>
[Collection("PostgreSql")]
public sealed class SystemSuiteLogicalDeletionE2ETests
{
    private const string TenantsTable = "ums_identity.\"Tenants\"";
    private const string SystemSuitesTable = "ums_authorization.\"SystemSuites\"";

    private readonly PostgreSqlContainerFixture _fixture;
    private readonly HttpClient _client;

    public SystemSuiteLogicalDeletionE2ETests(PostgreSqlContainerFixture fixture)
    {
        _fixture = fixture;

        if (fixture.IsAvailable)
        {
            var factory = new PostgreSqlWebApplicationFactory(fixture.ConnectionString);
            _client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false,
            });
            _client.DefaultRequestHeaders.Add("X-User-Id", TestAuthHandler.AdministradorSembrado);
            _client.DefaultRequestHeaders.Add("X-User-Name", "e2e-logical-deletion");
            // ADR-0077: aprovisionar y ELIMINAR sistemas son operaciones on-behalf del internal-admin
            // sobre un inquilino CLIENT auxiliar; sin este contexto TenantScopePolicy devuelve AUTH_015.
            _client.DefaultRequestHeaders.Add("X-Is-Internal-Admin", "true");
        }
        else
        {
            _client = new HttpClient();
        }
    }

    [Fact]
    public async Task DeleteSystemSuite_DeprecatedWithoutLiveReferences_Returns204AndDisappearsFromReads()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker required.");
        var ct = TestContext.Current.CancellationToken;

        var suiteId = await CreateSuiteId(ct);

        // El sistema lleva su propia topología: es la forma exacta en que el carril A los deja
        // (suite → módulo → nodo). La composición del agregado no debe bloquear la eliminación.
        var moduleId = await AddModuleAsync(suiteId, ct);
        await AddRootNodeAsync(suiteId, moduleId, ct);

        var code = await CodeOfAsync(suiteId, ct);
        await DeprecateAsync(suiteId, ct);

        var delete = await _client.DeleteAsync($"/api/v1/system-suites/{suiteId}", ct);
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterGet = await _client.GetAsync($"/api/v1/system-suites/{suiteId}", ct);
        afterGet.StatusCode.Should().Be(
            HttpStatusCode.NotFound,
            because: "un sistema eliminado lógicamente deja de leerse, aunque su fila siga en la base");

        // Y tampoco por el listado, que es la otra puerta de lectura: si el filtro solo cubriera el
        // GET por id, el catálogo seguiría mostrando lo eliminado.
        (await ListedCodesAsync(ct)).Should().NotContain(
            code,
            because: "lo eliminado tampoco aparece en el catálogo");
    }

    /// <summary>
    /// LA prueba de la política: tras el 204, la fila SIGUE en la base con su estado terminal.
    ///
    /// Se consulta con SQL crudo y no por la API a propósito: la API está obligada a no verla, así
    /// que preguntarle a ella no distinguiría «oculto» de «borrado». Aquí se mira el almacenamiento.
    /// </summary>
    [Fact]
    public async Task DeleteSystemSuite_RowSurvivesInDatabaseWithDeletedStatus()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker required.");
        var ct = TestContext.Current.CancellationToken;

        var suiteId = await CreateSuiteId(ct);
        await DeprecateAsync(suiteId, ct);

        (await _client.DeleteAsync($"/api/v1/system-suites/{suiteId}", ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var statusId = await ScalarAsync(
            $"SELECT \"StatusId\" FROM {SystemSuitesTable} WHERE \"Id\" = @id",
            ct,
            ("id", suiteId));

        statusId.Should().NotBeNull(
            because: "el borrado es LÓGICO: la fila no se borra, porque sobre el catálogo se consultan datos antiguos");
        statusId.Should().Be(
            SystemStatus.Deleted.Id,
            because: "la fila que sobrevive debe quedar marcada como eliminada, no en un estado cualquiera");
    }

    [Fact]
    public async Task DeleteSystemSuite_WithLiveRole_Returns409WithBlockingDependenciesAndSuiteSurvives()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker required.");
        var ct = TestContext.Current.CancellationToken;

        var suiteId = await CreateSuiteId(ct);
        await CreateRoleAsync(suiteId, ct);

        await DeprecateAsync(suiteId, ct);

        var delete = await _client.DeleteAsync($"/api/v1/system-suites/{suiteId}", ct);
        delete.StatusCode.Should().Be(HttpStatusCode.Conflict);

        using var error = JsonDocument.Parse(await delete.Content.ReadAsStringAsync(ct));
        error.RootElement.GetProperty("errorCode").GetString()
            .Should().Be("SYSTEM_SUITE_HAS_DEPENDENTS",
                because: "el rechazo debe ser una regla de dominio con nombre, no una violación de FK");

        var blockers = error.RootElement.GetProperty("blockingDependencies");
        blockers.EnumerateArray()
            .Any(d => d.GetProperty("entityType").GetString() == "Role" && d.GetProperty("count").GetInt32() > 0)
            .Should().BeTrue(because: "la respuesta debe decir QUÉ bloquea, para que el llamador sepa qué eliminar antes");

        var afterGet = await _client.GetAsync($"/api/v1/system-suites/{suiteId}", ct);
        afterGet.StatusCode.Should().Be(HttpStatusCode.OK, because: "una eliminación rechazada no cambia nada");
    }

    /// <summary>
    /// Las dos caras de la regla de cascada sobre el MISMO dependiente.
    ///
    /// <para>Se usa el inquilino que apunta al sistema como su sistema por defecto porque hoy es el
    /// ÚNICO de los siete referentes con eliminación lógica de verdad (<c>Tenants.IsDeleted</c>).
    /// Roles, plantillas, banderas, configuraciones y flujos de aprobación no la tienen: para ellos
    /// toda fila existente cuenta como viva, y por eso la mitad «ya eliminado, deja pasar» no se
    /// puede demostrar con un rol.</para>
    ///
    /// <para>El estado se dispone con SQL porque la API no expone ni fijar el sistema por defecto de
    /// un inquilino ni eliminar un inquilino; lo que la prueba ejercita es la GUARDA, no esos
    /// caminos.</para>
    /// </summary>
    [Fact]
    public async Task DeleteSystemSuite_TenantDefault_BlocksWhileLive_AndPassesOnceLogicallyDeleted()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker required.");
        var ct = TestContext.Current.CancellationToken;

        var suiteId = await CreateSuiteId(ct);
        var dependentTenantId = await CreateTenantAsync(setAsCurrent: false, ct);

        await ExecuteAsync(
            $"UPDATE {TenantsTable} SET \"DefaultSystemSuiteId\" = @suite WHERE \"Id\" = @tenant",
            ct,
            ("suite", suiteId), ("tenant", dependentTenantId));

        await DeprecateAsync(suiteId, ct);

        // (b) Referencia VIVA → bloquea, con el desglose.
        var blocked = await _client.DeleteAsync($"/api/v1/system-suites/{suiteId}", ct);
        blocked.StatusCode.Should().Be(HttpStatusCode.Conflict);

        using (var error = JsonDocument.Parse(await blocked.Content.ReadAsStringAsync(ct)))
        {
            error.RootElement.GetProperty("errorCode").GetString().Should().Be("SYSTEM_SUITE_HAS_DEPENDENTS");
            error.RootElement.GetProperty("blockingDependencies").EnumerateArray()
                .Any(d => d.GetProperty("entityType").GetString() == "Tenant" && d.GetProperty("count").GetInt32() > 0)
                .Should().BeTrue(because: "el inquilino que lo usa por defecto es el referente más peligroso: se queda sin destino de autenticación");
        }

        // (c) El MISMO dependiente, ya eliminado lógicamente → es una lápida y deja de bloquear.
        await ExecuteAsync(
            $"UPDATE {TenantsTable} SET \"IsDeleted\" = true, \"DeletedAtUtc\" = (now() at time zone 'utc'), \"DeletedBy\" = 'e2e-logical-deletion' WHERE \"Id\" = @tenant",
            ct,
            ("tenant", dependentTenantId));

        var allowed = await _client.DeleteAsync($"/api/v1/system-suites/{suiteId}", ct);
        allowed.StatusCode.Should().Be(
            HttpStatusCode.NoContent,
            because: "lo ya eliminado lógicamente no es una referencia real: no puede bloquear para siempre");
    }

    [Fact]
    public async Task DeleteSystemSuite_StillInService_Returns409()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker required.");
        var ct = TestContext.Current.CancellationToken;

        // Sin archivar (nace Active): solo se elimina lo que ya se dio de baja.
        var suiteId = await CreateSuiteId(ct);

        var delete = await _client.DeleteAsync($"/api/v1/system-suites/{suiteId}", ct);
        delete.StatusCode.Should().Be(HttpStatusCode.Conflict);

        (await _client.GetAsync($"/api/v1/system-suites/{suiteId}", ct))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeleteSystemSuite_Repeated_Returns404()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker required.");
        var ct = TestContext.Current.CancellationToken;

        var suiteId = await CreateSuiteId(ct);
        await DeprecateAsync(suiteId, ct);

        (await _client.DeleteAsync($"/api/v1/system-suites/{suiteId}", ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Ya no se lee, así que el segundo intento no lo encuentra: coherente con el GET.
        (await _client.DeleteAsync($"/api/v1/system-suites/{suiteId}", ct))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SetStatus_ToDeleted_Returns409()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker required.");
        var ct = TestContext.Current.CancellationToken;

        // La puerta de atrás: si el cambio de estado admitiera «Deleted», eliminaría el sistema sin
        // pasar por la guarda de cascada.
        var suiteId = await CreateSuiteId(ct);

        var res = await _client.PutAsJsonAsync(
            $"/api/v1/system-suites/{suiteId}/status", new { status = "Deleted" }, ct);

        res.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await _client.GetAsync($"/api/v1/system-suites/{suiteId}", ct))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Consecuencia directa de que la fila sobreviva: su código sigue ocupado.
    ///
    /// El índice único <c>(TenantId, Code)</c> cuenta también las lápidas, así que el alta tiene que
    /// verlas. Si no las viera, reutilizar el código pasaría la validación y reventaría en el commit
    /// con una violación de integridad: un 500 en lugar de una respuesta con sentido.
    /// </summary>
    [Fact]
    public async Task CreateSystemSuite_ReusingCodeOfDeletedSuite_IsRejectedCleanly()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker required.");
        var ct = TestContext.Current.CancellationToken;

        var suiteId = await CreateSuiteId(ct);
        var code = await CodeOfAsync(suiteId, ct);
        var tenantId = await ScalarAsync(
            $"SELECT \"TenantId\" FROM {SystemSuitesTable} WHERE \"Id\" = @id", ct, ("id", suiteId));

        await DeprecateAsync(suiteId, ct);
        (await _client.DeleteAsync($"/api/v1/system-suites/{suiteId}", ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var res = await _client.PostAsJsonAsync("/api/v1/system-suites", new
        {
            tenantId = (Guid)tenantId!,
            code,
            name = "Sistema que reutiliza un código eliminado",
            description = "El código de un sistema eliminado sigue ocupado por su lápida.",
        }, ct);

        ((int)res.StatusCode).Should().BeLessThan(
            500,
            because: "el rechazo debe ser una respuesta de negocio, nunca una violación de índice único");
        res.StatusCode.Should().BeOneOf(HttpStatusCode.Conflict, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeleteSystemSuite_UnknownId_Returns404()
    {
        if (!_fixture.IsAvailable) Assert.Skip("Docker required.");
        var ct = TestContext.Current.CancellationToken;

        var delete = await _client.DeleteAsync($"/api/v1/system-suites/{Guid.NewGuid()}", ct);
        delete.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private async Task DeprecateAsync(Guid suiteId, CancellationToken ct)
    {
        var res = await _client.PutAsJsonAsync(
            $"/api/v1/system-suites/{suiteId}/status", new { status = "Deprecated" }, ct);
        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private async Task<string> CodeOfAsync(Guid suiteId, CancellationToken ct)
        => (string?)await ScalarAsync($"SELECT \"Code\" FROM {SystemSuitesTable} WHERE \"Id\" = @id", ct, ("id", suiteId))
           ?? throw new InvalidOperationException($"El sistema {suiteId} no existe ni siquiera en la base.");

    private async Task<IReadOnlyList<string?>> ListedCodesAsync(CancellationToken ct)
    {
        var res = await _client.GetAsync("/api/v1/system-suites?page=1&pageSize=100", ct);
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        var items = doc.RootElement.TryGetProperty("items", out var itemsElement)
            ? itemsElement
            : doc.RootElement;

        return items.EnumerateArray()
            .Select(x => x.TryGetProperty("code", out var code) ? code.GetString() : null)
            .ToList();
    }

    private async Task<Guid> AddModuleAsync(Guid suiteId, CancellationToken ct)
    {
        var res = await _client.PostAsJsonAsync($"/api/v1/system-suites/{suiteId}/modules", new
        {
            systemSuiteId = suiteId,
            code = UniqueCode("DELMOD"),
            name = "Módulo de la suite eliminada",
            description = "Composición propia del sistema, creada por el E2E de eliminación lógica.",
            sortOrder = 1,
        }, ct);

        res.StatusCode.Should().Be(HttpStatusCode.Created);
        return await ReadGuid(res, "moduleId", ct);
    }

    private async Task AddRootNodeAsync(Guid suiteId, Guid moduleId, CancellationToken ct)
    {
        // Los nodos exigen módulo activo (el dominio rechaza colgar navegación de un módulo inactivo).
        (await _client.PostAsync($"/api/v1/system-suites/{suiteId}/modules/{moduleId}/activate", null, ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var res = await _client.PostAsJsonAsync($"/api/v1/system-suites/{suiteId}/modules/{moduleId}/nodes", new
        {
            systemSuiteId = suiteId,
            moduleId,
            parentNodeId = (Guid?)null,
            kind = "Menu",
            code = UniqueCode("DELNODE"),
            label = "Menú de la suite eliminada",
            description = "Nodo raíz creado por el E2E de eliminación lógica.",
            sortOrder = 1,
        }, ct);

        res.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    private async Task<Guid> CreateRoleAsync(Guid suiteId, CancellationToken ct)
    {
        var res = await _client.PostAsJsonAsync($"/api/v1/system-suites/{suiteId}/roles", new
        {
            code = UniqueCode("DELROLE"),
            value = "Rol que bloquea la eliminación",
            description = "Referencia externa viva del sistema, creada por el E2E de eliminación lógica.",
            parentRoleId = (Guid?)null,
            hierarchyLevel = 0,
            promotionOrder = 0,
        }, ct);

        res.StatusCode.Should().Be(HttpStatusCode.Created);
        return await ReadGuid(res, "roleId", ct);
    }

    // Los códigos se normalizan en el dominio (Trim + ToUpperInvariant); se normalizan aquí para que
    // lo enviado coincida con lo almacenado.
    private static string UniqueCode(string prefix)
        => $"{prefix}{Guid.NewGuid():N}"[..Math.Min(20, prefix.Length + 32)].ToUpperInvariant();

    /// <param name="setAsCurrent">
    /// Si el inquilino recién creado pasa a ser el del encabezado <c>X-Tenant-Id</c>. Los inquilinos
    /// auxiliares —el que solo referencia al sistema— NO deben cambiarlo: el filtro de inquilino
    /// escondería el sistema bajo prueba, que pertenece a otro.
    /// </param>
    private async Task<Guid> CreateTenantAsync(bool setAsCurrent, CancellationToken ct)
    {
        var uid = Guid.NewGuid().ToString("N")[..10].ToUpper();
        // G-037: la propiedad de gestión es única; el inquilino CLIENT auxiliar no debe reclamarla.
        var payload = new
        {
            code = $"T{uid}",
            name = $"E2E Eliminacion Tenant {uid}",
            type = "CLIENT",
            idpStrategy = (string?)null,
            companyReference = (string?)null,
            isManagementOwner = false,
        };

        var response = await _client.PostAsJsonAsync("/api/v1/tenants", payload, ct);
        response.EnsureSuccessStatusCode();

        var id = Guid.Parse(response.Headers.Location!.ToString().Split('/')[^1]);

        if (setAsCurrent)
        {
            _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
            _client.DefaultRequestHeaders.Add("X-Tenant-Id", id.ToString());
        }

        return id;
    }

    private async Task<Guid> CreateSuiteId(CancellationToken ct)
    {
        var tenantId = await CreateTenantAsync(setAsCurrent: true, ct);
        var uid = Guid.NewGuid().ToString("N")[..8].ToUpper();

        var res = await _client.PostAsJsonAsync("/api/v1/system-suites", new
        {
            tenantId,
            code = $"DEL{uid}",
            name = $"E2E Eliminacion Suite {uid}",
            description = "Sistema creado por el E2E de eliminación lógica (G-246).",
        }, ct);

        res.StatusCode.Should().Be(HttpStatusCode.Created);
        return await ReadGuid(res, "systemSuiteId", ct);
    }

    private static async Task<Guid> ReadGuid(HttpResponseMessage response, string property, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return doc.RootElement.GetProperty(property).GetGuid();
    }

    // ── Acceso directo a la base ─────────────────────────────────────────────
    // Se usa para DOS cosas que la API no puede dar: disponer estados que no expone (el sistema por
    // defecto de un inquilino, la eliminación lógica de un inquilino) y, sobre todo, mirar el
    // almacenamiento por debajo de la API para distinguir «oculto» de «borrado».

    private async Task<NpgsqlConnection> OpenAsync(CancellationToken ct)
    {
        var connection = new NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync(ct);

        // Neutraliza la compuerta de RLS (`app.current_organization_id` vacía = sin restricción de
        // inquilino). Sin esto, la aserción mediría visibilidad y no existencia, que es justo lo que
        // esta prueba tiene que separar.
        await using var gate = new NpgsqlCommand("SET app.current_organization_id = ''", connection);
        await gate.ExecuteNonQueryAsync(ct);

        return connection;
    }

    private async Task ExecuteAsync(string sql, CancellationToken ct, params (string Name, object Value)[] parameters)
    {
        await using var connection = await OpenAsync(ct);
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        await command.ExecuteNonQueryAsync(ct);
    }

    /// <summary>Escalar crudo, o <c>null</c> si la consulta no devolvió fila.</summary>
    private async Task<object?> ScalarAsync(string sql, CancellationToken ct, params (string Name, object Value)[] parameters)
    {
        await using var connection = await OpenAsync(ct);
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        var result = await command.ExecuteScalarAsync(ct);
        return result is DBNull ? null : result;
    }
}
