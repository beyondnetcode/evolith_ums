using Microsoft.EntityFrameworkCore;
using Evolith.Ums.Presentation.IntegrationTest.Infrastructure;

namespace Evolith.Ums.Presentation.IntegrationTest.Identity;

/// <summary>
/// ADR-0164 aplicado a las SUCURSALES, verificado contra PostgreSQL real (Testcontainers).
///
/// <para>Antes de este cambio, <c>DELETE /tenants/{id}/branches/{branchId}</c> ejecutaba un
/// <c>DELETE</c> de verdad: <c>Tenant.RemoveBranch</c> quitaba la sucursal de la colección y
/// <c>EfChildCollectionReconciler</c> traducía la ausencia en un borrado físico. Como
/// <c>UserAccounts.BranchId</c> y <c>Profiles.BranchId</c> no tienen clave ajena contra
/// <c>TenantBranches</c>, esas filas quedaban apuntando al vacío sin que nadie se enterara.</para>
///
/// <para>Lo que se fija aquí, y por qué cada cosa se mide donde se mide:</para>
/// <list type="bullet">
///   <item><description>
///     <b>La fila sigue en la base.</b> Se lee con un contexto EF propio, NO por el API: preguntar al
///     API no distingue «oculto» de «borrado», y esa distinción es justamente lo que hay que probar.
///   </description></item>
///   <item><description>
///     <b>Cerrar con referencias vivas se rechaza con 409</b> y el cuerpo nombra qué bloquea. Con la
///     contraprueba obligatoria: una vez que esa referencia deja de estar activa, el cierre procede.
///   </description></item>
///   <item><description>
///     <b>El código no se libera.</b> El índice único no filtra por el estado de cierre, y el alta
///     responde un conflicto legible en vez de reventar con un 23505.
///   </description></item>
///   <item><description><b>La bitácora registra el episodio</b>, con su fecha y su autor.</description></item>
/// </list>
/// </summary>
[Collection("PostgreSql")]
public sealed class BranchClosureTests : IntegrationTestBase
{
    /// <summary>Identificadores de <c>BranchLifecycleEpisode</c> tal y como se persisten.</summary>
    private const int EpisodioApertura = 1;
    private const int EpisodioCierre = 4;

