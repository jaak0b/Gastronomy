using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GastronomyApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveEventSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EventSessions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_NumberCounters",
                table: "NumberCounters");

            migrationBuilder.DropColumn(
                name: "EventSessionId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "EventSessionId",
                table: "NumberCounters");

            migrationBuilder.AddPrimaryKey(
                name: "PK_NumberCounters",
                table: "NumberCounters",
                columns: new[] { "CounterKind", "ProductionLocationId", "PrinterEndpointKey" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_NumberCounters",
                table: "NumberCounters");

            migrationBuilder.AddColumn<Guid>(
                name: "EventSessionId",
                table: "Orders",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "EventSessionId",
                table: "NumberCounters",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddPrimaryKey(
                name: "PK_NumberCounters",
                table: "NumberCounters",
                columns: new[] { "CounterKind", "EventSessionId", "ProductionLocationId", "PrinterEndpointKey" });

            migrationBuilder.CreateTable(
                name: "EventSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EndedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsPractice = table.Column<bool>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventSessions", x => x.Id);
                });
        }
    }
}
