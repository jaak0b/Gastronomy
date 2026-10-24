using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GastronomyApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SwitchOrdersToTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_Tables_Id_FestivalId",
                table: "Tables",
                columns: new[] { "Id", "FestivalId" });

            migrationBuilder.AddColumn<Guid>(
                name: "TableId",
                table: "Orders",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // EF fills the rebuilt Orders while the old Tables is still in place, and SQLite refuses the new foreign key unless that old table has a unique index on its target columns; dropping the old table drops the index.
            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX "IX_Tables_Id_FestivalId_UntilRebuilt" ON "Tables" ("Id", "FestivalId");
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO "Areas" ("Id", "FestivalId", "Name", "SortOrder", "IsHidden")
                SELECT hex(randomblob(4)) || '-' || hex(randomblob(2)) || '-' || hex(randomblob(2)) || '-' || hex(randomblob(2)) || '-' || hex(randomblob(6)),
                       "Festivals"."Id", 'Bisherige Tische', 0, 1
                FROM "Festivals"
                WHERE EXISTS (SELECT 1 FROM "Orders" WHERE "Orders"."FestivalId" = "Festivals"."Id");

                INSERT INTO "Tables" ("Id", "Name", "IsHidden", "FestivalId", "AreaId", "X", "Y", "Width", "Height", "Rotation")
                SELECT hex(randomblob(4)) || '-' || hex(randomblob(2)) || '-' || hex(randomblob(2)) || '-' || hex(randomblob(2)) || '-' || hex(randomblob(6)),
                       "Orders"."TableName", 1, "Orders"."FestivalId", "Areas"."Id", 0, 0, 100, 100, 0
                FROM "Orders"
                JOIN "Areas" ON "Areas"."FestivalId" = "Orders"."FestivalId" AND "Areas"."Name" = 'Bisherige Tische'
                WHERE NOT EXISTS (
                    SELECT 1 FROM "Orders" AS "EarlierOrder"
                    WHERE "EarlierOrder"."FestivalId" = "Orders"."FestivalId"
                      AND "EarlierOrder"."TableName" = "Orders"."TableName" COLLATE NOCASE
                      AND ("EarlierOrder"."CreatedAtUtc" < "Orders"."CreatedAtUtc"
                           OR ("EarlierOrder"."CreatedAtUtc" = "Orders"."CreatedAtUtc" AND "EarlierOrder"."GlobalOrderNumber" < "Orders"."GlobalOrderNumber")));

                UPDATE "Orders"
                SET "TableId" = (
                    SELECT "Tables"."Id" FROM "Tables"
                    WHERE "Tables"."FestivalId" = "Orders"."FestivalId"
                      AND "Tables"."Name" = "Orders"."TableName" COLLATE NOCASE);
                """);

            migrationBuilder.DropColumn(
                name: "TableName",
                table: "Orders");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_TableId_FestivalId",
                table: "Orders",
                columns: new[] { "TableId", "FestivalId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_Tables_TableId_FestivalId",
                table: "Orders",
                columns: new[] { "TableId", "FestivalId" },
                principalTable: "Tables",
                principalColumns: new[] { "Id", "FestivalId" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_Tables_TableId_FestivalId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_TableId_FestivalId",
                table: "Orders");

            migrationBuilder.AddColumn<string>(
                name: "TableName",
                table: "Orders",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                """
                UPDATE "Orders"
                SET "TableName" = (SELECT "Tables"."Name" FROM "Tables" WHERE "Tables"."Id" = "Orders"."TableId");
                """);

            migrationBuilder.DropColumn(
                name: "TableId",
                table: "Orders");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Tables_Id_FestivalId",
                table: "Tables");
        }
    }
}
