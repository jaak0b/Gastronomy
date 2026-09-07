using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GastronomyApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CatalogCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    NormalizedName = table.Column<string>(type: "TEXT", nullable: false),
                    ColourHex = table.Column<string>(type: "TEXT", maxLength: 7, nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Devices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Language = table.Column<string>(type: "TEXT", maxLength: 2, nullable: false),
                    TokenHash = table.Column<byte[]>(type: "BLOB", nullable: false),
                    TokenSalt = table.Column<byte[]>(type: "BLOB", nullable: false),
                    TokenIterations = table.Column<int>(type: "INTEGER", nullable: false),
                    TokenAlgorithm = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    TokenLookupId = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastSeenAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Devices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EnrolmentInvitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    QrCodeHash = table.Column<byte[]>(type: "BLOB", nullable: false),
                    QrCodeSalt = table.Column<byte[]>(type: "BLOB", nullable: false),
                    QrCodeIterations = table.Column<int>(type: "INTEGER", nullable: false),
                    QrCodeAlgorithm = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ConsumedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ConsumedByDeviceId = table.Column<Guid>(type: "TEXT", nullable: true),
                    IsOutstandingMarker = table.Column<int>(type: "INTEGER", nullable: true, computedColumnSql: "CASE WHEN ConsumedAtUtc IS NULL THEN 1 END", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnrolmentInvitations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ItemStationAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CatalogItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StationId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemStationAssignments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ClientOrderId = table.Column<Guid>(type: "TEXT", nullable: false),
                    GlobalOrderNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    StaffMemberId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TableName = table.Column<string>(type: "TEXT", nullable: false),
                    Note = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SequenceCounters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    NextOrderNumber = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SequenceCounters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CatalogItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    CategoryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PriceCents = table.Column<int>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsAvailable = table.Column<bool>(type: "INTEGER", nullable: false),
                    ProductionMinutes = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CatalogItems_CatalogCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "CatalogCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StaffMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: true),
                    EnrolmentInvitationId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StaffMembers_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_StaffMembers_EnrolmentInvitations_EnrolmentInvitationId",
                        column: x => x.EnrolmentInvitationId,
                        principalTable: "EnrolmentInvitations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Stations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: true),
                    EnrolmentInvitationId = table.Column<Guid>(type: "TEXT", nullable: true),
                    NextStationOrderNumber = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Stations_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Stations_EnrolmentInvitations_EnrolmentInvitationId",
                        column: x => x.EnrolmentInvitationId,
                        principalTable: "EnrolmentInvitations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "StationOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OrderId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StationOrderNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    DeliveryMode = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StationOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StationOrders_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StationOrders_Stations_StationId",
                        column: x => x.StationId,
                        principalTable: "Stations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrderItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    StationOrderId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CatalogItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ItemName = table.Column<string>(type: "TEXT", nullable: false),
                    UnitPriceCents = table.Column<int>(type: "INTEGER", nullable: false),
                    Note = table.Column<string>(type: "TEXT", nullable: true),
                    ProductionStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    SettledAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ChargedPriceCents = table.Column<int>(type: "INTEGER", nullable: true),
                    SettledByStaffMemberId = table.Column<Guid>(type: "TEXT", nullable: true),
                    PaymentNotice = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderItems_StationOrders_StationOrderId",
                        column: x => x.StationOrderId,
                        principalTable: "StationOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderItemStatusChanges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OrderItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ChangedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderItemStatusChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderItemStatusChanges_OrderItems_OrderItemId",
                        column: x => x.OrderItemId,
                        principalTable: "OrderItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CatalogCategories_NormalizedName",
                table: "CatalogCategories",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CatalogItems_CategoryId",
                table: "CatalogItems",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_TokenLookupId",
                table: "Devices",
                column: "TokenLookupId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EnrolmentInvitations_IsOutstandingMarker",
                table: "EnrolmentInvitations",
                column: "IsOutstandingMarker",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ItemStationAssignments_CatalogItemId_StationId",
                table: "ItemStationAssignments",
                columns: new[] { "CatalogItemId", "StationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_ProductionStatus",
                table: "OrderItems",
                column: "ProductionStatus");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_SettledAtUtc",
                table: "OrderItems",
                column: "SettledAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_StationOrderId",
                table: "OrderItems",
                column: "StationOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItemStatusChanges_OrderItemId",
                table: "OrderItemStatusChanges",
                column: "OrderItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_ClientOrderId",
                table: "Orders",
                column: "ClientOrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StaffMembers_DeviceId",
                table: "StaffMembers",
                column: "DeviceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StaffMembers_EnrolmentInvitationId",
                table: "StaffMembers",
                column: "EnrolmentInvitationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StationOrders_OrderId_StationId",
                table: "StationOrders",
                columns: new[] { "OrderId", "StationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StationOrders_StationId",
                table: "StationOrders",
                column: "StationId");

            migrationBuilder.CreateIndex(
                name: "IX_Stations_DeviceId",
                table: "Stations",
                column: "DeviceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Stations_EnrolmentInvitationId",
                table: "Stations",
                column: "EnrolmentInvitationId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CatalogItems");

            migrationBuilder.DropTable(
                name: "ItemStationAssignments");

            migrationBuilder.DropTable(
                name: "OrderItemStatusChanges");

            migrationBuilder.DropTable(
                name: "SequenceCounters");

            migrationBuilder.DropTable(
                name: "StaffMembers");

            migrationBuilder.DropTable(
                name: "CatalogCategories");

            migrationBuilder.DropTable(
                name: "OrderItems");

            migrationBuilder.DropTable(
                name: "StationOrders");

            migrationBuilder.DropTable(
                name: "Orders");

            migrationBuilder.DropTable(
                name: "Stations");

            migrationBuilder.DropTable(
                name: "Devices");

            migrationBuilder.DropTable(
                name: "EnrolmentInvitations");
        }
    }
}
