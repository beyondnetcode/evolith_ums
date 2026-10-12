using System.Linq;
using Microsoft.EntityFrameworkCore;
using Evolith.Ums.Infrastructure.Persistence.Audit;
using Evolith.Ums.Infrastructure.Persistence.Audit.Entities;
using Evolith.Ums.Infrastructure.Persistence.Configuration.Entities;
using Evolith.Ums.Infrastructure.Persistence.Interceptors;
using Evolith.Ums.Presentation.IntegrationTest.Infrastructure;
using AuditRecordAggregate = Evolith.Ums.Domain.Audit.AuditRecord.AuditRecord;

namespace Evolith.Ums.Presentation.IntegrationTest.Audit;

/// <summary>
/// G-081 — Anti-tamper de la traza de auditoría a NIVEL DE PERSISTENCIA, contra un PostgreSQL real
/// (Testcontainers). Es el punto del gap: la inmutabilidad ya se probaba estructuralmente en el
/// dominio (<c>AuditRecordTests.Record_HasNoMutationMethods_AppendOnly</c>) y la EMISIÓN en
/// <c>AuditTrailCommandTests</c>; faltaba garantizar que la capa de persistencia RECHAZA mutar o
/// borrar una traza ya escrita, y verificar el aislamiento por inquilino (<c>RootTenantId</c>).
///
/// <para>
/// El host <see cref="PostgreSqlWebApplicationFactory"/> re-registra el <see cref="UmsPlatformDbContext"/>
/// <b>sin interceptores</b> (para las pruebas de query-filter), de modo que el guard NO estaría
/// activo por esa vía. Por eso estos tests construyen el contexto directamente contra el contenedor
/// —igual que <see cref="PostgreSqlContainerFixture"/> hace para migrar—, cableando el interceptor
/// real <see cref="AuditAppendOnlyGuardInterceptor"/> (tal y como lo cablea producción en
/// <c>DependencyInjection</c>). Así se ejercita PostgreSQL real + mapeo EF real + guard real.
/// </para>
/// </summary>
[Collection("PostgreSql")]
public sealed class AuditTrailPersistenceAntiTamperTests
{
    private readonly PostgreSqlContainerFixture _fixture;

    public AuditTrailPersistenceAntiTamperTests(PostgreSqlContainerFixture fixture)
    {
        _fixture = fixture;
    }

