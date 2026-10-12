using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Evolith.Ums.Infrastructure.Persistence.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class AddAuthorizationLookupIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_SystemSuiteDomainResources_ModuleId",
                schema: "ums_authorization",
                table: "SystemSuiteDomainResources",
                column: "ModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_Profiles_UserId_Active",
                schema: "ums_authorization",
                table: "Profiles",
                column: "UserId",
                filter: "\"IsActive\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_ProfilePermissions_TemplateId",
                schema: "ums_authorization",
                table: "ProfilePermissions",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_PermissionTemplateItems_TargetId_IsActive",
                schema: "ums_authorization",
                table: "PermissionTemplateItems",
                columns: new[] { "TargetId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SystemSuiteDomainResources_ModuleId",
                schema: "ums_authorization",
                table: "SystemSuiteDomainResources");

            migrationBuilder.DropIndex(
                name: "IX_Profiles_UserId_Active",
                schema: "ums_authorization",
                table: "Profiles");

            migrationBuilder.DropIndex(
                name: "IX_ProfilePermissions_TemplateId",
                schema: "ums_authorization",
                table: "ProfilePermissions");

            migrationBuilder.DropIndex(
                name: "IX_PermissionTemplateItems_TargetId_IsActive",
                schema: "ums_authorization",
                table: "PermissionTemplateItems");
        }
    }
}
