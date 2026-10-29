using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GastronomyApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class KeepOnlySha256Secrets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM "Devices" WHERE "TokenAlgorithm" <> 'SHA-256';
                DELETE FROM "EnrolmentInvitations" WHERE "QRCodeAlgorithm" <> 'SHA-256';
                """);

            migrationBuilder.DropColumn(
                name: "QRCodeIterations",
                table: "EnrolmentInvitations");

            migrationBuilder.DropColumn(
                name: "TokenIterations",
                table: "Devices");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "QRCodeIterations",
                table: "EnrolmentInvitations",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TokenIterations",
                table: "Devices",
                type: "INTEGER",
                nullable: true);
        }
    }
}
