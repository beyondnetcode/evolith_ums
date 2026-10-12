using System.Data.Common;
using Evolith.Ums.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Evolith.Ums.Infrastructure.Persistence.Interceptors;

/// <summary>
/// EF Core connection interceptor that publishes the current organization/tenant id to the
/// PostgreSQL session as the GUC <c>app.current_organization_id</c>, so it can back a
/// database-level Row-Level Security (RLS) failsafe (G-020).
///
/// <para>
/// The previous implementation set a SQL Server session context (<c>sp_set_session_context</c>).
/// Under PostgreSQL-only persistence (D-008 / ADR-UMS-089) that T-SQL no longer applies, and the
/// interceptor had degraded to a no-op. Tenant isolation is still enforced primarily by the
/// application-layer EF Core global query filters on <c>TenantId</c> (see
/// <see cref="UmsPlatformDbContext"/>); RLS is defense-in-depth on top of them.
/// </para>
///
/// <para>
/// The GUC is set on every connection open (<c>false</c> = session scope), which is correct under
/// Npgsql pooling because the logical <c>Open()</c> — and therefore this interceptor — fires on
/// every checkout, overwriting any value left by a previous borrower of the physical connection.
/// A <see langword="null"/> organization id (internal admin / system / seeding) is published as an
/// empty string; the RLS policy must treat an empty setting as "no tenant restriction", mirroring
/// the query-filter short-circuit (<c>!OrganizationId.HasValue</c>).
/// </para>
///
/// <para>
/// <b>Enabling the RLS policies is intentionally NOT done here.</b> It requires a migration that
/// (1) <c>ALTER TABLE … ENABLE ROW LEVEL SECURITY</c> on every tenant-scoped table, (2) creates a
/// policy <c>USING ((SELECT current_setting('app.current_organization_id', true)) = '' OR
/// "TenantId" = (SELECT NULLIF(current_setting('app.current_organization_id', true), '')::uuid))</c>
/// — la GUC va envuelta en una subconsulta escalar para que PostgreSQL la evalúe una vez por
/// sentencia (InitPlan) y no una vez por fila, y la comparación es uuid contra uuid para no
/// inutilizar <c>IX_*_TenantId</c> (G-173, migración <c>FixRlsTenantComparisonTypeMismatch</c>) —,
/// and — critically —
/// (3) accounts for the fact that a table <b>owner</b> bypasses RLS unless
/// <c>FORCE ROW LEVEL SECURITY</c> is set, and that any DbContext touching those tables without this
/// interceptor (ReadModels, MasterData, raw ADO) would be filtered by a stale/empty GUC. Because RLS
/// is fail-closed, it must be verified on a live multi-tenant database before rollout (G-020).
/// </para>
/// </summary>
public class OrganizationDbContextInterceptor : DbConnectionInterceptor
{
    private const string Guc = "app.current_organization_id";

    private readonly ITenantContext _tenantContext;

    public OrganizationDbContextInterceptor(ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        ApplyOrganizationContext(connection);
        base.ConnectionOpened(connection, eventData);
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await ApplyOrganizationContextAsync(connection, cancellationToken).ConfigureAwait(false);
        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken).ConfigureAwait(false);
    }

    // Empty string when there is no tenant restriction (internal admin / system / seeding),
    // matching the query-filter short-circuit. set_config(..., false) = session scope; re-applied
    // on every open, so pooled connections never leak a previous tenant's value.
    private string CurrentOrganization()
        => _tenantContext.OrganizationId?.ToString() ?? string.Empty;

    /// <summary>
    /// Sentencia que publica la organización en la sesión, según el motor de la conexión.
    ///
    /// <para>No hay denominador común: <c>set_config</c> es de PostgreSQL y SQL Server lo resuelve
    /// con <c>sp_set_session_context</c>, leíble después con <c>SESSION_CONTEXT(N'...')</c>. Ambas
    /// tienen alcance de sesión y se reaplican en cada apertura lógica, así que ninguna filtra el
    /// valor de un inquilino anterior a través del pool.</para>
    ///
    /// <para>Una conexión de un proveedor no contemplado no recibe sentencia alguna: el aislamiento
    /// primario son los filtros globales de consulta por <c>TenantId</c>, y este interceptor solo
    /// alimenta el resguardo RLS, que hoy sigue sin activarse (G-020).</para>
    /// </summary>
    private static string? SentenciaDeContexto(DbConnection connection) => connection switch
    {
        Npgsql.NpgsqlConnection => $"SELECT set_config('{Guc}', @org, false)",
        Microsoft.Data.SqlClient.SqlConnection =>
            $"EXEC sp_set_session_context @key = N'{Guc}', @value = @org",
        _ => null,
    };

    private void ApplyOrganizationContext(DbConnection connection)
    {
        var sentencia = SentenciaDeContexto(connection);
        if (sentencia is null) return;

        using var command = connection.CreateCommand();
        command.CommandText = sentencia;
        command.Parameters.Add(CreateOrgParameter(command));
        command.ExecuteNonQuery();
    }

    private async Task ApplyOrganizationContextAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var sentencia = SentenciaDeContexto(connection);
        if (sentencia is null) return;

        await using var command = connection.CreateCommand();
        command.CommandText = sentencia;
        command.Parameters.Add(CreateOrgParameter(command));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private DbParameter CreateOrgParameter(DbCommand command)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = "org";
        parameter.Value = CurrentOrganization();
        return parameter;
    }
}
