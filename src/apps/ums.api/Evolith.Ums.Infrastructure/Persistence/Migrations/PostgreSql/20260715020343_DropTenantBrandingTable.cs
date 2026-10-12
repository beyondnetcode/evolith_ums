using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Evolith.Ums.Infrastructure.Persistence.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class DropTenantBrandingTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenantBrandings",
                schema: "ums_identity");
        }

        /// <inheritdoc />
        #pragma warning disable S1186
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TenantBrandings",
                schema: "ums_identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuditTimeSpan = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    BackgroundStyleId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CustomDomain = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    DnsCnameTarget = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    DnsVerificationStatusId = table.Column<int>(type: "integer", nullable: false),
                    FooterText = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    HeadlineText = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Logo = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    LogoFormatId = table.Column<int>(type: "integer", nullable: false),
                    MagicLinkFallbackEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    PrimaryButtonLabel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PrimaryColor = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SecondaryText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantBrandings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantBrandings_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "ums_identity",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TenantBrandings_CustomDomain",
                schema: "ums_identity",
                table: "TenantBrandings",
                column: "CustomDomain",
                unique: true,
                filter: "\"CustomDomain\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TenantBrandings_TenantId",
                schema: "ums_identity",
                table: "TenantBrandings",
                column: "TenantId",
                unique: true);
        }
#pragma warning restore S1186
    }
}
