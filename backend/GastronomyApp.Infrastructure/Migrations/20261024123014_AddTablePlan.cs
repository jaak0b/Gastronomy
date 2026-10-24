using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GastronomyApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTablePlan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_FestivalStations_Id_FestivalId",
                table: "FestivalStations",
                columns: new[] { "Id", "FestivalId" });

            migrationBuilder.CreateTable(
                name: "Areas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    FestivalId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    IsHidden = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Areas", x => x.Id);
                    table.UniqueConstraint("AK_Areas_Id_FestivalId", x => new { x.Id, x.FestivalId });
                    table.ForeignKey(
                        name: "FK_Areas_Festivals_FestivalId",
                        column: x => x.FestivalId,
                        principalTable: "Festivals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FestivalStationShapes",
                columns: table => new
                {
                    FestivalStationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FestivalId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AreaId = table.Column<Guid>(type: "TEXT", nullable: false),
                    X = table.Column<double>(type: "REAL", nullable: false),
                    Y = table.Column<double>(type: "REAL", nullable: false),
                    Width = table.Column<double>(type: "REAL", nullable: false),
                    Height = table.Column<double>(type: "REAL", nullable: false),
                    Rotation = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FestivalStationShapes", x => x.FestivalStationId);
                    table.ForeignKey(
                        name: "FK_FestivalStationShapes_Areas_AreaId_FestivalId",
                        columns: x => new { x.AreaId, x.FestivalId },
                        principalTable: "Areas",
                        principalColumns: new[] { "Id", "FestivalId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FestivalStationShapes_FestivalStations_FestivalStationId_FestivalId",
                        columns: x => new { x.FestivalStationId, x.FestivalId },
                        principalTable: "FestivalStations",
                        principalColumns: new[] { "Id", "FestivalId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FestivalStationShapes_Festivals_FestivalId",
                        column: x => x.FestivalId,
                        principalTable: "Festivals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Tables",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    IsHidden = table.Column<bool>(type: "INTEGER", nullable: false),
                    FestivalId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AreaId = table.Column<Guid>(type: "TEXT", nullable: false),
                    X = table.Column<double>(type: "REAL", nullable: false),
                    Y = table.Column<double>(type: "REAL", nullable: false),
                    Width = table.Column<double>(type: "REAL", nullable: false),
                    Height = table.Column<double>(type: "REAL", nullable: false),
                    Rotation = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tables_Areas_AreaId_FestivalId",
                        columns: x => new { x.AreaId, x.FestivalId },
                        principalTable: "Areas",
                        principalColumns: new[] { "Id", "FestivalId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tables_Festivals_FestivalId",
                        column: x => x.FestivalId,
                        principalTable: "Festivals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Areas_FestivalId_Name",
                table: "Areas",
                columns: new[] { "FestivalId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FestivalStationShapes_AreaId_FestivalId",
                table: "FestivalStationShapes",
                columns: new[] { "AreaId", "FestivalId" });

            migrationBuilder.CreateIndex(
                name: "IX_FestivalStationShapes_FestivalId",
                table: "FestivalStationShapes",
                column: "FestivalId");

            migrationBuilder.CreateIndex(
                name: "IX_FestivalStationShapes_FestivalStationId_FestivalId",
                table: "FestivalStationShapes",
                columns: new[] { "FestivalStationId", "FestivalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tables_AreaId_FestivalId",
                table: "Tables",
                columns: new[] { "AreaId", "FestivalId" });

            migrationBuilder.CreateIndex(
                name: "IX_Tables_FestivalId_Name",
                table: "Tables",
                columns: new[] { "FestivalId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FestivalStationShapes");

            migrationBuilder.DropTable(
                name: "Tables");

            migrationBuilder.DropTable(
                name: "Areas");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_FestivalStations_Id_FestivalId",
                table: "FestivalStations");
        }
    }
}