    public BranchClosureTests(PostgreSqlContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Cerrar_DejaLaFilaEnLaBase_ConSuCodigoYSuAuditoria()
    {
        if (!Fixture.IsAvailable) Assert.Skip("Docker es necesario para las pruebas de integración PostgreSQL.");
        var ct = TestContext.Current.CancellationToken;

        var tenantId = await ProvisionarInquilinoAsync(ct);
        var (branchId, codigo) = await CrearSucursalAsync(tenantId, "Terminal Callao", ct);

        var cierre = await Client.DeleteAsync(
            $"/api/v1/tenants/{tenantId}/branches/{branchId}?reason=Cese%20de%20operaciones%20en%20la%20plaza", ct);
        cierre.StatusCode.Should().Be(HttpStatusCode.NoContent, await cierre.Content.ReadAsStringAsync(ct));

        // LA FILA SIGUE AHÍ. Esta es la afirmación central del ADR y se mide sobre el almacenamiento,
        // no sobre el API.
        await using var db = CrearContextoDirecto();
        var fila = await db.TenantBranches.SingleOrDefaultAsync(b => b.Id == branchId, ct);

        fila.Should().NotBeNull("el borrado es LÓGICO: la fila no puede desaparecer de la tabla");
        fila!.IsClosed.Should().BeTrue();
        fila.IsActive.Should().BeFalse("una sucursal cerrada evidentemente no opera");
        fila.ClosedAtUtc.Should().NotBeNull();
        fila.ClosedBy.Should().NotBeNullOrWhiteSpace("la fila debe decir quién la cerró");
        fila.Code.Should().Be(codigo, "el histórico conserva el código con el que operó");
        fila.Name.Should().Be("Terminal Callao");

        // Y la lectura ordinaria la esconde, que es lo que ve quien opera hoy.
        using var listado = await LeerSucursalesAsync(tenantId, ct);
        listado.RootElement.EnumerateArray().Any(b => b.GetProperty("branchId").GetGuid() == branchId)
            .Should().BeFalse();
    }

    [Fact]
    public async Task Cerrar_ConUsuariosActivos_Responde409ConElDesglose_YProcedeCuandoDejanDeEstarlo()
    {
        if (!Fixture.IsAvailable) Assert.Skip("Docker es necesario para las pruebas de integración PostgreSQL.");
        var ct = TestContext.Current.CancellationToken;

        var tenantId = await ProvisionarInquilinoAsync(ct);
        var (branchId, _) = await CrearSucursalAsync(tenantId, "Almacén Paita", ct);
        var userId = await CrearUsuarioActivoEnSucursalAsync(tenantId, branchId, ct);

        // (b) Referencia VIVA: se rechaza y se nombra qué bloquea.
        var bloqueado = await Client.DeleteAsync($"/api/v1/tenants/{tenantId}/branches/{branchId}", ct);
        bloqueado.StatusCode.Should().Be(HttpStatusCode.Conflict, await bloqueado.Content.ReadAsStringAsync(ct));

        using var error = JsonDocument.Parse(await bloqueado.Content.ReadAsStringAsync(ct));
        error.RootElement.GetProperty("errorCode").GetString().Should().Be("BRANCH_HAS_LIVE_REFERENCES");
        error.RootElement.GetProperty("message").GetString().Should().NotBeNullOrWhiteSpace();
        var deps = error.RootElement.GetProperty("blockingDependencies");
        deps.GetArrayLength().Should().BeGreaterThan(0);
        deps.EnumerateArray().Any(d => d.GetProperty("entityType").GetString() == "UserAccount")
            .Should().BeTrue("el desglose debe decir que lo que bloquea son cuentas de usuario");
        deps.EnumerateArray().First(d => d.GetProperty("entityType").GetString() == "UserAccount")
            .GetProperty("count").GetInt32().Should().Be(1);

        // Un rechazo no puede haber escrito nada.
        await using (var verificacion = CrearContextoDirecto())
        {
            (await verificacion.TenantBranches.SingleAsync(b => b.Id == branchId, ct)).IsClosed
                .Should().BeFalse("un cierre rechazado no toca la fila");
        }

        // CONTRAPRUEBA (ADR-0164 §2.2): lo ya retirado NO bloquea. Sin ella la regla se cumpliría por
        // accidente —bastaría con rechazar siempre— y nadie lo notaría.
        (await Client.PostAsync($"/api/v1/user-accounts/{userId}/block?reason=reubicacion", null, ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var cierre = await Client.DeleteAsync($"/api/v1/tenants/{tenantId}/branches/{branchId}", ct);
        cierre.StatusCode.Should().Be(HttpStatusCode.NoContent, await cierre.Content.ReadAsStringAsync(ct));
    }

    [Fact]
    public async Task ElCodigoDeUnaSucursalCerrada_NoSeLibera()
    {
        if (!Fixture.IsAvailable) Assert.Skip("Docker es necesario para las pruebas de integración PostgreSQL.");
        var ct = TestContext.Current.CancellationToken;

        var tenantId = await ProvisionarInquilinoAsync(ct);
        var (branchId, codigo) = await CrearSucursalAsync(tenantId, "Depósito Tacna", ct);

        (await Client.DeleteAsync($"/api/v1/tenants/{tenantId}/branches/{branchId}", ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // ADR-0164 §2.3: el índice único NO se filtra por el estado de cierre, así que el alta con el
        // mismo código responde un CONFLICTO DE DOMINIO legible —no una violación de índice
        // convertida en 500, y desde luego no un 201 que dejaría dos sucursales con el mismo código y
        // volvería ambigua cualquier consulta histórica.
        var reintento = await Client.PostAsJsonAsync($"/api/v1/tenants/{tenantId}/branches", new
        {
            code = codigo,
            name = "Depósito Tacna (segunda época)",
            geofencingMetadata = (string?)null,
        }, ct);

        reintento.StatusCode.Should().Be(HttpStatusCode.Conflict, await reintento.Content.ReadAsStringAsync(ct));

        await using var db = CrearContextoDirecto();
        var filas = await db.TenantBranches.Where(b => b.TenantId == tenantId && b.Code == codigo).ToListAsync(ct);
        filas.Should().HaveCount(1, "el código sigue ocupado por la sucursal cerrada y no admite una segunda");
    }

    [Fact]
    public async Task LaBitacora_RegistraElCierre_ConSuFechaSuAutorYSuMotivo()
    {
        if (!Fixture.IsAvailable) Assert.Skip("Docker es necesario para las pruebas de integración PostgreSQL.");
        var ct = TestContext.Current.CancellationToken;

        var tenantId = await ProvisionarInquilinoAsync(ct);
        var (branchId, _) = await CrearSucursalAsync(tenantId, "Terminal Matarani", ct);

        (await Client.PostAsync($"/api/v1/tenants/{tenantId}/branches/{branchId}/deactivate", null, ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Client.PostAsync($"/api/v1/tenants/{tenantId}/branches/{branchId}/reactivate", null, ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Client.DeleteAsync($"/api/v1/tenants/{tenantId}/branches/{branchId}?reason=Fin%20de%20concesion", ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Se lee de la TABLA, no del API: la bitácora tiene que estar persistida en la misma
        // transacción que el cambio de estado, no depender de un manejador post-commit que por
        // contrato (ADR-0098 D4) es best-effort y puede perder episodios sin avisar.
        await using var db = CrearContextoDirecto();
        var asientos = await db.TenantBranchLifecycleEntries
            .Where(e => e.BranchId == branchId)
            .OrderBy(e => e.OccurredAtUtc)
            .ToListAsync(ct);

        asientos.Should().HaveCount(4, "apertura, baja, reapertura y cierre");
        asientos[0].EpisodeId.Should().Be(EpisodioApertura);
        asientos[^1].EpisodeId.Should().Be(EpisodioCierre);
        asientos[^1].Reason.Should().Be("Fin de concesion");
        asientos.Should().OnlyContain(a => a.TenantId == tenantId);
        asientos.Should().OnlyContain(a => !string.IsNullOrWhiteSpace(a.ActorId));
        asientos.Should().OnlyContain(a => a.OccurredAtUtc != default);
        asientos.Should().OnlyContain(a => a.NameSnapshot == "Terminal Matarani");

        // Y es consultable por el API, también después de cerrada: preguntar por el pasado de una
        // sucursal cerrada es el caso de uso, no una excepción.
        var respuesta = await Client.GetAsync($"/api/v1/tenants/{tenantId}/branches/{branchId}/bitacora", ct);
        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        using var payload = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync(ct));
        payload.RootElement.EnumerateArray().Select(e => e.GetProperty("episode").GetString())
            .Should().Equal("Opened", "Deactivated", "Reactivated", "Closed");
    }

    [Fact]
    public async Task DesactivarYReactivar_SiguenFuncionando_YNoSeConfundenConCerrar()
    {
        if (!Fixture.IsAvailable) Assert.Skip("Docker es necesario para las pruebas de integración PostgreSQL.");
        var ct = TestContext.Current.CancellationToken;

        var tenantId = await ProvisionarInquilinoAsync(ct);
        var (branchId, _) = await CrearSucursalAsync(tenantId, "Sucursal Ilo", ct);

        (await Client.PostAsync($"/api/v1/tenants/{tenantId}/branches/{branchId}/deactivate", null, ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Desactivada NO es cerrada: sigue en el listado, marcada como inactiva, y vuelve.
        await using (var db = CrearContextoDirecto())
        {
            var fila = await db.TenantBranches.SingleAsync(b => b.Id == branchId, ct);
            fila.IsActive.Should().BeFalse();
            fila.IsClosed.Should().BeFalse("desactivar no es eliminar (ADR-0164 §2.4)");
            fila.ClosedAtUtc.Should().BeNull();
        }

        using (var listado = await LeerSucursalesAsync(tenantId, ct))
        {
            var dto = listado.RootElement.EnumerateArray()
                .Single(b => b.GetProperty("branchId").GetGuid() == branchId);
            dto.GetProperty("isActive").GetBoolean().Should().BeFalse();
            dto.GetProperty("isClosed").GetBoolean().Should().BeFalse();
        }

        (await Client.PostAsync($"/api/v1/tenants/{tenantId}/branches/{branchId}/reactivate", null, ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using (var db = CrearContextoDirecto())
        {
            (await db.TenantBranches.SingleAsync(b => b.Id == branchId, ct)).IsActive.Should().BeTrue();
        }
    }

    [Fact]
    public async Task UnaSucursalCerrada_NoSePuedeReactivar()
    {
        if (!Fixture.IsAvailable) Assert.Skip("Docker es necesario para las pruebas de integración PostgreSQL.");
        var ct = TestContext.Current.CancellationToken;

        var tenantId = await ProvisionarInquilinoAsync(ct);
        var (branchId, _) = await CrearSucursalAsync(tenantId, "Sucursal Chimbote", ct);

        (await Client.DeleteAsync($"/api/v1/tenants/{tenantId}/branches/{branchId}", ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // La puerta de atrás cerrada: al estado terminal no se entra ni se sale manipulando el
        // estado reversible (ADR-0164 §2.4).
        var reactivacion = await Client.PostAsync($"/api/v1/tenants/{tenantId}/branches/{branchId}/reactivate", null, ct);
        reactivacion.StatusCode.Should().Be(HttpStatusCode.Conflict, await reactivacion.Content.ReadAsStringAsync(ct));

        await using var db = CrearContextoDirecto();
        var fila = await db.TenantBranches.SingleAsync(b => b.Id == branchId, ct);
        fila.IsClosed.Should().BeTrue();
        fila.IsActive.Should().BeFalse("una reactivación rechazada no puede haber devuelto la sucursal al servicio");
    }

    // ── Utilidades de aprovisionamiento ─────────────────────────────────────

    private async Task<Guid> ProvisionarInquilinoAsync(CancellationToken ct)
    {
        // El host PostgreSQL corre con SeedDevData=false: cada prueba levanta su propio inquilino.
        var code = $"BRCL{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var response = await Client.PostAsJsonAsync("/api/v1/tenants", new
        {
            code,
            name = $"Operador logístico {code}",
            type = "CLIENT",
            isManagementOwner = false,
        }, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(ct));
        var tenantId = Guid.Parse(response.Headers.Location!.ToString().Split('/')[^1]);

        // ADR-0077: aprovisionar recursos de un inquilino CLIENT es una operación ON-BEHALF que solo
        // ejerce el internal-admin; sin la cabecera, TenantScopePolicy devuelve AUTH_015 → 400.
        Client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        Client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());
        Client.DefaultRequestHeaders.Remove("X-Is-Internal-Admin");
        Client.DefaultRequestHeaders.Add("X-Is-Internal-Admin", "true");

        return tenantId;
    }

    private async Task<(Guid BranchId, string Code)> CrearSucursalAsync(Guid tenantId, string nombre, CancellationToken ct)
    {
        var codigo = $"SUC{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var response = await Client.PostAsJsonAsync($"/api/v1/tenants/{tenantId}/branches", new
        {
            code = codigo,
            name = nombre,
            geofencingMetadata = (string?)null,
        }, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(ct));

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return (payload.RootElement.GetProperty("branchId").GetGuid(), codigo);
    }

    private async Task<Guid> CrearUsuarioActivoEnSucursalAsync(Guid tenantId, Guid branchId, CancellationToken ct)
    {
        var response = await Client.PostAsJsonAsync("/api/v1/user-accounts", new
        {
            tenantId,
            branchId,
            email = $"operador.{Guid.NewGuid():N}"[..24] + "@evolith.local",
            category = "Internal",
            identityReference = $"EMP-{Guid.NewGuid():N}"[..10],
            identityReferenceType = "HrId",
        }, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(ct));

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var userId = payload.RootElement.GetProperty("userAccountId").GetGuid();

        (await Client.PostAsync($"/api/v1/user-accounts/{userId}/activate", null, ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        return userId;
    }

    private async Task<JsonDocument> LeerSucursalesAsync(Guid tenantId, CancellationToken ct)
    {
        var response = await Client.GetAsync($"/api/v1/tenants/{tenantId}/branches", ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(ct));
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
    }

    /// <summary>
    /// Contexto EF conectado al MISMO contenedor que el API, con inquilino nulo para que ningún filtro
    /// global recorte la vista. Es la ventana al almacenamiento real: lo que el API oculta, aquí se ve.
    /// </summary>
    private UmsPlatformDbContext CrearContextoDirecto()
    {
        var options = new DbContextOptionsBuilder<UmsPlatformDbContext>()
            .UseNpgsql(Fixture.ConnectionString)
            .Options;

        return new UmsPlatformDbContext(
            options,
            new ContextoDeInquilinoDelSistema(),
            new Moq.Mock<MediatR.IPublisher>().Object,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<UmsPlatformDbContext>.Instance);
    }

    private sealed class ContextoDeInquilinoDelSistema : ITenantContext
    {
        public Guid? OrganizationId => null;
        public Guid? OriginalTenantId => null;
        public bool IsInternalAdmin => true;
        public void Initialize(Guid userTenantId, bool isInternalAdmin) { }
        public void SetOrganizationId(Guid organizationId) { }
        public void EnableCrossTenantAccess() { }
        public void DisableCrossTenantAccess() { }
    }
}
