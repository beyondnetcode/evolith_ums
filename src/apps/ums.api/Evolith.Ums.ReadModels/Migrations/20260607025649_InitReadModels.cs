using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Evolith.Ums.ReadModels.Migrations
{
    /// <inheritdoc />
    public partial class InitReadModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "Authorization");

            migrationBuilder.CreateTable(
                name: "PermissionTemplateReadModels",
                schema: "Authorization",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    SystemSuiteId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<string>(type: "text", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermissionTemplateReadModels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PermissionTemplateItemReadModels",
                schema: "Authorization",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetTypeId = table.Column<int>(type: "integer", nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActionId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsAllowed = table.Column<bool>(type: "boolean", nullable: false),
                    IsDenied = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermissionTemplateItemReadModels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PermissionTemplateItemReadModels_PermissionTemplateReadMode~",
                        column: x => x.TemplateId,
                        principalSchema: "Authorization",
                        principalTable: "PermissionTemplateReadModels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PermissionTemplateItemReadModels_TemplateId",
                schema: "Authorization",
                table: "PermissionTemplateItemReadModels",
                column: "TemplateId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PermissionTemplateItemReadModels",
                schema: "Authorization");

            migrationBuilder.DropTable(
                name: "PermissionTemplateReadModels",
                schema: "Authorization");
        }
    }
}
