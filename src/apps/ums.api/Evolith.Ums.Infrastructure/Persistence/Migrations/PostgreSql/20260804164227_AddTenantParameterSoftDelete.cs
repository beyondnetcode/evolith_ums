using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Evolith.Ums.Infrastructure.Persistence.Migrations.PostgreSql
{
    /// <summary>
    /// Borrado LÓGICO de parámetros de inquilino. El repositorio hacía DELETE físico y la
    /// configuración histórica del inquilino se perdía sin remedio; la política del propietario es que
    /// solo exista borrado lógico porque el negocio consulta datos antiguos.
    ///
    /// La columna nace con DEFAULT false, así que ninguna fila existente cambia de semántica: todo lo
    /// que hay hoy está vivo. El índice parcial cubre el predicado que el filtro global añade a TODA
    /// consulta de parámetros, igual que en Tenants y UserAccounts.
    /// </summary>
    public partial class AddTenantParameterSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                schema: "ums_identity",
                table: "TenantParameters",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_TenantParameters_IsDeleted",
                schema: "ums_identity",
                table: "TenantParameters",
                column: "IsDeleted",
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TenantParameters_IsDeleted",
                schema: "ums_identity",
                table: "TenantParameters");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                schema: "ums_identity",
                table: "TenantParameters");
        }
    }
}
