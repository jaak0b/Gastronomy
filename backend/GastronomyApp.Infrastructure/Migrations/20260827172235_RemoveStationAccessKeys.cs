using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GastronomyApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveStationAccessKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProductionLocations_StationAccessKey",
                table: "ProductionLocations");

            migrationBuilder.DropColumn(
                name: "StationAccessKey",
                table: "ProductionLocations");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StationAccessKey",
                table: "ProductionLocations",
                type: "TEXT",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionLocations_StationAccessKey",
                table: "ProductionLocations",
                column: "StationAccessKey",
                unique: true);
        }
    }
}
