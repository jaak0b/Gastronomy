using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GastronomyApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUnits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Units",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Units", x => x.Id);
                });

            migrationBuilder.Sql(
                """
                INSERT INTO "Units" ("Id", "Name", "IsActive")
                VALUES
                  ('2B7C1A10-0000-4000-8000-000000000001', 'Stück', 1),
                  ('2B7C1A10-0000-4000-8000-000000000002', 'g', 1),
                  ('2B7C1A10-0000-4000-8000-000000000003', 'kg', 1),
                  ('2B7C1A10-0000-4000-8000-000000000004', 'ml', 1),
                  ('2B7C1A10-0000-4000-8000-000000000005', 'l', 1);
                """);

            migrationBuilder.AddColumn<Guid>(
                name: "UnitId",
                table: "Ingredients",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.Sql(
                """
                UPDATE "Ingredients"
                SET "UnitId" = CASE "Unit"
                  WHEN 1 THEN '2B7C1A10-0000-4000-8000-000000000001'
                  WHEN 2 THEN '2B7C1A10-0000-4000-8000-000000000002'
                  WHEN 3 THEN '2B7C1A10-0000-4000-8000-000000000004'
                END;
                """);

            migrationBuilder.DropColumn(
                name: "Unit",
                table: "Ingredients");

            migrationBuilder.AddForeignKey(
                name: "FK_Ingredients_Units_UnitId",
                table: "Ingredients",
                column: "UnitId",
                principalTable: "Units",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.CreateIndex(
                name: "IX_Ingredients_UnitId",
                table: "Ingredients",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_Units_Name",
                table: "Units",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE "Ingredients" ADD "Unit" INTEGER NOT NULL DEFAULT 1;
                """);

            migrationBuilder.Sql(
                """
                UPDATE "Ingredients"
                SET "Unit" = CASE
                  WHEN "UnitId" IN ('2B7C1A10-0000-4000-8000-000000000002', '2B7C1A10-0000-4000-8000-000000000003') THEN 2
                  WHEN "UnitId" IN ('2B7C1A10-0000-4000-8000-000000000004', '2B7C1A10-0000-4000-8000-000000000005') THEN 3
                  ELSE 1
                END;
                """);

            migrationBuilder.Sql("PRAGMA foreign_keys = 0;", suppressTransaction: true);

            migrationBuilder.Sql(
                """
                CREATE TABLE "ef_temp_Ingredients" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_Ingredients" PRIMARY KEY,
                    "IsActive" INTEGER NOT NULL,
                    "Name" TEXT COLLATE NOCASE NOT NULL,
                    "Unit" INTEGER NOT NULL
                );

                INSERT INTO "ef_temp_Ingredients" ("Id", "IsActive", "Name", "Unit")
                SELECT "Id", "IsActive", "Name", "Unit"
                FROM "Ingredients";

                DROP TABLE "Ingredients";

                ALTER TABLE "ef_temp_Ingredients" RENAME TO "Ingredients";

                CREATE UNIQUE INDEX "IX_Ingredients_Name" ON "Ingredients" ("Name");

                DROP TABLE "Units";
                """);

            migrationBuilder.Sql("PRAGMA foreign_keys = 1;", suppressTransaction: true);
        }
    }
}
