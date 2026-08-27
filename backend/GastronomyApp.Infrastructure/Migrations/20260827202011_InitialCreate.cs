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
                    LastSeenAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RevokedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UserAgentSnapshot = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false)
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
                    SixDigitHash = table.Column<byte[]>(type: "BLOB", nullable: false),
                    SixDigitSalt = table.Column<byte[]>(type: "BLOB", nullable: false),
                    CodeIterations = table.Column<int>(type: "INTEGER", nullable: false),
                    CodeAlgorithm = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    FailedSixDigitAttempts = table.Column<int>(type: "INTEGER", nullable: false),
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
                name: "NumberCounters",
                columns: table => new
                {
                    CounterKind = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    StationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PrinterEndpointKey = table.Column<string>(type: "TEXT", maxLength: 96, nullable: false),
                    NextValue = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NumberCounters", x => new { x.CounterKind, x.StationId, x.PrinterEndpointKey });
                });

            migrationBuilder.CreateTable(
                name: "Orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ClientOrderId = table.Column<Guid>(type: "TEXT", nullable: false),
                    GlobalOrderNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    StaffMemberId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TableLabel = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Note = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    TotalCents = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PrintAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PrintJobId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AttemptNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    Outcome = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Phase = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    BytesWritten = table.Column<int>(type: "INTEGER", nullable: false),
                    TransportDetail = table.Column<string>(type: "TEXT", maxLength: 400, nullable: false),
                    PrinterStatusSnapshotJson = table.Column<string>(type: "TEXT", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrintAttempts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PrinterConfigurations",
                columns: table => new
                {
                    StationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TransportKind = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Host = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Port = table.Column<int>(type: "INTEGER", nullable: false),
                    AgentIdentifier = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    CharactersPerLine = table.Column<int>(type: "INTEGER", nullable: false),
                    CodePageName = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ConnectTimeoutSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    JobTimeoutSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    HeartbeatSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrinterConfigurations", x => x.StationId);
                });

            migrationBuilder.CreateTable(
                name: "PrinterStatuses",
                columns: table => new
                {
                    StationId = table.Column<Guid>(type: "TEXT", nullable: false),
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
                    table.PrimaryKey("PK_PrinterStatuses", x => x.StationId);
                });

            migrationBuilder.CreateTable(
                name: "PrintJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    LocationTicketId = table.Column<Guid>(type: "TEXT", nullable: true),
                    StationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ProcessId = table.Column<int>(type: "INTEGER", nullable: true),
                    FailureReason = table.Column<string>(type: "TEXT", maxLength: 24, nullable: true),
                    RequestedByDeviceId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrintJobs", x => x.Id);
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
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TableSuggestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Label = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TableSuggestions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LocationTickets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OrderId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StationSequenceNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 24, nullable: false),
                    ReprintCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ResolvedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ResolutionNote = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocationTickets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LocationTickets_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OrderId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LocationTicketId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CatalogItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ChosenStationId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ItemNameSnapshot = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    UnitPriceCentsSnapshot = table.Column<int>(type: "INTEGER", nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    Note = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderLines_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

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
                name: "IX_LocationTickets_OrderId_StationId",
                table: "LocationTickets",
                columns: new[] { "OrderId", "StationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderLines_OrderId",
                table: "OrderLines",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_ClientOrderId",
                table: "Orders",
                column: "ClientOrderId",
                unique: true);
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
                name: "LocationTickets");

            migrationBuilder.DropTable(
                name: "NumberCounters");

            migrationBuilder.DropTable(
                name: "OrderLines");

            migrationBuilder.DropTable(
                name: "PrintAttempts");

            migrationBuilder.DropTable(
                name: "PrinterConfigurations");

            migrationBuilder.DropTable(
                name: "PrinterStatuses");

            migrationBuilder.DropTable(
                name: "PrintJobs");

            migrationBuilder.DropTable(
                name: "StaffMembers");

            migrationBuilder.DropTable(
                name: "Stations");

            migrationBuilder.DropTable(
                name: "TableSuggestions");

            migrationBuilder.DropTable(
                name: "Orders");
        }
    }
}
