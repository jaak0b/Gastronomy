using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GastronomyApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEntityNavigations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Orders_StaffMemberId",
                table: "Orders",
                column: "StaffMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_CatalogItemId",
                table: "OrderItems",
                column: "CatalogItemId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_SettledByStaffMemberId",
                table: "OrderItems",
                column: "SettledByStaffMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemStationAssignments_CatalogItemId",
                table: "ItemStationAssignments",
                column: "CatalogItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemStationAssignments_StationId",
                table: "ItemStationAssignments",
                column: "StationId");

            migrationBuilder.CreateIndex(
                name: "IX_EnrolmentInvitations_ConsumedByDeviceId",
                table: "EnrolmentInvitations",
                column: "ConsumedByDeviceId");

            migrationBuilder.Sql(
                """
                DELETE FROM "EnrolmentInvitations"
                WHERE "ConsumedByDeviceId" IS NOT NULL
                  AND "ConsumedByDeviceId" NOT IN (SELECT "Id" FROM "Devices");
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_EnrolmentInvitations_Devices_ConsumedByDeviceId",
                table: "EnrolmentInvitations",
                column: "ConsumedByDeviceId",
                principalTable: "Devices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ItemStationAssignments_CatalogItems_CatalogItemId",
                table: "ItemStationAssignments",
                column: "CatalogItemId",
                principalTable: "CatalogItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ItemStationAssignments_Festivals_FestivalId",
                table: "ItemStationAssignments",
                column: "FestivalId",
                principalTable: "Festivals",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ItemStationAssignments_Stations_StationId",
                table: "ItemStationAssignments",
                column: "StationId",
                principalTable: "Stations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OrderItems_CatalogItems_CatalogItemId",
                table: "OrderItems",
                column: "CatalogItemId",
                principalTable: "CatalogItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OrderItems_StaffMembers_SettledByStaffMemberId",
                table: "OrderItems",
                column: "SettledByStaffMemberId",
                principalTable: "StaffMembers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_StaffMembers_StaffMemberId",
                table: "Orders",
                column: "StaffMemberId",
                principalTable: "StaffMembers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EnrolmentInvitations_Devices_ConsumedByDeviceId",
                table: "EnrolmentInvitations");

            migrationBuilder.DropForeignKey(
                name: "FK_ItemStationAssignments_CatalogItems_CatalogItemId",
                table: "ItemStationAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_ItemStationAssignments_Festivals_FestivalId",
                table: "ItemStationAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_ItemStationAssignments_Stations_StationId",
                table: "ItemStationAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_OrderItems_CatalogItems_CatalogItemId",
                table: "OrderItems");

            migrationBuilder.DropForeignKey(
                name: "FK_OrderItems_StaffMembers_SettledByStaffMemberId",
                table: "OrderItems");

            migrationBuilder.DropForeignKey(
                name: "FK_Orders_StaffMembers_StaffMemberId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_StaffMemberId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_OrderItems_CatalogItemId",
                table: "OrderItems");

            migrationBuilder.DropIndex(
                name: "IX_OrderItems_SettledByStaffMemberId",
                table: "OrderItems");

            migrationBuilder.DropIndex(
                name: "IX_ItemStationAssignments_CatalogItemId",
                table: "ItemStationAssignments");

            migrationBuilder.DropIndex(
                name: "IX_ItemStationAssignments_StationId",
                table: "ItemStationAssignments");

            migrationBuilder.DropIndex(
                name: "IX_EnrolmentInvitations_ConsumedByDeviceId",
                table: "EnrolmentInvitations");
        }
    }
}
