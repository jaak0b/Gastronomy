using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GastronomyApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CollateCategoryNamesAndCapitalizeQRColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CatalogCategories_NormalizedName",
                table: "CatalogCategories");

            migrationBuilder.DropColumn(
                name: "NormalizedName",
                table: "CatalogCategories");

            migrationBuilder.RenameColumn(
                name: "QrCodeSalt",
                table: "EnrolmentInvitations",
                newName: "QRCodeSalt");

            migrationBuilder.RenameColumn(
                name: "QrCodeIterations",
                table: "EnrolmentInvitations",
                newName: "QRCodeIterations");

            migrationBuilder.RenameColumn(
                name: "QrCodeHash",
                table: "EnrolmentInvitations",
                newName: "QRCodeHash");

            migrationBuilder.RenameColumn(
                name: "QrCodeAlgorithm",
                table: "EnrolmentInvitations",
                newName: "QRCodeAlgorithm");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "CatalogCategories",
                type: "TEXT",
                nullable: false,
                collation: "NOCASE",
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogCategories_Name",
                table: "CatalogCategories",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CatalogCategories_Name",
                table: "CatalogCategories");

            migrationBuilder.RenameColumn(
                name: "QRCodeSalt",
                table: "EnrolmentInvitations",
                newName: "QrCodeSalt");

            migrationBuilder.RenameColumn(
                name: "QRCodeIterations",
                table: "EnrolmentInvitations",
                newName: "QrCodeIterations");

            migrationBuilder.RenameColumn(
                name: "QRCodeHash",
                table: "EnrolmentInvitations",
                newName: "QrCodeHash");

            migrationBuilder.RenameColumn(
                name: "QRCodeAlgorithm",
                table: "EnrolmentInvitations",
                newName: "QrCodeAlgorithm");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "CatalogCategories",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldCollation: "NOCASE");

            migrationBuilder.AddColumn<string>(
                name: "NormalizedName",
                table: "CatalogCategories",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogCategories_NormalizedName",
                table: "CatalogCategories",
                column: "NormalizedName",
                unique: true);
        }
    }
}