    // ── Append OK: una traza se persiste por el flujo real (repo → SaveChanges con el guard) ──
    [Fact]
    public async Task AppendAuditRecord_ViaRepository_Persists()
    {
        if (!_fixture.IsAvailable)
        {
            Assert.Skip("Docker required.");
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        var tenant = Guid.NewGuid();
        var eventType = $"AppendOk_{Guid.NewGuid():N}";

        var id = await AppendAuditAsync(tenant, eventType, "traza legítima", ct);

        await using var verify = CreateGuardedContext();
        var repo = new PostgreSqlAuditRecordRepository(verify, new NoTenantContext());
        var persisted = await repo.GetByIdAsync(id, ct);

        persisted.Should().NotBeNull("una traza en estado Added debe persistirse: el guard solo bloquea UPDATE/DELETE");
        persisted!.EventType.Should().Be(eventType);
        persisted.RootTenantId.Should().Be(tenant);
        persisted.WhatChanged.Should().Be("traza legítima");
    }

    // ── Anti-tamper UPDATE: mutar una traza persistida y guardar → rechazado por el guard ──
    [Fact]
    public async Task UpdateAuditRecord_ViaEf_IsRejected()
    {
        if (!_fixture.IsAvailable)
        {
            Assert.Skip("Docker required.");
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        var tenant = Guid.NewGuid();
        var eventType = $"AntiTamperUpdate_{Guid.NewGuid():N}";
        var id = await AppendAuditAsync(tenant, eventType, "original", ct);

        await using var ctx = CreateGuardedContext();
        var record = await ctx.Set<AuditRecordRecord>().FirstAsync(x => x.Id == id, ct);
        record.WhatChanged = "MANIPULADO";

        var act = async () => await ctx.SaveChangesAsync(ct);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No repudio (G-081)*")
            .WithMessage("*append-only*");

        // La fila permanece intacta: el SaveChanges se abortó antes de tocar la BD.
        await using var verify = CreateGuardedContext();
        var reread = await verify.Set<AuditRecordRecord>().AsNoTracking().FirstAsync(x => x.Id == id, ct);
        reread.WhatChanged.Should().Be("original", "un UPDATE rechazado no debe persistir cambio alguno");
    }

    // ── Anti-tamper DELETE: intentar borrar una traza vía EF → rechazado por el guard ──
    [Fact]
    public async Task DeleteAuditRecord_ViaEf_IsRejected()
    {
        if (!_fixture.IsAvailable)
        {
            Assert.Skip("Docker required.");
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        var tenant = Guid.NewGuid();
        var eventType = $"AntiTamperDelete_{Guid.NewGuid():N}";
        var id = await AppendAuditAsync(tenant, eventType, "no borrable", ct);

        await using var ctx = CreateGuardedContext();
        var record = await ctx.Set<AuditRecordRecord>().FirstAsync(x => x.Id == id, ct);
        ctx.Set<AuditRecordRecord>().Remove(record);

        var act = async () => await ctx.SaveChangesAsync(ct);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No repudio (G-081)*");

        // La traza sigue presente: el DELETE fue rechazado.
        await using var verify = CreateGuardedContext();
        var stillThere = await verify.Set<AuditRecordRecord>().AsNoTracking().AnyAsync(x => x.Id == id, ct);
        stillThere.Should().BeTrue("un DELETE rechazado no debe borrar la traza");
    }

    // ── Aislamiento por inquilino: consultar como inquilino A no revela las trazas de B ──
    // La tabla de auditoría NO está cubierta por RLS (no figura en la migración EnableRowLevelSecurity)
    // ni por un global query filter de EF; el aislamiento se apoya en el filtro de aplicación del
    // repositorio (parámetro RootTenantId en QueryBy*). Se prueba ahí, donde vive.
    [Fact]
    public async Task QueryByTenant_DoesNotLeakOtherTenantsAuditRecords()
    {
        if (!_fixture.IsAvailable)
        {
            Assert.Skip("Docker required.");
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var eventType = $"Isolation_{Guid.NewGuid():N}"; // mismo tipo de evento en ambos: filtra el tenant

        var idA = await AppendAuditAsync(tenantA, eventType, "traza-A", ct);
        var idB = await AppendAuditAsync(tenantB, eventType, "traza-B", ct);

        await using var ctx = CreateGuardedContext();
        var repo = new PostgreSqlAuditRecordRepository(ctx, new NoTenantContext());
        var from = DateTime.UtcNow.AddMinutes(-5);
        var to = DateTime.UtcNow.AddMinutes(5);

        var forA = await repo.QueryByEventTypeAsync(eventType, tenantA, from, to, ct);
        forA.Should().OnlyContain(r => r.RootTenantId == tenantA, "el inquilino A solo debe ver sus propias trazas");
        forA.Select(r => r.GetId().GetValue()).Should().Contain(idA).And.NotContain(idB);

        var forB = await repo.QueryByEventTypeAsync(eventType, tenantB, from, to, ct);
        forB.Should().OnlyContain(r => r.RootTenantId == tenantB, "el inquilino B solo debe ver sus propias trazas");
        forB.Select(r => r.GetId().GetValue()).Should().Contain(idB).And.NotContain(idA);
    }

    // ── G-103: aislamiento por inquilino en la LECTURA POR ID. GetByIdAsync filtraba SOLO por Id,
    // así que un actor podía leer la traza de otro inquilino conociendo su Id (fuga cross-tenant).
    // La tabla de auditoría no lleva query filter global (rompería la lectura cross-tenant legítima
    // del admin de plataforma y las QueryBy* con inquilino explícito), así que el aislamiento vive
    // en el repositorio, igual que las QueryBy*. Aquí se prueba con PostgreSQL real. ──
    [Fact]
    public async Task GetById_OtherTenantRecord_AsRegularUser_ReturnsNull()
    {
        if (!_fixture.IsAvailable)
        {
            Assert.Skip("Docker required.");
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var eventType = $"CrossTenantRead_{Guid.NewGuid():N}";

        var idB = await AppendAuditAsync(tenantB, eventType, "traza-de-B", ct);

        // Actor REGULAR del inquilino A intentando leer la traza de B por un Id conocido.
        await using var ctx = CreateGuardedContext();
        var repo = new PostgreSqlAuditRecordRepository(ctx, new FixedTenantContext(tenantA, isInternalAdmin: false));

        var leaked = await repo.GetByIdAsync(idB, ct);

        leaked.Should().BeNull(
            "un actor del inquilino A no debe poder leer la traza de auditoría del inquilino B conociendo su Id (G-103)");
    }

    // ── G-103: la lectura por Id de la PROPIA traza sigue funcionando para un usuario regular. ──
    [Fact]
    public async Task GetById_OwnTenantRecord_AsRegularUser_ReturnsRecord()
    {
        if (!_fixture.IsAvailable)
        {
            Assert.Skip("Docker required.");
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        var tenantA = Guid.NewGuid();
        var eventType = $"OwnTenantRead_{Guid.NewGuid():N}";

        var idA = await AppendAuditAsync(tenantA, eventType, "traza-de-A", ct);

        await using var ctx = CreateGuardedContext();
        var repo = new PostgreSqlAuditRecordRepository(ctx, new FixedTenantContext(tenantA, isInternalAdmin: false));

        var own = await repo.GetByIdAsync(idA, ct);

        own.Should().NotBeNull("un actor debe poder leer la traza de su propio inquilino por Id");
        own!.RootTenantId.Should().Be(tenantA);
        own.EventType.Should().Be(eventType);
    }

    // ── G-103: el admin de plataforma (IsInternalAdmin) conserva la vista cross-tenant de la traza.
    // OrganizationId es su propio inquilino (≠ B), pero el bypass legítimo de admin interno —el mismo
    // patrón que usa GetAllAuditRecordsQueryHandler— le permite leer la traza de otro inquilino. ──
    [Fact]
    public async Task GetById_OtherTenantRecord_AsInternalAdmin_ReturnsRecord()
    {
        if (!_fixture.IsAvailable)
        {
            Assert.Skip("Docker required.");
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        var tenantB = Guid.NewGuid();
        var adminOwnTenant = Guid.NewGuid();
        var eventType = $"AdminCrossTenantRead_{Guid.NewGuid():N}";

        var idB = await AppendAuditAsync(tenantB, eventType, "traza-de-B", ct);

        await using var ctx = CreateGuardedContext();
        var repo = new PostgreSqlAuditRecordRepository(ctx, new FixedTenantContext(adminOwnTenant, isInternalAdmin: true));

        var seen = await repo.GetByIdAsync(idB, ct);

        seen.Should().NotBeNull(
            "el admin interno conserva la vista cross-tenant de la traza: no se rompe la consulta de auditoría del admin de plataforma");
        seen!.RootTenantId.Should().Be(tenantB);
    }

    // ── No-regresión: el guard es específico de la traza; NO bloquea la modificación (ni el
    // estampado) de una entidad normal que SÍ se modifica. Con el guard activo, insertar y luego
    // MODIFICAR una entidad no-auditoría debe funcionar y persistir. ──
    [Fact]
    public async Task ModifyNonAuditEntity_ViaEf_IsAllowedByTheGuard()
    {
        if (!_fixture.IsAvailable)
        {
            Assert.Skip("Docker required.");
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        var id = Guid.NewGuid();
        var code = $"pd_{Guid.NewGuid():N}";

        await using (var insert = CreateGuardedContext())
        {
            insert.Set<ParameterDefinitionRecord>().Add(new ParameterDefinitionRecord
            {
                Id = id,
                Code = code,
                Name = "original",
                Description = "def de no-regresión",
                DataTypeId = 1,
                DefaultValue = "x",
                ScopeId = 1,
                IsActive = true,
                IsMandatory = false,
                DisplayOrder = 0,
                Version = "1.0.0",
                CreatedBy = "tester",
                CreatedAtUtc = DateTime.UtcNow,
                AuditTimeSpan = "0",
            });
            await insert.SaveChangesAsync(ct);
        }

        await using (var update = CreateGuardedContext())
        {
            var pd = await update.Set<ParameterDefinitionRecord>().FirstAsync(x => x.Id == id, ct);
            pd.Name = "modificado";

            var act = async () => await update.SaveChangesAsync(ct);

            await act.Should().NotThrowAsync(
                "el guard anti-tamper solo protege la traza de auditoría (AuditRecordRecord); las entidades normales se modifican con normalidad");
        }

        await using var verify = CreateGuardedContext();
        var reread = await verify.Set<ParameterDefinitionRecord>().AsNoTracking().FirstAsync(x => x.Id == id, ct);
        reread.Name.Should().Be("modificado", "una modificación legítima de una entidad no-auditoría debe persistir");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    // Construye un UmsPlatformDbContext contra el contenedor con el guard anti-tamper cableado,
    // espejo de cómo AddInfrastructure lo registra en producción (options.AddInterceptors(...)).
    private UmsPlatformDbContext CreateGuardedContext()
    {
        var options = new DbContextOptionsBuilder<UmsPlatformDbContext>()
            .UseNpgsql(_fixture.ConnectionString, sql => sql.EnableRetryOnFailure(3))
            .AddInterceptors(new AuditAppendOnlyGuardInterceptor())
            .Options;

        return new UmsPlatformDbContext(
            options,
            new NoTenantContext(),
            new Moq.Mock<MediatR.IPublisher>().Object,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<UmsPlatformDbContext>.Instance);
    }

    // Persiste una traza por el repositorio real (AppendAsync → SaveChanges con el guard activo).
    private async Task<Guid> AppendAuditAsync(Guid rootTenantId, string eventType, string whatChanged, CancellationToken ct)
    {
        await using var ctx = CreateGuardedContext();
        var repo = new PostgreSqlAuditRecordRepository(ctx, new NoTenantContext());

        var record = AuditRecordAggregate.Record(
            whoActed: Guid.NewGuid(),
            subjectType: SubjectType.User,
            whatChanged: whatChanged,
            eventType: eventType,
            auditResult: AuditResult.Success,
            affectedEntityId: Guid.NewGuid(),
            affectedEntityType: "UserAccount",
            rootTenantId: rootTenantId,
            metadata: null).Value;

        await repo.AppendAsync(record, ct);
        await repo.SaveChangesAsync(ct);

        return record.GetId().GetValue();
    }

    // Contexto de inquilino nulo → sin restricción (vista sistema/admin); la tabla de auditoría no
    // lleva query filter, así que el aislamiento se prueba por el parámetro RootTenantId del repo.
    private sealed class NoTenantContext : ITenantContext
    {
        public Guid? OrganizationId => null;
        public Guid? OriginalTenantId => null;
        public bool IsInternalAdmin => true;
        public void Initialize(Guid userTenantId, bool isInternalAdmin) { }
        public void SetOrganizationId(Guid organizationId) { }
        public void EnableCrossTenantAccess() { }
        public void DisableCrossTenantAccess() { }
    }

    // Contexto de inquilino fijo para las pruebas de aislamiento de GetByIdAsync (G-103): un actor
    // ligado a un OrganizationId concreto, admin interno o no. Espeja la semántica de TenantContext.
    private sealed class FixedTenantContext : ITenantContext
    {
        public FixedTenantContext(Guid? organizationId, bool isInternalAdmin)
        {
            OrganizationId = organizationId;
            OriginalTenantId = organizationId;
            IsInternalAdmin = isInternalAdmin;
        }

        public Guid? OrganizationId { get; }
        public Guid? OriginalTenantId { get; }
        public bool IsInternalAdmin { get; }
        public void Initialize(Guid userTenantId, bool isInternalAdmin) { }
        public void SetOrganizationId(Guid organizationId) { }
        public void EnableCrossTenantAccess() { }
        public void DisableCrossTenantAccess() { }
    }
}
