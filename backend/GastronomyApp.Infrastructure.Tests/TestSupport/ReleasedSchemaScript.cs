namespace GastronomyApp.Infrastructure.Tests.TestSupport;

public sealed class ReleasedSchemaScript
{
  public string MigrationId => "20260915222426_InitialCreate";

  public string Sql => """
                       CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                           "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
                           "ProductVersion" TEXT NOT NULL
                       );

                       CREATE TABLE "CatalogCategories" (
                           "Id" TEXT NOT NULL CONSTRAINT "PK_CatalogCategories" PRIMARY KEY,
                           "Name" TEXT NOT NULL,
                           "NormalizedName" TEXT NOT NULL,
                           "ColourHex" TEXT NOT NULL,
                           "SortOrder" INTEGER NOT NULL,
                           "IsActive" INTEGER NOT NULL
                       );

                       CREATE TABLE "Devices" (
                           "Id" TEXT NOT NULL CONSTRAINT "PK_Devices" PRIMARY KEY,
                           "Language" TEXT NOT NULL,
                           "TokenHash" BLOB NOT NULL,
                           "TokenSalt" BLOB NOT NULL,
                           "TokenIterations" INTEGER NOT NULL,
                           "TokenAlgorithm" TEXT NOT NULL,
                           "TokenLookupId" TEXT NOT NULL,
                           "CreatedAtUtc" TEXT NOT NULL,
                           "LastSeenAtUtc" TEXT NOT NULL
                       );

                       CREATE TABLE "EnrolmentInvitations" (
                           "Id" TEXT NOT NULL CONSTRAINT "PK_EnrolmentInvitations" PRIMARY KEY,
                           "QrCodeHash" BLOB NOT NULL,
                           "QrCodeSalt" BLOB NOT NULL,
                           "QrCodeIterations" INTEGER NOT NULL,
                           "QrCodeAlgorithm" TEXT NOT NULL,
                           "CreatedAtUtc" TEXT NOT NULL,
                           "ExpiresAtUtc" TEXT NOT NULL,
                           "ConsumedAtUtc" TEXT NULL,
                           "ConsumedByDeviceId" TEXT NULL,
                           "IsOutstandingMarker" AS (CASE WHEN ConsumedAtUtc IS NULL THEN 1 END) STORED
                       );

                       CREATE TABLE "Festivals" (
                           "Id" TEXT NOT NULL CONSTRAINT "PK_Festivals" PRIMARY KEY,
                           "Name" TEXT NOT NULL,
                           "StartsAtUtc" TEXT NOT NULL,
                           "EndsAtUtc" TEXT NOT NULL,
                           "NextOrderNumber" INTEGER NOT NULL,
                           "IsHidden" INTEGER NOT NULL
                       );

                       CREATE TABLE "ItemStationAssignments" (
                           "Id" TEXT NOT NULL CONSTRAINT "PK_ItemStationAssignments" PRIMARY KEY,
                           "FestivalId" TEXT NOT NULL,
                           "CatalogItemId" TEXT NOT NULL,
                           "StationId" TEXT NOT NULL
                       );

                       CREATE TABLE "CatalogItems" (
                           "Id" TEXT NOT NULL CONSTRAINT "PK_CatalogItems" PRIMARY KEY,
                           "Name" TEXT NOT NULL,
                           "CategoryId" TEXT NOT NULL,
                           "SortOrder" INTEGER NOT NULL,
                           "IsActive" INTEGER NOT NULL,
                           "ProductionMinutes" REAL NULL,
                           "IsQueueIndependent" INTEGER NOT NULL,
                           CONSTRAINT "FK_CatalogItems_CatalogCategories_CategoryId" FOREIGN KEY ("CategoryId") REFERENCES "CatalogCategories" ("Id") ON DELETE RESTRICT
                       );

                       CREATE TABLE "StaffMembers" (
                           "Id" TEXT NOT NULL CONSTRAINT "PK_StaffMembers" PRIMARY KEY,
                           "Name" TEXT NOT NULL,
                           "IsActive" INTEGER NOT NULL,
                           "DeviceId" TEXT NULL,
                           "EnrolmentInvitationId" TEXT NULL,
                           "CreatedAtUtc" TEXT NOT NULL,
                           CONSTRAINT "FK_StaffMembers_Devices_DeviceId" FOREIGN KEY ("DeviceId") REFERENCES "Devices" ("Id") ON DELETE SET NULL,
                           CONSTRAINT "FK_StaffMembers_EnrolmentInvitations_EnrolmentInvitationId" FOREIGN KEY ("EnrolmentInvitationId") REFERENCES "EnrolmentInvitations" ("Id") ON DELETE SET NULL
                       );

                       CREATE TABLE "Stations" (
                           "Id" TEXT NOT NULL CONSTRAINT "PK_Stations" PRIMARY KEY,
                           "Name" TEXT NOT NULL,
                           "SortOrder" INTEGER NOT NULL,
                           "IsActive" INTEGER NOT NULL,
                           "DeviceId" TEXT NULL,
                           "EnrolmentInvitationId" TEXT NULL,
                           CONSTRAINT "FK_Stations_Devices_DeviceId" FOREIGN KEY ("DeviceId") REFERENCES "Devices" ("Id") ON DELETE SET NULL,
                           CONSTRAINT "FK_Stations_EnrolmentInvitations_EnrolmentInvitationId" FOREIGN KEY ("EnrolmentInvitationId") REFERENCES "EnrolmentInvitations" ("Id") ON DELETE SET NULL
                       );

                       CREATE TABLE "Orders" (
                           "Id" TEXT NOT NULL CONSTRAINT "PK_Orders" PRIMARY KEY,
                           "ClientOrderId" TEXT NOT NULL,
                           "FestivalId" TEXT NOT NULL,
                           "GlobalOrderNumber" INTEGER NOT NULL,
                           "StaffMemberId" TEXT NOT NULL,
                           "TableName" TEXT NOT NULL,
                           "Note" TEXT NULL,
                           "CreatedAtUtc" TEXT NOT NULL,
                           CONSTRAINT "FK_Orders_Festivals_FestivalId" FOREIGN KEY ("FestivalId") REFERENCES "Festivals" ("Id") ON DELETE RESTRICT
                       );

                       CREATE TABLE "FestivalCatalogItems" (
                           "Id" TEXT NOT NULL CONSTRAINT "PK_FestivalCatalogItems" PRIMARY KEY,
                           "FestivalId" TEXT NOT NULL,
                           "CatalogItemId" TEXT NOT NULL,
                           "PriceCents" INTEGER NOT NULL,
                           "IsAvailable" INTEGER NOT NULL,
                           CONSTRAINT "FK_FestivalCatalogItems_CatalogItems_CatalogItemId" FOREIGN KEY ("CatalogItemId") REFERENCES "CatalogItems" ("Id") ON DELETE RESTRICT,
                           CONSTRAINT "FK_FestivalCatalogItems_Festivals_FestivalId" FOREIGN KEY ("FestivalId") REFERENCES "Festivals" ("Id") ON DELETE RESTRICT
                       );

                       CREATE TABLE "FestivalStations" (
                           "Id" TEXT NOT NULL CONSTRAINT "PK_FestivalStations" PRIMARY KEY,
                           "FestivalId" TEXT NOT NULL,
                           "StationId" TEXT NOT NULL,
                           "NextStationOrderNumber" INTEGER NOT NULL,
                           CONSTRAINT "FK_FestivalStations_Festivals_FestivalId" FOREIGN KEY ("FestivalId") REFERENCES "Festivals" ("Id") ON DELETE RESTRICT,
                           CONSTRAINT "FK_FestivalStations_Stations_StationId" FOREIGN KEY ("StationId") REFERENCES "Stations" ("Id") ON DELETE RESTRICT
                       );

                       CREATE TABLE "StationOrders" (
                           "Id" TEXT NOT NULL CONSTRAINT "PK_StationOrders" PRIMARY KEY,
                           "OrderId" TEXT NOT NULL,
                           "FestivalId" TEXT NOT NULL,
                           "StationId" TEXT NOT NULL,
                           "StationOrderNumber" INTEGER NOT NULL,
                           "DeliveryMode" INTEGER NOT NULL,
                           "IsHiddenFromAsItComesQueue" INTEGER NOT NULL,
                           CONSTRAINT "FK_StationOrders_Festivals_FestivalId" FOREIGN KEY ("FestivalId") REFERENCES "Festivals" ("Id") ON DELETE RESTRICT,
                           CONSTRAINT "FK_StationOrders_Orders_OrderId" FOREIGN KEY ("OrderId") REFERENCES "Orders" ("Id") ON DELETE CASCADE,
                           CONSTRAINT "FK_StationOrders_Stations_StationId" FOREIGN KEY ("StationId") REFERENCES "Stations" ("Id") ON DELETE RESTRICT
                       );

                       CREATE TABLE "OrderItems" (
                           "Id" TEXT NOT NULL CONSTRAINT "PK_OrderItems" PRIMARY KEY,
                           "StationOrderId" TEXT NOT NULL,
                           "CatalogItemId" TEXT NOT NULL,
                           "ItemName" TEXT NOT NULL,
                           "UnitPriceCents" INTEGER NOT NULL,
                           "Note" TEXT NULL,
                           "FulfilledAtUtc" TEXT NULL,
                           "SettledAtUtc" TEXT NULL,
                           "ChargedPriceCents" INTEGER NULL,
                           "SettledByStaffMemberId" TEXT NULL,
                           "PaymentNotice" TEXT NULL,
                           CONSTRAINT "FK_OrderItems_StationOrders_StationOrderId" FOREIGN KEY ("StationOrderId") REFERENCES "StationOrders" ("Id") ON DELETE CASCADE
                       );

                       CREATE UNIQUE INDEX "IX_CatalogCategories_NormalizedName" ON "CatalogCategories" ("NormalizedName");

                       CREATE INDEX "IX_CatalogItems_CategoryId" ON "CatalogItems" ("CategoryId");

                       CREATE UNIQUE INDEX "IX_Devices_TokenLookupId" ON "Devices" ("TokenLookupId");

                       CREATE UNIQUE INDEX "IX_EnrolmentInvitations_IsOutstandingMarker" ON "EnrolmentInvitations" ("IsOutstandingMarker");

                       CREATE INDEX "IX_FestivalCatalogItems_CatalogItemId" ON "FestivalCatalogItems" ("CatalogItemId");

                       CREATE UNIQUE INDEX "IX_FestivalCatalogItems_FestivalId_CatalogItemId" ON "FestivalCatalogItems" ("FestivalId", "CatalogItemId");

                       CREATE UNIQUE INDEX "IX_FestivalStations_FestivalId_StationId" ON "FestivalStations" ("FestivalId", "StationId");

                       CREATE INDEX "IX_FestivalStations_StationId" ON "FestivalStations" ("StationId");

                       CREATE UNIQUE INDEX "IX_ItemStationAssignments_FestivalId_CatalogItemId_StationId" ON "ItemStationAssignments" ("FestivalId", "CatalogItemId", "StationId");

                       CREATE INDEX "IX_OrderItems_FulfilledAtUtc" ON "OrderItems" ("FulfilledAtUtc");

                       CREATE INDEX "IX_OrderItems_SettledAtUtc" ON "OrderItems" ("SettledAtUtc");

                       CREATE INDEX "IX_OrderItems_StationOrderId" ON "OrderItems" ("StationOrderId");

                       CREATE UNIQUE INDEX "IX_Orders_ClientOrderId" ON "Orders" ("ClientOrderId");

                       CREATE INDEX "IX_Orders_FestivalId" ON "Orders" ("FestivalId");

                       CREATE UNIQUE INDEX "IX_Orders_FestivalId_GlobalOrderNumber" ON "Orders" ("FestivalId", "GlobalOrderNumber");

                       CREATE UNIQUE INDEX "IX_StaffMembers_DeviceId" ON "StaffMembers" ("DeviceId");

                       CREATE UNIQUE INDEX "IX_StaffMembers_EnrolmentInvitationId" ON "StaffMembers" ("EnrolmentInvitationId");

                       CREATE UNIQUE INDEX "IX_StationOrders_FestivalId_StationId_StationOrderNumber" ON "StationOrders" ("FestivalId", "StationId", "StationOrderNumber");

                       CREATE UNIQUE INDEX "IX_StationOrders_OrderId_StationId" ON "StationOrders" ("OrderId", "StationId");

                       CREATE INDEX "IX_StationOrders_StationId" ON "StationOrders" ("StationId");

                       CREATE UNIQUE INDEX "IX_Stations_DeviceId" ON "Stations" ("DeviceId");

                       CREATE UNIQUE INDEX "IX_Stations_EnrolmentInvitationId" ON "Stations" ("EnrolmentInvitationId");

                       INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                       VALUES ('20260915222426_InitialCreate', '9.0.19');
                       """;
}
