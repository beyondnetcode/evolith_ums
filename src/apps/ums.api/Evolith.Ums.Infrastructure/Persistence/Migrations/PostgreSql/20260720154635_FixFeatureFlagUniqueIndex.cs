using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Evolith.Ums.Infrastructure.Persistence.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class FixFeatureFlagUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FeatureFlags_SystemSuiteId_FlagCode",
                schema: "ums_configuration",
                table: "FeatureFlags");

            migrationBuilder.CreateIndex(
                name: "IX_FeatureFlags_SystemSuiteId_FlagCode",
                schema: "ums_configuration",
                table: "FeatureFlags",
                columns: new[] { "SystemSuiteId", "FlagCode" },
                unique: true,
                filter: "\"StatusId\" != 3");
        }

        /// <inheritdoc />
        #pragma warning disable S1186
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FeatureFlags_SystemSuiteId_FlagCode",
                schema: "ums_configuration",
                table: "FeatureFlags");

            migrationBuilder.CreateIndex(
                name: "IX_FeatureFlags_SystemSuiteId_FlagCode",
                schema: "ums_configuration",
                table: "FeatureFlags",
                columns: new[] { "SystemSuiteId", "FlagCode" },
                unique: true);
        }
#pragma warning restore S1186
    }
}
