using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Evolith.Ums.Infrastructure.Persistence.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class AddParameterDefinitionSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAtUtc",
                schema: "ums_configuration",
                table: "ParameterDefinitions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                schema: "ums_configuration",
                table: "ParameterDefinitions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                schema: "ums_configuration",
                table: "ParameterDefinitions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_ParameterDefinitions_IsDeleted",
                schema: "ums_configuration",
                table: "ParameterDefinitions",
                column: "IsDeleted");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ParameterDefinitions_IsDeleted",
                schema: "ums_configuration",
                table: "ParameterDefinitions");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                schema: "ums_configuration",
                table: "ParameterDefinitions");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                schema: "ums_configuration",
                table: "ParameterDefinitions");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                schema: "ums_configuration",
                table: "ParameterDefinitions");
        }
    }
}
