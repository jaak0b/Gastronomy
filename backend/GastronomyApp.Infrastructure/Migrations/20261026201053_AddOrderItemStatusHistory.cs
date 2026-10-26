using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GastronomyApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderItemStatusHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OrderItemStatuses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OrderItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PreviousStatusId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedByStaffMemberId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedByStationId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderItemStatuses", x => x.Id);
                    table.CheckConstraint("CK_OrderItemStatuses_ExactlyOneCreator", "(\"CreatedByStaffMemberId\" IS NULL) <> (\"CreatedByStationId\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_OrderItemStatuses_OrderItemStatuses_PreviousStatusId",
                        column: x => x.PreviousStatusId,
                        principalTable: "OrderItemStatuses",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_OrderItemStatuses_OrderItems_OrderItemId",
                        column: x => x.OrderItemId,
                        principalTable: "OrderItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrderItemStatuses_StaffMembers_CreatedByStaffMemberId",
                        column: x => x.CreatedByStaffMemberId,
                        principalTable: "StaffMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderItemStatuses_Stations_CreatedByStationId",
                        column: x => x.CreatedByStationId,
                        principalTable: "Stations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrderItemStatuses_CreatedByStaffMemberId",
                table: "OrderItemStatuses",
                column: "CreatedByStaffMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItemStatuses_CreatedByStationId",
                table: "OrderItemStatuses",
                column: "CreatedByStationId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItemStatuses_OrderItemId",
                table: "OrderItemStatuses",
                column: "OrderItemId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItemStatuses_PreviousStatusId",
                table: "OrderItemStatuses",
                column: "PreviousStatusId",
                unique: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CurrentStatusId",
                table: "OrderItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.Sql(
                """
                INSERT INTO "OrderItemStatuses" ("Id", "OrderItemId", "PreviousStatusId", "Status", "CreatedAtUtc", "CreatedByStaffMemberId", "CreatedByStationId")
                SELECT hex(randomblob(4)) || '-' || hex(randomblob(2)) || '-' || hex(randomblob(2)) || '-' || hex(randomblob(2)) || '-' || hex(randomblob(6)), "OrderItems"."Id", NULL, 0, "Orders"."CreatedAtUtc", "Orders"."StaffMemberId", NULL
                FROM "OrderItems"
                JOIN "StationOrders" ON "StationOrders"."Id" = "OrderItems"."StationOrderId"
                JOIN "Orders" ON "Orders"."Id" = "StationOrders"."OrderId";

                INSERT INTO "OrderItemStatuses" ("Id", "OrderItemId", "PreviousStatusId", "Status", "CreatedAtUtc", "CreatedByStaffMemberId", "CreatedByStationId")
                SELECT hex(randomblob(4)) || '-' || hex(randomblob(2)) || '-' || hex(randomblob(2)) || '-' || hex(randomblob(2)) || '-' || hex(randomblob(6)), "OrderItems"."Id", "OrderItemStatuses"."Id", 20, "OrderItems"."FulfilledAtUtc", NULL, "StationOrders"."StationId"
                FROM "OrderItems"
                JOIN "StationOrders" ON "StationOrders"."Id" = "OrderItems"."StationOrderId"
                JOIN "OrderItemStatuses" ON "OrderItemStatuses"."OrderItemId" = "OrderItems"."Id" AND "OrderItemStatuses"."Status" = 0
                WHERE "OrderItems"."FulfilledAtUtc" IS NOT NULL;

                UPDATE "OrderItems"
                SET "CurrentStatusId" = (SELECT "Latest"."Id"
                                         FROM "OrderItemStatuses" AS "Latest"
                                         WHERE "Latest"."OrderItemId" = "OrderItems"."Id"
                                           AND NOT EXISTS (SELECT 1 FROM "OrderItemStatuses" AS "Later" WHERE "Later"."PreviousStatusId" = "Latest"."Id"));
                """);

            migrationBuilder.DropIndex(
                name: "IX_OrderItems_FulfilledAtUtc",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "FulfilledAtUtc",
                table: "OrderItems");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_CurrentStatusId",
                table: "OrderItems",
                column: "CurrentStatusId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_OrderItems_OrderItemStatuses_CurrentStatusId",
                table: "OrderItems",
                column: "CurrentStatusId",
                principalTable: "OrderItemStatuses",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrderItems_OrderItemStatuses_CurrentStatusId",
                table: "OrderItems");

            migrationBuilder.DropIndex(
                name: "IX_OrderItems_CurrentStatusId",
                table: "OrderItems");

            migrationBuilder.AddColumn<DateTime>(
                name: "FulfilledAtUtc",
                table: "OrderItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "OrderItems"
                SET "FulfilledAtUtc" = (SELECT "Current"."CreatedAtUtc"
                                        FROM "OrderItemStatuses" AS "Current"
                                        WHERE "Current"."Id" = "OrderItems"."CurrentStatusId" AND "Current"."Status" = 20);
                """);

            migrationBuilder.DropColumn(
                name: "CurrentStatusId",
                table: "OrderItems");

            migrationBuilder.DropTable(
                name: "OrderItemStatuses");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_FulfilledAtUtc",
                table: "OrderItems",
                column: "FulfilledAtUtc");
        }
    }
}
