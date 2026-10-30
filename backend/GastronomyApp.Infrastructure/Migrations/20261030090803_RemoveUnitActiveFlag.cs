using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GastronomyApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUnitActiveFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Units");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Units",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }
    }
}
