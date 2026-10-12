using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Evolith.Ums.Infrastructure.Migrations.SqlServer.Plataforma
{
    /// <inheritdoc />
    public partial class RecordarSesionYSistemaEnRefreshToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SessionId",
                schema: "ums_identity",
                table: "RefreshTokens",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SystemCode",
                schema: "ums_identity",
                table: "RefreshTokens",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SessionId",
                schema: "ums_identity",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "SystemCode",
                schema: "ums_identity",
                table: "RefreshTokens");
        }
    }
}
