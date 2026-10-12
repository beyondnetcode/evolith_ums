using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Evolith.Ums.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIgaTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "iga");

            migrationBuilder.CreateTable(
                name: "RoleMaturityStatuses",
                schema: "iga",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrentMaturityLevel = table.Column<int>(type: "integer", nullable: false),
                    NextEligibleMaturityLevel = table.Column<int>(type: "integer", nullable: true),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CurrentLevelSince = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EligibleForPromotionAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedCertificationsCount = table.Column<int>(type: "integer", nullable: false),
                    CompletedTrainingsCount = table.Column<int>(type: "integer", nullable: false),
                    PerformanceScore = table.Column<decimal>(type: "numeric(4,2)", nullable: false),
                    HasNoComplianceIssues = table.Column<bool>(type: "boolean", nullable: false),
                    BlockingFactor = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LastReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleMaturityStatuses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RolePromotionRequests",
                schema: "iga",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequesterId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrentRoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetRoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    StatusId = table.Column<int>(type: "integer", nullable: false),
                    RiskScore = table.Column<int>(type: "integer", nullable: true),
                    ApproverId = table.Column<Guid>(type: "uuid", nullable: true),
                    SecurityReviewerId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExecutorId = table.Column<Guid>(type: "uuid", nullable: true),
                    VerifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    DecisionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AuditTimeSpan = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePromotionRequests", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RoleMaturityStatuses_TenantId",
                schema: "iga",
                table: "RoleMaturityStatuses",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_RoleMaturityStatuses_TenantId_UserId",
                schema: "iga",
                table: "RoleMaturityStatuses",
                columns: new[] { "TenantId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_RoleMaturityStatuses_TenantId_UserId_RoleId",
                schema: "iga",
                table: "RoleMaturityStatuses",
                columns: new[] { "TenantId", "UserId", "RoleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RolePromotionRequests_TargetUserId",
                schema: "iga",
                table: "RolePromotionRequests",
                column: "TargetUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RolePromotionRequests_TenantId",
                schema: "iga",
                table: "RolePromotionRequests",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_RolePromotionRequests_TenantId_StatusId",
                schema: "iga",
                table: "RolePromotionRequests",
                columns: new[] { "TenantId", "StatusId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RoleMaturityStatuses",
                schema: "iga");

            migrationBuilder.DropTable(
                name: "RolePromotionRequests",
                schema: "iga");
        }
    }
}
