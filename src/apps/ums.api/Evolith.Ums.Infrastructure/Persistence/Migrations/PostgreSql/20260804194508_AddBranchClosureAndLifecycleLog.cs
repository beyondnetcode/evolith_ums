using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Evolith.Ums.Infrastructure.Persistence.Migrations.PostgreSql
{
    /// <summary>
    /// ADR-0164 aplicado a las SUCURSALES. Dos cosas a la vez, porque son la misma decisión:
    ///
    /// <para><b>1. Cierre definitivo en lugar de borrado físico.</b> <c>Tenant.RemoveBranch</c> quitaba
    /// la sucursal de la colección y el reconciliador de EF lo traducía en un <c>DELETE</c> real. Como
    /// ni <c>Profiles.BranchId</c> ni <c>UserAccounts.BranchId</c> tienen clave ajena contra esta
    /// tabla, la base no decía nada y las filas quedaban huérfanas en silencio; y un despacho de 2024
    /// se quedaba sin la sucursal que lo explicaba. Ahora hay <c>IsClosed</c> / <c>ClosedAtUtc</c> /
    /// <c>ClosedBy</c>: la fila permanece. La columna nace con <c>DEFAULT false</c>, así que ninguna
    /// fila existente cambia de semántica — todo lo que hay hoy sigue vivo.</para>
    ///
    /// <para><b>Lo que esta migración NO hace, a propósito:</b> no toca
    /// <c>IX_TenantBranches_TenantId_Code</c>. El índice único sigue SIN filtrar por el estado de
    /// cierre (§2.3), que es lo que mantiene ocupado el código de una sucursal cerrada y evita que dos
    /// sucursales distintas compartan código dentro del mismo inquilino. Es una omisión deliberada,
    /// no un olvido.</para>
    ///
    /// <para><b>2. Bitácora de episodios.</b> <c>TenantBranchLifecycleEntries</c> registra cada
    /// apertura, baja, reapertura y cierre con su fecha, su autor y la foto (nombre y geocerca) de la
    /// época. Sin ella, un <c>bool IsActive</c> y una sola marca de auditoría hacen indistinguibles
    /// dos épocas de la misma sucursal, y una auditoría de 2028 sobre un despacho de 2024 vería la
    /// dirección y el responsable de hoy.</para>
    ///
    /// <para><b>Relleno del histórico.</b> Se siembra UN asiento de apertura por cada sucursal
    /// existente, tomado de su propia auditoría de creación, para que la bitácora no empiece a media
    /// historia. NO se inventan bajas ni reaperturas de las sucursales hoy inactivas: se sabe que
    /// están inactivas, no cuándo ni quién las desactivó, y un asiento inventado sería peor que la
    /// ausencia. Ese hueco se cierra solo con el uso.</para>
    /// </summary>
    public partial class AddBranchClosureAndLifecycleLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ClosedAtUtc",
                schema: "ums_identity",
                table: "TenantBranches",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClosedBy",
                schema: "ums_identity",
                table: "TenantBranches",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsClosed",
                schema: "ums_identity",
                table: "TenantBranches",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "TenantBranchLifecycleEntries",
                schema: "ums_identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    EpisodeId = table.Column<int>(type: "integer", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ActorId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NameSnapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    GeofencingSnapshot = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantBranchLifecycleEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantBranchLifecycleEntries_TenantBranches_BranchId",
                        column: x => x.BranchId,
                        principalSchema: "ums_identity",
                        principalTable: "TenantBranches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TenantBranches_IsClosed",
                schema: "ums_identity",
                table: "TenantBranches",
                column: "IsClosed",
                filter: "\"IsClosed\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_TenantBranchLifecycleEntries_BranchId_OccurredAtUtc",
                schema: "ums_identity",
                table: "TenantBranchLifecycleEntries",
                columns: new[] { "BranchId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TenantBranchLifecycleEntries_TenantId",
                schema: "ums_identity",
                table: "TenantBranchLifecycleEntries",
                column: "TenantId");

            // ── Relleno del histórico ────────────────────────────────────────────────────────────
            // Va ANTES de activar la RLS: la política es fail-closed —con la GUC de inquilino sin
            // fijar, `current_setting(...,true)` devuelve NULL y el predicado no se cumple— y una
            // migración no tiene inquilino de contexto, así que un INSERT posterior sería rechazado.
            // EpisodeId = 1 es BranchLifecycleEpisode.Opened.
            migrationBuilder.Sql(@"
                INSERT INTO ums_identity.""TenantBranchLifecycleEntries""
                    (""Id"", ""TenantId"", ""BranchId"", ""EpisodeId"", ""OccurredAtUtc"",
                     ""ActorId"", ""NameSnapshot"", ""GeofencingSnapshot"", ""Reason"")
                SELECT
                    gen_random_uuid(),
                    b.""TenantId"",
                    b.""Id"",
                    1,
                    b.""CreatedAtUtc"",
                    b.""CreatedBy"",
                    b.""Name"",
                    b.""GeofencingMetadata"",
                    'Asiento de apertura reconstruido desde la auditoría de creación al estrenar la bitácora.'
                FROM ums_identity.""TenantBranches"" b;
            ");

            // ── Aislamiento por inquilino ────────────────────────────────────────────────────────
            // Mismo tratamiento que el resto de tablas acotadas por inquilino: ENABLE + FORCE y la
            // política con el predicado ya optimizado de 20260804135513 (InitPlan por sentencia y
            // comparación uuid = uuid, sin conversión a texto, para no perder el índice).
            migrationBuilder.Sql(@"ALTER TABLE ums_identity.""TenantBranchLifecycleEntries"" ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql(@"ALTER TABLE ums_identity.""TenantBranchLifecycleEntries"" FORCE ROW LEVEL SECURITY;");
            migrationBuilder.Sql(@"
                CREATE POLICY tenant_isolation_policy ON ums_identity.""TenantBranchLifecycleEntries""
                FOR ALL
                USING (
                    (SELECT current_setting('app.current_organization_id', true)) = ''
                    OR ""TenantId"" = (SELECT NULLIF(current_setting('app.current_organization_id', true), '')::uuid)
                );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP POLICY IF EXISTS tenant_isolation_policy ON ums_identity.""TenantBranchLifecycleEntries"";");

            migrationBuilder.DropTable(
                name: "TenantBranchLifecycleEntries",
                schema: "ums_identity");

            migrationBuilder.DropIndex(
                name: "IX_TenantBranches_IsClosed",
                schema: "ums_identity",
                table: "TenantBranches");

            migrationBuilder.DropColumn(
                name: "ClosedAtUtc",
                schema: "ums_identity",
                table: "TenantBranches");

            migrationBuilder.DropColumn(
                name: "ClosedBy",
                schema: "ums_identity",
                table: "TenantBranches");

            migrationBuilder.DropColumn(
                name: "IsClosed",
                schema: "ums_identity",
                table: "TenantBranches");
        }
    }
}
