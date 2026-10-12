using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Evolith.Ums.Infrastructure.Persistence.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class AddSingleManagementOwnerIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Tenants_SingleManagementOwner",
                schema: "ums_identity",
                table: "Tenants",
                column: "IsManagementOwner",
                unique: true,
                filter: "\"IsManagementOwner\" = true");
        }

        /// <inheritdoc />
        #pragma warning disable S1186
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tenants_SingleManagementOwner",
                schema: "ums_identity",
                table: "Tenants");
        }
#pragma warning restore S1186
    }
}
