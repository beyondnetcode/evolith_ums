using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Evolith.Ums.Infrastructure.Persistence.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class AddSystemSuiteNodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SystemSuiteNodes",
                schema: "ums_authorization",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ModuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentNodeId = table.Column<Guid>(type: "uuid", nullable: true),
                    NodeKindId = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    StatusId = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Responsable = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Criticidad = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ProductoImpactado = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ComponenteTecnico = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Dependencias = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Evidencias = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    TrazabilidadSdlc = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemSuiteNodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SystemSuiteNodes_SystemSuiteModules_ModuleId",
                        column: x => x.ModuleId,
                        principalSchema: "ums_authorization",
                        principalTable: "SystemSuiteModules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SystemSuiteNodes_SystemSuiteNodes_ParentNodeId",
                        column: x => x.ParentNodeId,
                        principalSchema: "ums_authorization",
                        principalTable: "SystemSuiteNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SystemSuiteNodeActions",
                schema: "ums_authorization",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActionCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemSuiteNodeActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SystemSuiteNodeActions_SystemSuiteNodes_NodeId",
                        column: x => x.NodeId,
                        principalSchema: "ums_authorization",
                        principalTable: "SystemSuiteNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SystemSuiteNodeActions_NodeId_ActionCode",
                schema: "ums_authorization",
                table: "SystemSuiteNodeActions",
                columns: new[] { "NodeId", "ActionCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SystemSuiteNodes_ModuleId_ParentNodeId_Code",
                schema: "ums_authorization",
                table: "SystemSuiteNodes",
                columns: new[] { "ModuleId", "ParentNodeId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SystemSuiteNodes_ParentNodeId",
                schema: "ums_authorization",
                table: "SystemSuiteNodes",
                column: "ParentNodeId");
        }

        /// <inheritdoc />
        #pragma warning disable S1186
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SystemSuiteNodeActions",
                schema: "ums_authorization");

            migrationBuilder.DropTable(
                name: "SystemSuiteNodes",
                schema: "ums_authorization");
        }
#pragma warning restore S1186
    }
}
