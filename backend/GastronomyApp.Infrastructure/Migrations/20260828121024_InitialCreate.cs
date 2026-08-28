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
                name: "CatalogItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    CategoryName = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    PriceCents = table.Column<int>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsAvailable = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Devices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    StaffMemberId = table.Column<Guid>(type: "TEXT", nullable: false),
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
                    StaffMemberId = table.Column<Guid>(type: "TEXT", nullable: true),
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
                    TableName = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Note = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Printers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    PrinterType = table.Column<string>(type: "TEXT", maxLength: 34, nullable: false),
                    Host = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Port = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Printers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PrinterStatuses",
                columns: table => new
                {
                    PrinterId = table.Column<Guid>(type: "TEXT", nullable: false),
                    IsOnline = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsPaperEnd = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsPaperNearEnd = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsCoverOpen = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsInErrorState = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsFaulty = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastDetail = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    LastChangedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastHeardFromAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrinterStatuses", x => x.PrinterId);
                });

            migrationBuilder.CreateTable(
                name: "SequenceCounters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    NextOrderNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    NextPrinterJobId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SequenceCounters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StaffMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffMembers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Stations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    PrinterId = table.Column<Guid>(type: "TEXT", nullable: true),
                    NextStationOrderNumber = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Stations_Printers_PrinterId",
                        column: x => x.PrinterId,
                        principalTable: "Printers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StationOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OrderId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StationOrderNumber = table.Column<int>(type: "INTEGER", nullable: false)
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
                    ItemName = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    UnitPriceCents = table.Column<int>(type: "INTEGER", nullable: false),
                    Note = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true)
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
                name: "PrintJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    StationOrderId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CopyNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    FailureReason = table.Column<int>(type: "INTEGER", nullable: true),
                    PrinterJobId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrintJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrintJobs_StationOrders_StationOrderId",
                        column: x => x.StationOrderId,
                        principalTable: "StationOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Devices_StaffMemberId",
                table: "Devices",
                column: "StaffMemberId",
                unique: true);

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
                name: "IX_OrderItems_StationOrderId",
                table: "OrderItems",
                column: "StationOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_ClientOrderId",
                table: "Orders",
                column: "ClientOrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PrintJobs_StationOrderId_CopyNumber",
                table: "PrintJobs",
                columns: new[] { "StationOrderId", "CopyNumber" },
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
                name: "IX_Stations_PrinterId",
                table: "Stations",
                column: "PrinterId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CatalogItems");

            migrationBuilder.DropTable(
                name: "Devices");

            migrationBuilder.DropTable(
                name: "EnrolmentInvitations");

            migrationBuilder.DropTable(
                name: "ItemStationAssignments");

            migrationBuilder.DropTable(
                name: "OrderItems");

            migrationBuilder.DropTable(
                name: "PrinterStatuses");

            migrationBuilder.DropTable(
                name: "PrintJobs");

            migrationBuilder.DropTable(
                name: "SequenceCounters");

            migrationBuilder.DropTable(
                name: "StaffMembers");

            migrationBuilder.DropTable(
                name: "StationOrders");

            migrationBuilder.DropTable(
                name: "Orders");

            migrationBuilder.DropTable(
                name: "Stations");

            migrationBuilder.DropTable(
                name: "Printers");
        }
    }
}
