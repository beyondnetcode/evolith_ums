using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Evolith.Ums.Infrastructure.Persistence.Migrations.PostgreSql
{
    /// <summary>
    /// El borrado lógico LIBERA la ranura de configuración: los cuatro índices únicos del esquema
    /// `ums_configuration` pasan a ser PARCIALES y dejan fuera lo eliminado.
    ///
    /// Por qué solo aquí. ADR-0164 §2.3 fijó que la clave natural queda ocupada para siempre, y para
    /// una sucursal o un sistema es correcto: su código lo inventa quien opera —cierras LIMA-01 y
    /// abres LIMA-05— y reutilizarlo volvería ambigua una consulta histórica. El código de una
    /// configuración es otra cosa: viene de un catálogo cerrado. `MFA_REQUIRED_FOR_ADMIN` es *el*
    /// nombre de ese parámetro, no uno que se elija. Dejar esa ranura ocupada equivalía a que borrar
    /// la configuración global de un parámetro impidiera volver a configurarlo NUNCA. El propietario
    /// del producto acotó la regla el 2026-08-04; sucursales y `SystemSuite` NO cambian.
    ///
    /// Qué NO cambia: la fila eliminada sigue en la tabla. Esto no reintroduce el borrado físico —el
    /// repositorio sigue sin exponerlo— y todo lo demás de ADR-0164 continúa vigente.
    ///
    /// Sobre la reversión: `Down` restaura los índices totales y solo puede aplicarse si en ese
    /// momento no conviven una fila viva y una lápida con la misma clave. Es la consecuencia
    /// esperable de revertir una regla que ya se usó; el dato no se pierde, la reversión se rechaza.
    /// </summary>
    public partial class LiberarRanuraDeConfiguracionAlBorrar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ParameterTenantValues_TenantId_ParameterDefinitionId",
                schema: "ums_configuration",
                table: "ParameterTenantValues");

            migrationBuilder.DropIndex(
                name: "IX_ParameterGlobalValues_ParameterDefinitionId",
                schema: "ums_configuration",
                table: "ParameterGlobalValues");

            migrationBuilder.DropIndex(
                name: "IX_ParameterDefinitions_Code",
                schema: "ums_configuration",
                table: "ParameterDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_AppConfigurations_TenantId_SystemSuiteId_ModuleId_Code",
                schema: "ums_configuration",
                table: "AppConfigurations");

            migrationBuilder.CreateIndex(
                name: "IX_ParameterTenantValues_TenantId_ParameterDefinitionId",
                schema: "ums_configuration",
                table: "ParameterTenantValues",
                columns: new[] { "TenantId", "ParameterDefinitionId" },
                unique: true,
                filter: "\"StatusId\" != 4");

            migrationBuilder.CreateIndex(
                name: "IX_ParameterGlobalValues_ParameterDefinitionId",
                schema: "ums_configuration",
                table: "ParameterGlobalValues",
                column: "ParameterDefinitionId",
                unique: true,
                filter: "\"StatusId\" != 4");

            migrationBuilder.CreateIndex(
                name: "IX_ParameterDefinitions_Code",
                schema: "ums_configuration",
                table: "ParameterDefinitions",
                column: "Code",
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_AppConfigurations_TenantId_SystemSuiteId_ModuleId_Code",
                schema: "ums_configuration",
                table: "AppConfigurations",
                columns: new[] { "TenantId", "SystemSuiteId", "ModuleId", "Code" },
                unique: true,
                filter: "\"StatusId\" != 4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ParameterTenantValues_TenantId_ParameterDefinitionId",
                schema: "ums_configuration",
                table: "ParameterTenantValues");

            migrationBuilder.DropIndex(
                name: "IX_ParameterGlobalValues_ParameterDefinitionId",
                schema: "ums_configuration",
                table: "ParameterGlobalValues");

            migrationBuilder.DropIndex(
                name: "IX_ParameterDefinitions_Code",
                schema: "ums_configuration",
                table: "ParameterDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_AppConfigurations_TenantId_SystemSuiteId_ModuleId_Code",
                schema: "ums_configuration",
                table: "AppConfigurations");

            migrationBuilder.CreateIndex(
                name: "IX_ParameterTenantValues_TenantId_ParameterDefinitionId",
                schema: "ums_configuration",
                table: "ParameterTenantValues",
                columns: new[] { "TenantId", "ParameterDefinitionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParameterGlobalValues_ParameterDefinitionId",
                schema: "ums_configuration",
                table: "ParameterGlobalValues",
                column: "ParameterDefinitionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParameterDefinitions_Code",
                schema: "ums_configuration",
                table: "ParameterDefinitions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppConfigurations_TenantId_SystemSuiteId_ModuleId_Code",
                schema: "ums_configuration",
                table: "AppConfigurations",
                columns: new[] { "TenantId", "SystemSuiteId", "ModuleId", "Code" },
                unique: true);
        }
    }
}
