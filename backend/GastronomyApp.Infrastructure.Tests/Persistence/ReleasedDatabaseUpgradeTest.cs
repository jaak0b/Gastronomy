using System.Diagnostics;
using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace GastronomyApp.Infrastructure.Tests.Persistence;

public sealed class ReleasedDatabaseUpgradeTest
{
  private readonly ReleasedSecretHash _released = new();
  private readonly ReleasedSchemaScript _releasedSchema = new();

  [Test]
  public async Task MigrateAsync_OnReleasedDatabaseWithData_KeepsRowsAndAppendsOneMigration()
  {
    using SqliteTempFileFixture fixture = new(TemporaryDatabaseSchema.None);
    var categoryId = Guid.NewGuid();
    var festivalId = Guid.NewGuid();
    var invitationId = Guid.NewGuid();
    var staffMemberId = Guid.NewGuid();
    var orderId = Guid.NewGuid();
    var orderItemId = Guid.NewGuid();
    byte[] qrCodeHash =
    [
      1,
      2,
      3,
      4
    ];
    byte[] qrCodeSalt =
    [
      5,
      6,
      7,
      8
    ];

    var connection = fixture.OpenConnection();
    await ExecuteAsync(connection, _releasedSchema.Sql);
    await InsertCatalogCategoryAsync(connection, categoryId);
    await InsertEnrolmentInvitationAsync(connection, invitationId, qrCodeHash, qrCodeSalt);
    await InsertFestivalAsync(connection, festivalId);
    await InsertStaffMemberAsync(connection, staffMemberId);
    await InsertOrderAsync(connection, orderId, festivalId, staffMemberId);
    await InsertSettledOrderItemAsync(connection, orderItemId, orderId, festivalId, categoryId, staffMemberId);

    var context = fixture.CreateContext();
    await context.Database.MigrateAsync();

    var category = await context.Set<CatalogCategory>().SingleAsync(persisted => persisted.Id == categoryId);
    var invitation = await context.Set<EnrolmentInvitation>().SingleAsync(persisted => persisted.Id == invitationId);
    var order = await context.Set<Order>().Include(persisted => persisted.Table).ThenInclude(table => table.Area).SingleAsync(persisted => persisted.Id == orderId);
    var orderItem = await context.Set<OrderItem>().SingleAsync(persisted => persisted.Id == orderItemId);
    List<string> appliedMigrations = (await context.Database.GetAppliedMigrationsAsync()).ToList();
    var ingredientCount = await context.Set<Ingredient>().CountAsync();
    var stockRowCount = await context.Set<FestivalIngredient>().CountAsync();
    var recipeLineCount = await context.Set<CatalogItemIngredient>().CountAsync();
    var unitCount = await context.Set<Unit>().CountAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(category.Name, Is.EqualTo("Getränke"));
                      Assert.That(invitation.QRCodeHash, Is.EqualTo(qrCodeHash));
                      Assert.That(invitation.QRCodeSalt, Is.EqualTo(qrCodeSalt));
                      Assert.That(invitation.QRCodeIterations, Is.EqualTo(210000));
                      Assert.That(invitation.QRCodeAlgorithm, Is.EqualTo("PBKDF2-HMAC-SHA512"));
                      Assert.That(order.Table.Name, Is.EqualTo("Tisch 7"));
                      Assert.That(order.Table.FestivalId, Is.EqualTo(festivalId));
                      Assert.That(order.Table.IsHidden, Is.True);
                      Assert.That(order.Table.Area.Name, Is.EqualTo("Bisherige Tische"));
                      Assert.That(order.Table.Area.IsHidden, Is.True);
                      Assert.That(order.GlobalOrderNumber, Is.EqualTo(4));
                      Assert.That(order.CreatedAtUtc, Is.EqualTo(new DateTime(2026, 9, 20, 17, 30, 0)));
                      Assert.That(order.StaffMemberId, Is.EqualTo(staffMemberId));
                      Assert.That(orderItem.ChargedPriceCents, Is.EqualTo(350));
                      Assert.That(orderItem.SettledAtUtc, Is.EqualTo(new DateTime(2026, 9, 20, 18, 0, 0)));
                      Assert.That(orderItem.SettledByStaffMemberId, Is.EqualTo(staffMemberId));
                      Assert.That(orderItem.PaymentMethod, Is.Null);
                      Assert.That(appliedMigrations, Has.Count.EqualTo(13));
                      Assert.That(appliedMigrations[0], Is.EqualTo(_releasedSchema.MigrationId));
                      Assert.That(appliedMigrations[1], Does.EndWith("_CollateCategoryNamesAndCapitalizeQRColumns"));
                      Assert.That(appliedMigrations[2], Does.EndWith("_DropOrderNote"));
                      Assert.That(appliedMigrations[3], Does.EndWith("_AddEntityNavigations"));
                      Assert.That(appliedMigrations[4], Does.EndWith("_AddOrderItemPaymentMethod"));
                      Assert.That(appliedMigrations[5], Does.EndWith("_AddIngredientStock"));
                      Assert.That(appliedMigrations[6], Does.EndWith("_AddTablePlan"));
                      Assert.That(appliedMigrations[7], Does.EndWith("_SwitchOrdersToTables"));
                      Assert.That(appliedMigrations[8], Does.EndWith("_AddUnits"));
                      Assert.That(appliedMigrations[9], Does.EndWith("_RemoveDeviceLastSeen"));
                      Assert.That(appliedMigrations[10], Does.EndWith("_NameColumnsIgnoreCase"));
                      Assert.That(appliedMigrations[11], Does.EndWith("_AddOrderItemStatusHistory"));
                      Assert.That(appliedMigrations[12], Does.EndWith("_MakeHashIterationsOptional"));
                      Assert.That(ingredientCount, Is.Zero);
                      Assert.That(stockRowCount, Is.Zero);
                      Assert.That(recipeLineCount, Is.Zero);
                      Assert.That(unitCount, Is.EqualTo(5));
                    });
  }

  [Test]
  public async Task MigrateAsync_OnReleasedDatabaseWithNamedRowsOtherRowsPointAt_KeepsEveryNameAndEveryLink()
  {
    using SqliteTempFileFixture fixture = new(TemporaryDatabaseSchema.None);
    var categoryId = Guid.NewGuid();
    var festivalId = Guid.NewGuid();
    var staffMemberId = Guid.NewGuid();
    var orderId = Guid.NewGuid();
    var orderItemId = Guid.NewGuid();
    var barLinkId = Guid.NewGuid();
    var barId = Guid.NewGuid();

    var connection = fixture.OpenConnection();
    await ExecuteAsync(connection, _releasedSchema.Sql);
    await InsertCatalogCategoryAsync(connection, categoryId);
    await InsertFestivalAsync(connection, festivalId);
    await InsertStaffMemberAsync(connection, staffMemberId);
    await InsertOrderAsync(connection, orderId, festivalId, staffMemberId);
    await InsertSettledOrderItemAsync(connection, orderItemId, orderId, festivalId, categoryId, staffMemberId);
    await InsertFestivalStationAsync(connection, barLinkId, festivalId, barId, "Theke", 3);

    var context = fixture.CreateContext();
    await context.Database.MigrateAsync();

    var orderItem = await context.Set<OrderItem>().Include(persisted => persisted.CatalogItem).Include(persisted => persisted.SettledByStaffMember).Include(persisted => persisted.StationOrder).ThenInclude(stationOrder => stationOrder.Station).SingleAsync(persisted => persisted.Id == orderItemId);
    var festivalStation = await context.Set<FestivalStation>().Include(persisted => persisted.Festival).Include(persisted => persisted.Station).SingleAsync(persisted => persisted.Id == barLinkId);
    var brokenLinks = await context.Database.SqlQueryRaw<string>("SELECT \"table\" AS \"Value\" FROM pragma_foreign_key_check").ToListAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(orderItem.CatalogItem.Name, Is.EqualTo("Bratwurst"));
                      Assert.That(orderItem.CatalogItem.CategoryId, Is.EqualTo(categoryId));
                      Assert.That(orderItem.StationOrder.Station.Name, Is.EqualTo("Kueche"));
                      Assert.That(orderItem.SettledByStaffMember!.Name, Is.EqualTo("Anna"));
                      Assert.That(festivalStation.Station.Name, Is.EqualTo("Theke"));
                      Assert.That(festivalStation.Festival.Name, Is.EqualTo("Sommerfest"));
                      Assert.That(festivalStation.NextStationOrderNumber, Is.EqualTo(3));
                      Assert.That(brokenLinks, Is.Empty);
                    });
  }

  [Test]
  public async Task MigrateAsync_OnReleasedDatabaseWithDevices_KeepsTheDeviceAndItsToken()
  {
    using SqliteTempFileFixture fixture = new(TemporaryDatabaseSchema.None);
    var deviceId = Guid.NewGuid();

    var connection = fixture.OpenConnection();
    await ExecuteAsync(connection, _releasedSchema.Sql);
    await InsertDeviceAsync(connection, deviceId);

    var context = fixture.CreateContext();
    await context.Database.MigrateAsync();

    var device = await context.Set<Device>().SingleAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(device.Id, Is.EqualTo(deviceId));
                      Assert.That(device.Language, Is.EqualTo("de"));
                      Assert.That(device.TokenHash,
                                  Is.EqualTo(new byte[]
                                             {
                                               17,
                                               18,
                                               19,
                                               20
                                             }));
                      Assert.That(device.TokenSalt,
                                  Is.EqualTo(new byte[]
                                             {
                                               21,
                                               22,
                                               23,
                                               24
                                             }));
                      Assert.That(device.TokenIterations, Is.EqualTo(210000));
                      Assert.That(device.TokenAlgorithm, Is.EqualTo("PBKDF2-HMAC-SHA512"));
                      Assert.That(device.TokenLookupId, Is.EqualTo(deviceId.ToString("N")));
                      Assert.That(device.CreatedAtUtc, Is.EqualTo(new DateTime(2026, 9, 16, 18, 1, 0)));
                    });
  }

  [Test]
  public async Task MigrateAsync_OnReleasedDatabaseWithAPhoneSetUp_StillAcceptsThePhonesToken()
  {
    using SqliteTempFileFixture fixture = new(TemporaryDatabaseSchema.None);
    var deviceId = Guid.NewGuid();
    var staffMemberId = Guid.NewGuid();

    var connection = fixture.OpenConnection();
    await ExecuteAsync(connection, _releasedSchema.Sql);
    await InsertDeviceAsync(connection, deviceId, _released.Hash, _released.Salt);
    await InsertStaffMemberAsync(connection, staffMemberId);
    await PointStaffMemberAtDeviceAsync(connection, staffMemberId, deviceId);

    var context = fixture.CreateContext();
    await context.Database.MigrateAsync();

    var owner = await new EnrolmentComposition(context, TimeProvider.System).Lookup.FindByTokenAsync(deviceId.ToString("N"), _released.Secret, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(owner, Is.InstanceOf<StaffMember>());
                      Assert.That(owner!.Id, Is.EqualTo(staffMemberId));
                      Assert.That(owner.Device!.Id, Is.EqualTo(deviceId));
                    });
  }

  [Test]
  public async Task MigrateAsync_OnReleasedDatabaseWithAnOutstandingInvitation_RedeemsItsCodeOnce()
  {
    using SqliteTempFileFixture fixture = new(TemporaryDatabaseSchema.None);
    FakeTimeProvider clock = new(new(2026, 9, 16, 18, 1, 0, TimeSpan.Zero));

    var connection = fixture.OpenConnection();
    await ExecuteAsync(connection, _releasedSchema.Sql);
    await InsertEnrolmentInvitationAsync(connection, Guid.NewGuid(), _released.Hash, _released.Salt);

    var context = fixture.CreateContext();
    await context.Database.MigrateAsync();

    var enrolment = new EnrolmentComposition(context, clock).Service;
    var first = await enrolment.RedeemAsync(new()
                                            {
                                              Code = _released.Secret,
                                              Name = "Ben"
                                            },
                                            "de",
                                            TestContext.CurrentContext.CancellationToken);
    var second = await enrolment.RedeemAsync(new()
                                             {
                                               Code = _released.Secret,
                                               Name = "Ben"
                                             },
                                             "de",
                                             TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(first.IsError, Is.False);
                      Assert.That(first.Value.StaffMember!.Name, Is.EqualTo("Ben"));
                      Assert.That(second.RefusalMessageKey(), Is.EqualTo("errors.enrolment.codeNoLongerValid"));
                    });
  }

  [Test]
  public async Task MigrateAsync_OnConsumedInvitationsWithAndWithoutTheirDevice_KeepsOnlyTheOneWhoseDeviceExists()
  {
    using SqliteTempFileFixture fixture = new(TemporaryDatabaseSchema.None);
    var survivingDeviceId = Guid.NewGuid();
    var survivingInvitationId = Guid.NewGuid();
    var orphanedInvitationId = Guid.NewGuid();

    var connection = fixture.OpenConnection();
    await ExecuteAsync(connection, _releasedSchema.Sql);
    await InsertDeviceAsync(connection, survivingDeviceId);
    await InsertConsumedEnrolmentInvitationAsync(connection, survivingInvitationId, survivingDeviceId);
    await InsertConsumedEnrolmentInvitationAsync(connection, orphanedInvitationId, Guid.NewGuid());

    var context = fixture.CreateContext();
    await context.Database.MigrateAsync();

    List<EnrolmentInvitation> invitations = await context.Set<EnrolmentInvitation>().ToListAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(invitations.Select(invitation => invitation.Id), Is.EquivalentTo(new[] { survivingInvitationId }));
                      Assert.That(invitations.Single().ConsumedByDeviceId, Is.EqualTo(survivingDeviceId));
                    });
  }

  [Test]
  public async Task MigrateAsync_OnReleasedDatabaseWithStationsAtAFestival_KeepsEveryLinkAndLetsItBePlacedOnThePlan()
  {
    using SqliteTempFileFixture fixture = new(TemporaryDatabaseSchema.None);
    var festivalId = Guid.NewGuid();
    var kitchenLinkId = Guid.NewGuid();
    var kitchenId = Guid.NewGuid();
    var barLinkId = Guid.NewGuid();
    var barId = Guid.NewGuid();

    var connection = fixture.OpenConnection();
    await ExecuteAsync(connection, _releasedSchema.Sql);
    await InsertFestivalAsync(connection, festivalId);
    await InsertFestivalStationAsync(connection, kitchenLinkId, festivalId, kitchenId, "Kueche", 7);
    await InsertFestivalStationAsync(connection, barLinkId, festivalId, barId, "Theke", 3);

    var context = fixture.CreateContext();
    await context.Database.MigrateAsync();

    List<FestivalStation> links = await context.Set<FestivalStation>().OrderBy(link => link.NextStationOrderNumber).ToListAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(links.Select(link => link.Id),
                                  Is.EqualTo(new[]
                                             {
                                               barLinkId,
                                               kitchenLinkId
                                             }));
                      Assert.That(links.Select(link => link.StationId),
                                  Is.EqualTo(new[]
                                             {
                                               barId,
                                               kitchenId
                                             }));
                      Assert.That(links.Select(link => link.FestivalId), Is.All.EqualTo(festivalId));
                      Assert.That(links.Select(link => link.NextStationOrderNumber),
                                  Is.EqualTo(new[]
                                             {
                                               3,
                                               7
                                             }));
                    });

    var areaId = Guid.NewGuid();
    context.Set<Area>()
   .Add(new()
        {
          Id = areaId,
          FestivalId = festivalId,
          Name = "Garten",
          SortOrder = 1,
          IsHidden = false
        });
    context.Set<FestivalStationShape>()
   .Add(new()
        {
          FestivalStationId = kitchenLinkId,
          FestivalId = festivalId,
          AreaId = areaId,
          X = 10,
          Y = 20,
          Width = 60,
          Height = 40,
          Rotation = 0
        });

    Assert.DoesNotThrowAsync(async () => await context.SaveChangesAsync());
  }

  [Test]
  public async Task MigrateAsync_OnReleasedDatabaseWithOrdersAtTwoFestivals_PointsEveryOrderAtAHiddenTableOfItsOwnFestival()
  {
    using SqliteTempFileFixture fixture = new(TemporaryDatabaseSchema.None);
    var summerFestivalId = Guid.NewGuid();
    var autumnFestivalId = Guid.NewGuid();
    var emptyFestivalId = Guid.NewGuid();
    var staffMemberId = Guid.NewGuid();
    var categoryId = Guid.NewGuid();
    var stationId = Guid.NewGuid();
    var catalogItemId = Guid.NewGuid();
    var firstSpellingOrderId = Guid.NewGuid();
    var otherCapitalsOrderId = Guid.NewGuid();
    var barOrderId = Guid.NewGuid();
    var autumnOrderId = Guid.NewGuid();
    var emptyNameOrderId = Guid.NewGuid();
    var blankNameOrderId = Guid.NewGuid();
    var withoutStationOrdersOrderId = Guid.NewGuid();

    var connection = fixture.OpenConnection();
    await ExecuteAsync(connection, _releasedSchema.Sql);
    await InsertCatalogCategoryAsync(connection, categoryId);
    await InsertFestivalAsync(connection, summerFestivalId);
    await InsertFestivalAsync(connection, autumnFestivalId);
    await InsertFestivalAsync(connection, emptyFestivalId);
    await InsertStaffMemberAsync(connection, staffMemberId);
    await InsertStationAndCatalogItemAsync(connection, stationId, catalogItemId, categoryId);
    await InsertOrderWithOneItemAsync(connection, new(otherCapitalsOrderId, summerFestivalId, 2, "tisch 4", "2026-09-20 17:00:00"), staffMemberId, stationId, catalogItemId);
    await InsertOrderWithOneItemAsync(connection, new(firstSpellingOrderId, summerFestivalId, 1, "Tisch 4", "2026-09-20 16:30:00"), staffMemberId, stationId, catalogItemId);
    await InsertOrderWithOneItemAsync(connection, new(barOrderId, summerFestivalId, 3, "Bar", "2026-09-20 18:00:00"), staffMemberId, stationId, catalogItemId);
    await InsertOrderWithOneItemAsync(connection, new(autumnOrderId, autumnFestivalId, 1, "Tisch 4", "2026-09-27 17:00:00"), staffMemberId, stationId, catalogItemId);
    await InsertOrderWithOneItemAsync(connection, new(emptyNameOrderId, summerFestivalId, 4, string.Empty, "2026-09-20 18:10:00"), staffMemberId, stationId, catalogItemId);
    await InsertOrderWithOneItemAsync(connection, new(blankNameOrderId, summerFestivalId, 5, "   ", "2026-09-20 18:20:00"), staffMemberId, stationId, catalogItemId);
    await InsertOrderWithoutStationOrdersAsync(connection, new(withoutStationOrdersOrderId, summerFestivalId, 6, "Tisch 9", "2026-09-20 18:30:00"), staffMemberId);

    var context = fixture.CreateContext();
    await context.Database.MigrateAsync();

    List<Order> orders = await context.Set<Order>().Include(order => order.Table).ThenInclude(table => table.Area).Include(order => order.StationOrders).ThenInclude(stationOrder => stationOrder.Items).ToListAsync();
    Dictionary<Guid, Order> ordersById = orders.ToDictionary(order => order.Id);
    List<Table> tables = await context.Set<Table>().ToListAsync();
    List<Area> areas = await context.Set<Area>().ToListAsync();
    var tableFoundById = await context.Set<Table>().SingleAsync(table => table.Id == ordersById[barOrderId].TableId);

    Assert.Multiple(() =>
                    {
                      Assert.That(orders.Select(order => order.Id),
                                  Is.EquivalentTo(new[]
                                                  {
                                                    firstSpellingOrderId,
                                                    otherCapitalsOrderId,
                                                    barOrderId,
                                                    autumnOrderId,
                                                    emptyNameOrderId,
                                                    blankNameOrderId,
                                                    withoutStationOrdersOrderId
                                                  }));
                      Assert.That(orders.SelectMany(order => order.StationOrders).Count(), Is.EqualTo(6));
                      Assert.That(orders.SelectMany(order => order.StationOrders).SelectMany(stationOrder => stationOrder.Items).Count(), Is.EqualTo(6));
                      Assert.That(orders.Select(order => order.Table.FestivalId), Is.EqualTo(orders.Select(order => order.FestivalId)));
                      Assert.That(orders.Select(order => order.Table.Area.FestivalId), Is.EqualTo(orders.Select(order => order.FestivalId)));
                      Assert.That(orders.Select(order => order.Table.IsHidden), Is.All.True);
                      Assert.That(orders.Select(order => order.Table.Area.IsHidden), Is.All.True);
                      Assert.That(orders.Select(order => order.Table.Area.Name), Is.All.EqualTo("Bisherige Tische"));
                      Assert.That(ordersById[firstSpellingOrderId].Table.Name, Is.EqualTo("Tisch 4"));
                      Assert.That(ordersById[otherCapitalsOrderId].TableId, Is.EqualTo(ordersById[firstSpellingOrderId].TableId));
                      Assert.That(ordersById[barOrderId].Table.Name, Is.EqualTo("Bar"));
                      Assert.That(ordersById[autumnOrderId].Table.Name, Is.EqualTo("Tisch 4"));
                      Assert.That(ordersById[autumnOrderId].TableId, Is.Not.EqualTo(ordersById[firstSpellingOrderId].TableId));
                      Assert.That(ordersById[emptyNameOrderId].Table.Name, Is.Empty);
                      Assert.That(ordersById[blankNameOrderId].Table.Name, Is.EqualTo("   "));
                      Assert.That(ordersById[blankNameOrderId].TableId, Is.Not.EqualTo(ordersById[emptyNameOrderId].TableId));
                      Assert.That(ordersById[withoutStationOrdersOrderId].Table.Name, Is.EqualTo("Tisch 9"));
                      Assert.That(ordersById[withoutStationOrdersOrderId].StationOrders, Is.Empty);
                      Assert.That(tables, Has.Count.EqualTo(6));
                      Assert.That(areas.Select(area => area.FestivalId),
                                  Is.EquivalentTo(new[]
                                                  {
                                                    summerFestivalId,
                                                    autumnFestivalId
                                                  }));
                      Assert.That(tableFoundById.Name, Is.EqualTo("Bar"));
                    });
  }

  [Test]
  public async Task MigrateAsync_OnTheReleasedDatabaseWithIngredients_MapsEveryOldUnitAndKeepsNamesAmountsAndStock()
  {
    using SqliteTempFileFixture fixture = new(TemporaryDatabaseSchema.None);
    var categoryId = Guid.NewGuid();
    var festivalId = Guid.NewGuid();
    var stationId = Guid.NewGuid();
    var catalogItemId = Guid.NewGuid();
    var pieceIngredientId = Guid.NewGuid();
    var gramIngredientId = Guid.NewGuid();
    var millilitreIngredientId = Guid.NewGuid();
    var recipeLineId = Guid.NewGuid();
    var stockRowId = Guid.NewGuid();

    var connection = fixture.OpenConnection();
    await ExecuteAsync(connection, _releasedSchema.Sql);
    await InsertCatalogCategoryAsync(connection, categoryId);
    await InsertFestivalAsync(connection, festivalId);
    await InsertStationAndCatalogItemAsync(connection, stationId, catalogItemId, categoryId);

    var context = fixture.CreateContext();
    await context.Database.MigrateAsync("20261024130330_SwitchOrdersToTables", TestContext.CurrentContext.CancellationToken);

    await InsertIngredientAsync(connection, pieceIngredientId, "Brötchen", 1);
    await InsertIngredientAsync(connection, gramIngredientId, "Senf", 2);
    await InsertIngredientAsync(connection, millilitreIngredientId, "Ketchup", 3);
    await InsertRecipeLineAsync(connection, recipeLineId, catalogItemId, gramIngredientId, 12.5);
    await InsertStockRowAsync(connection, stockRowId, festivalId, millilitreIngredientId, 750.5);

    await context.Database.MigrateAsync(TestContext.CurrentContext.CancellationToken);

    var piece = await context.Set<Ingredient>().Include(ingredient => ingredient.Unit).SingleAsync(ingredient => ingredient.Id == pieceIngredientId);
    var gram = await context.Set<Ingredient>().Include(ingredient => ingredient.Unit).SingleAsync(ingredient => ingredient.Id == gramIngredientId);
    var millilitre = await context.Set<Ingredient>().Include(ingredient => ingredient.Unit).SingleAsync(ingredient => ingredient.Id == millilitreIngredientId);
    var recipeLine = await context.Set<CatalogItemIngredient>().SingleAsync(line => line.Id == recipeLineId);
    var stockRow = await context.Set<FestivalIngredient>().SingleAsync(stock => stock.Id == stockRowId);
    List<string> unitNames = await context.Set<Unit>().Select(unit => unit.Name).ToListAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(piece.Name, Is.EqualTo("Brötchen"));
                      Assert.That(piece.Unit.Name, Is.EqualTo("Stück"));
                      Assert.That(gram.Name, Is.EqualTo("Senf"));
                      Assert.That(gram.Unit.Name, Is.EqualTo("g"));
                      Assert.That(millilitre.Name, Is.EqualTo("Ketchup"));
                      Assert.That(millilitre.Unit.Name, Is.EqualTo("ml"));
                      Assert.That(recipeLine.Amount, Is.EqualTo(12.5));
                      Assert.That(stockRow.AvailableAmount, Is.EqualTo(750.5));
                      Assert.That(unitNames,
                                  Is.EquivalentTo(new[]
                                                  {
                                                    "Stück",
                                                    "g",
                                                    "kg",
                                                    "ml",
                                                    "l"
                                                  }));
                    });
  }

  [Test]
  public async Task MigrateAsync_OnReleasedDatabaseWithASeededUnit_SavesAnIngredientWithThatUnit()
  {
    using SqliteTempFileFixture fixture = new(TemporaryDatabaseSchema.None);

    var connection = fixture.OpenConnection();
    await ExecuteAsync(connection, _releasedSchema.Sql);

    var context = fixture.CreateContext();
    await context.Database.MigrateAsync(TestContext.CurrentContext.CancellationToken);

    var unit = await context.Set<Unit>().SingleAsync(seededUnit => seededUnit.Name == "g");
    var ingredientId = Guid.NewGuid();
    context.Set<Ingredient>()
   .Add(new()
        {
          Id = ingredientId,
          Name = "Zucker",
          UnitId = unit.Id,
          Unit = unit,
          IsActive = true
        });
    await context.SaveChangesAsync();

    var persisted = await context.Set<Ingredient>().Include(ingredient => ingredient.Unit).SingleAsync(ingredient => ingredient.Id == ingredientId);

    Assert.Multiple(() =>
                    {
                      Assert.That(persisted.UnitId, Is.EqualTo(unit.Id));
                      Assert.That(persisted.Unit.Name, Is.EqualTo("g"));
                    });
  }

  [Test]
  public async Task MigrateAsync_OnReleasedDatabaseWithAnOpenAndAHandedOutItem_GivesEachItsHistoryAndPointsAtTheLastStatus()
  {
    using SqliteTempFileFixture fixture = new(TemporaryDatabaseSchema.None);
    var categoryId = Guid.NewGuid();
    var festivalId = Guid.NewGuid();
    var staffMemberId = Guid.NewGuid();
    var orderId = Guid.NewGuid();
    var stationId = Guid.NewGuid();
    var catalogItemId = Guid.NewGuid();
    var openItemId = Guid.NewGuid();
    var handedOutItemId = Guid.NewGuid();

    var connection = fixture.OpenConnection();
    await ExecuteAsync(connection, _releasedSchema.Sql);
    await InsertCatalogCategoryAsync(connection, categoryId);
    await InsertFestivalAsync(connection, festivalId);
    await InsertStaffMemberAsync(connection, staffMemberId);
    await InsertOrderAsync(connection, orderId, festivalId, staffMemberId);
    await InsertStationAndCatalogItemAsync(connection, stationId, catalogItemId, categoryId);
    await InsertOpenAndHandedOutItemsAsync(connection, orderId, festivalId, stationId, catalogItemId, openItemId, handedOutItemId);

    var context = fixture.CreateContext();
    await context.Database.MigrateAsync();

    List<OrderItem> items = await context.Set<OrderItem>().ToListAsync();
    List<OrderItemStatus> openHistory = await context.Set<OrderItemStatus>().Where(status => status.OrderItemId == openItemId).ToListAsync();
    List<OrderItemStatus> handedOutHistory = await context.Set<OrderItemStatus>().Where(status => status.OrderItemId == handedOutItemId).OrderBy(status => status.CreatedAtUtc).ToListAsync();
    var openItem = items.Single(item => item.Id == openItemId);
    var handedOutItem = items.Single(item => item.Id == handedOutItemId);
    var brokenLinks = await context.Database.SqlQueryRaw<string>("SELECT \"table\" AS \"Value\" FROM pragma_foreign_key_check").ToListAsync();

    Assert.Multiple(() =>
                    {
                      Assert.That(openHistory, Has.Count.EqualTo(1));
                      Assert.That(openHistory[0].Status, Is.EqualTo(OrderItemState.Open));
                      Assert.That(openHistory[0].PreviousStatusId, Is.Null);
                      Assert.That(openHistory[0].CreatedAtUtc, Is.EqualTo(new DateTime(2026, 9, 20, 17, 30, 0)));
                      Assert.That(openHistory[0].CreatedByStaffMemberId, Is.EqualTo(staffMemberId));
                      Assert.That(openHistory[0].CreatedByStationId, Is.Null);
                      Assert.That(openItem.CurrentStatusId, Is.EqualTo(openHistory[0].Id));
                      Assert.That(handedOutHistory.Select(status => status.Status),
                                  Is.EqualTo(new[]
                                             {
                                               OrderItemState.Open,
                                               OrderItemState.HandedOut
                                             }));
                      Assert.That(handedOutHistory[0].CreatedAtUtc, Is.EqualTo(new DateTime(2026, 9, 20, 17, 30, 0)));
                      Assert.That(handedOutHistory[0].CreatedByStaffMemberId, Is.EqualTo(staffMemberId));
                      Assert.That(handedOutHistory[1].PreviousStatusId, Is.EqualTo(handedOutHistory[0].Id));
                      Assert.That(handedOutHistory[1].CreatedAtUtc, Is.EqualTo(new DateTime(2026, 9, 20, 17, 45, 0)));
                      Assert.That(handedOutHistory[1].CreatedByStationId, Is.EqualTo(stationId));
                      Assert.That(handedOutHistory[1].CreatedByStaffMemberId, Is.Null);
                      Assert.That(handedOutItem.CurrentStatusId, Is.EqualTo(handedOutHistory[1].Id));
                      Assert.That(handedOutItem.CurrentStatus!.Status, Is.EqualTo(OrderItemState.HandedOut));
                      Assert.That(brokenLinks, Is.Empty);
                    });
  }

  [Test]
  public async Task MigrateAsync_TheStatusHistoryOnAFestivalWithTwentyThousandItems_FinishesWellWithinTheTimeBound()
  {
    using SqliteTempFileFixture fixture = new(TemporaryDatabaseSchema.None);
    var categoryId = Guid.NewGuid();
    var festivalId = Guid.NewGuid();
    var staffMemberId = Guid.NewGuid();
    var stationId = Guid.NewGuid();
    var catalogItemId = Guid.NewGuid();

    var connection = fixture.OpenConnection();
    await ExecuteAsync(connection, _releasedSchema.Sql);
    await InsertCatalogCategoryAsync(connection, categoryId);
    await InsertFestivalAsync(connection, festivalId);
    await InsertStaffMemberAsync(connection, staffMemberId);
    await InsertStationAndCatalogItemAsync(connection, stationId, catalogItemId, categoryId);
    await InsertManyOrdersWithFiveItemsEachAsync(connection, festivalId, staffMemberId, stationId, catalogItemId, 4000);

    var context = fixture.CreateContext();
    await context.Database.MigrateAsync("20261025160032_NameColumnsIgnoreCase", TestContext.CurrentContext.CancellationToken);

    var stopwatch = Stopwatch.StartNew();
    await context.Database.MigrateAsync(TestContext.CurrentContext.CancellationToken);
    var elapsed = stopwatch.Elapsed;

    var statusCount = await context.Set<OrderItemStatus>().CountAsync(TestContext.CurrentContext.CancellationToken);
    var itemsWithoutCurrentStatus = await context.Set<OrderItem>().CountAsync(item => item.CurrentStatusId == null, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(elapsed, Is.LessThan(TimeSpan.FromSeconds(20)), "The status history of a busy festival must be written in seconds, not minutes, while the laptop updates.");
                      Assert.That(statusCount, Is.EqualTo(30000));
                      Assert.That(itemsWithoutCurrentStatus, Is.Zero);
                    });
  }

  [Test]
  public async Task MigrateAsync_BackToTheReleasedSchema_KeepsTheIngredientsAndMapsTheirUnitsBack()
  {
    using SqliteTempFileFixture fixture = new(TemporaryDatabaseSchema.None);
    var ingredientId = Guid.NewGuid();

    var connection = fixture.OpenConnection();
    await ExecuteAsync(connection, _releasedSchema.Sql);

    var context = fixture.CreateContext();
    await context.Database.MigrateAsync(TestContext.CurrentContext.CancellationToken);
    await InsertIngredientWithUnitIdAsync(connection, ingredientId, "Senf", Guid.Parse("2B7C1A10-0000-4000-8000-000000000002"));

    await context.Database.MigrateAsync("20261024130330_SwitchOrdersToTables", TestContext.CurrentContext.CancellationToken);

    await using var unitCommand = connection.CreateCommand();
    unitCommand.CommandText = """SELECT "Unit" FROM "Ingredients" WHERE "Id" = $id;""";
    unitCommand.Parameters.AddWithValue("$id", ToStoredText(ingredientId));
    var storedUnit = Convert.ToInt32(await unitCommand.ExecuteScalarAsync());

    await using var unitsTableCommand = connection.CreateCommand();
    unitsTableCommand.CommandText = """SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'Units';""";
    var unitsTableCount = Convert.ToInt32(await unitsTableCommand.ExecuteScalarAsync());

    Assert.Multiple(() =>
                    {
                      Assert.That(storedUnit, Is.EqualTo(2));
                      Assert.That(unitsTableCount, Is.Zero);
                    });
  }

  private string ToStoredText(Guid id)
  {
    return id.ToString("D").ToUpperInvariant();
  }

  private async Task ExecuteAsync(SqliteConnection connection, string sql)
  {
    await using var command = connection.CreateCommand();
    command.CommandText = sql;
    await command.ExecuteNonQueryAsync();
  }

  private async Task InsertCatalogCategoryAsync(SqliteConnection connection, Guid categoryId)
  {
    await using var command = connection.CreateCommand();
    command.CommandText = """
                          INSERT INTO "CatalogCategories" ("Id", "Name", "NormalizedName", "ColourHex", "SortOrder", "IsActive")
                          VALUES ($id, $name, $normalizedName, $colourHex, $sortOrder, $isActive);
                          """;
    command.Parameters.AddWithValue("$id", ToStoredText(categoryId));
    command.Parameters.AddWithValue("$name", "Getränke");
    command.Parameters.AddWithValue("$normalizedName", "GETRÄNKE");
    command.Parameters.AddWithValue("$colourHex", "#3366CC");
    command.Parameters.AddWithValue("$sortOrder", 1);
    command.Parameters.AddWithValue("$isActive", 1);
    await command.ExecuteNonQueryAsync();
  }

  private async Task InsertIngredientWithUnitIdAsync(SqliteConnection connection, Guid ingredientId, string name, Guid unitId)
  {
    await using var command = connection.CreateCommand();
    command.CommandText = """
                          INSERT INTO "Ingredients" ("Id", "Name", "UnitId", "IsActive")
                          VALUES ($id, $name, $unitId, 1);
                          """;
    command.Parameters.AddWithValue("$id", ToStoredText(ingredientId));
    command.Parameters.AddWithValue("$name", name);
    command.Parameters.AddWithValue("$unitId", ToStoredText(unitId));
    await command.ExecuteNonQueryAsync();
  }

  private async Task InsertIngredientAsync(SqliteConnection connection, Guid ingredientId, string name, int unit)
  {
    await using var command = connection.CreateCommand();
    command.CommandText = """
                          INSERT INTO "Ingredients" ("Id", "Name", "Unit", "IsActive")
                          VALUES ($id, $name, $unit, 1);
                          """;
    command.Parameters.AddWithValue("$id", ToStoredText(ingredientId));
    command.Parameters.AddWithValue("$name", name);
    command.Parameters.AddWithValue("$unit", unit);
    await command.ExecuteNonQueryAsync();
  }

  private async Task InsertRecipeLineAsync(SqliteConnection connection, Guid recipeLineId, Guid catalogItemId, Guid ingredientId, double amount)
  {
    await using var command = connection.CreateCommand();
    command.CommandText = """
                          INSERT INTO "CatalogItemIngredients" ("Id", "CatalogItemId", "IngredientId", "Amount")
                          VALUES ($id, $catalogItemId, $ingredientId, $amount);
                          """;
    command.Parameters.AddWithValue("$id", ToStoredText(recipeLineId));
    command.Parameters.AddWithValue("$catalogItemId", ToStoredText(catalogItemId));
    command.Parameters.AddWithValue("$ingredientId", ToStoredText(ingredientId));
    command.Parameters.AddWithValue("$amount", amount);
    await command.ExecuteNonQueryAsync();
  }

  private async Task InsertStockRowAsync(SqliteConnection connection, Guid stockRowId, Guid festivalId, Guid ingredientId, double availableAmount)
  {
    await using var command = connection.CreateCommand();
    command.CommandText = """
                          INSERT INTO "FestivalIngredients" ("Id", "FestivalId", "IngredientId", "AvailableAmount")
                          VALUES ($id, $festivalId, $ingredientId, $availableAmount);
                          """;
    command.Parameters.AddWithValue("$id", ToStoredText(stockRowId));
    command.Parameters.AddWithValue("$festivalId", ToStoredText(festivalId));
    command.Parameters.AddWithValue("$ingredientId", ToStoredText(ingredientId));
    command.Parameters.AddWithValue("$availableAmount", availableAmount);
    await command.ExecuteNonQueryAsync();
  }

  private async Task InsertEnrolmentInvitationAsync(SqliteConnection connection, Guid invitationId, byte[] qrCodeHash, byte[] qrCodeSalt)
  {
    await using var command = connection.CreateCommand();
    command.CommandText = """
                          INSERT INTO "EnrolmentInvitations" ("Id", "QrCodeHash", "QrCodeSalt", "QrCodeIterations", "QrCodeAlgorithm", "CreatedAtUtc", "ExpiresAtUtc", "ConsumedAtUtc", "ConsumedByDeviceId")
                          VALUES ($id, $hash, $salt, $iterations, $algorithm, $createdAtUtc, $expiresAtUtc, NULL, NULL);
                          """;
    command.Parameters.AddWithValue("$id", ToStoredText(invitationId));
    command.Parameters.AddWithValue("$hash", qrCodeHash);
    command.Parameters.AddWithValue("$salt", qrCodeSalt);
    command.Parameters.AddWithValue("$iterations", 210000);
    command.Parameters.AddWithValue("$algorithm", "PBKDF2-HMAC-SHA512");
    command.Parameters.AddWithValue("$createdAtUtc", "2026-09-16 18:00:00");
    command.Parameters.AddWithValue("$expiresAtUtc", "2026-09-16 18:05:00");
    await command.ExecuteNonQueryAsync();
  }

  private Task InsertDeviceAsync(SqliteConnection connection, Guid deviceId)
  {
    return InsertDeviceAsync(connection,
                             deviceId,
                             new byte[]
                             {
                               17,
                               18,
                               19,
                               20
                             },
                             new byte[]
                             {
                               21,
                               22,
                               23,
                               24
                             });
  }

  private async Task InsertDeviceAsync(SqliteConnection connection, Guid deviceId, byte[] tokenHash, byte[] tokenSalt)
  {
    await using var command = connection.CreateCommand();
    command.CommandText = """
                          INSERT INTO "Devices" ("Id", "Language", "TokenHash", "TokenSalt", "TokenIterations", "TokenAlgorithm", "TokenLookupId", "CreatedAtUtc", "LastSeenAtUtc")
                          VALUES ($id, $language, $tokenHash, $tokenSalt, $iterations, $algorithm, $tokenLookupId, $createdAtUtc, $lastSeenAtUtc);
                          """;
    command.Parameters.AddWithValue("$id", ToStoredText(deviceId));
    command.Parameters.AddWithValue("$language", "de");
    command.Parameters.AddWithValue("$tokenHash", tokenHash);
    command.Parameters.AddWithValue("$tokenSalt", tokenSalt);
    command.Parameters.AddWithValue("$iterations", 210000);
    command.Parameters.AddWithValue("$algorithm", "PBKDF2-HMAC-SHA512");
    command.Parameters.AddWithValue("$tokenLookupId", deviceId.ToString("N"));
    command.Parameters.AddWithValue("$createdAtUtc", "2026-09-16 18:01:00");
    command.Parameters.AddWithValue("$lastSeenAtUtc", "2026-09-20 17:00:00");
    await command.ExecuteNonQueryAsync();
  }

  private async Task InsertConsumedEnrolmentInvitationAsync(SqliteConnection connection, Guid invitationId, Guid consumedByDeviceId)
  {
    await using var command = connection.CreateCommand();
    command.CommandText = """
                          INSERT INTO "EnrolmentInvitations" ("Id", "QrCodeHash", "QrCodeSalt", "QrCodeIterations", "QrCodeAlgorithm", "CreatedAtUtc", "ExpiresAtUtc", "ConsumedAtUtc", "ConsumedByDeviceId")
                          VALUES ($id, $hash, $salt, $iterations, $algorithm, $createdAtUtc, $expiresAtUtc, $consumedAtUtc, $consumedByDeviceId);
                          """;
    command.Parameters.AddWithValue("$id", ToStoredText(invitationId));
    command.Parameters.AddWithValue("$hash",
                                    new byte[]
                                    {
                                      9,
                                      10,
                                      11,
                                      12
                                    });
    command.Parameters.AddWithValue("$salt",
                                    new byte[]
                                    {
                                      13,
                                      14,
                                      15,
                                      16
                                    });
    command.Parameters.AddWithValue("$iterations", 210000);
    command.Parameters.AddWithValue("$algorithm", "PBKDF2-HMAC-SHA512");
    command.Parameters.AddWithValue("$createdAtUtc", "2026-09-16 18:00:00");
    command.Parameters.AddWithValue("$expiresAtUtc", "2026-09-16 18:05:00");
    command.Parameters.AddWithValue("$consumedAtUtc", "2026-09-16 18:01:00");
    command.Parameters.AddWithValue("$consumedByDeviceId", ToStoredText(consumedByDeviceId));
    await command.ExecuteNonQueryAsync();
  }

  private async Task InsertStaffMemberAsync(SqliteConnection connection, Guid staffMemberId)
  {
    await using var command = connection.CreateCommand();
    command.CommandText = """
                          INSERT INTO "StaffMembers" ("Id", "Name", "IsActive", "DeviceId", "EnrolmentInvitationId", "CreatedAtUtc")
                          VALUES ($id, $name, $isActive, NULL, NULL, $createdAtUtc);
                          """;
    command.Parameters.AddWithValue("$id", ToStoredText(staffMemberId));
    command.Parameters.AddWithValue("$name", "Anna");
    command.Parameters.AddWithValue("$isActive", 1);
    command.Parameters.AddWithValue("$createdAtUtc", "2026-09-20 16:00:00");
    await command.ExecuteNonQueryAsync();
  }

  private async Task PointStaffMemberAtDeviceAsync(SqliteConnection connection, Guid staffMemberId, Guid deviceId)
  {
    await using var command = connection.CreateCommand();
    command.CommandText = """
                          UPDATE "StaffMembers" SET "DeviceId" = $deviceId WHERE "Id" = $id;
                          """;
    command.Parameters.AddWithValue("$id", ToStoredText(staffMemberId));
    command.Parameters.AddWithValue("$deviceId", ToStoredText(deviceId));
    await command.ExecuteNonQueryAsync();
  }

  private async Task InsertFestivalAsync(SqliteConnection connection, Guid festivalId)
  {
    await using var command = connection.CreateCommand();
    command.CommandText = """
                          INSERT INTO "Festivals" ("Id", "Name", "StartsAtUtc", "EndsAtUtc", "NextOrderNumber", "IsHidden")
                          VALUES ($id, $name, $startsAtUtc, $endsAtUtc, $nextOrderNumber, $isHidden);
                          """;
    command.Parameters.AddWithValue("$id", ToStoredText(festivalId));
    command.Parameters.AddWithValue("$name", "Sommerfest");
    command.Parameters.AddWithValue("$startsAtUtc", "2026-09-20 16:00:00");
    command.Parameters.AddWithValue("$endsAtUtc", "2026-09-21 02:00:00");
    command.Parameters.AddWithValue("$nextOrderNumber", 5);
    command.Parameters.AddWithValue("$isHidden", 0);
    await command.ExecuteNonQueryAsync();
  }

  private async Task InsertFestivalStationAsync(SqliteConnection connection, Guid linkId, Guid festivalId, Guid stationId, string stationName, int nextStationOrderNumber)
  {
    await using var command = connection.CreateCommand();
    command.CommandText = """
                          INSERT INTO "Stations" ("Id", "Name", "SortOrder", "IsActive", "DeviceId", "EnrolmentInvitationId")
                          VALUES ($stationId, $stationName, 1, 1, NULL, NULL);
                          INSERT INTO "FestivalStations" ("Id", "FestivalId", "StationId", "NextStationOrderNumber")
                          VALUES ($linkId, $festivalId, $stationId, $nextStationOrderNumber);
                          """;
    command.Parameters.AddWithValue("$stationId", ToStoredText(stationId));
    command.Parameters.AddWithValue("$stationName", stationName);
    command.Parameters.AddWithValue("$linkId", ToStoredText(linkId));
    command.Parameters.AddWithValue("$festivalId", ToStoredText(festivalId));
    command.Parameters.AddWithValue("$nextStationOrderNumber", nextStationOrderNumber);
    await command.ExecuteNonQueryAsync();
  }

  private async Task InsertOrderAsync(SqliteConnection connection, Guid orderId, Guid festivalId, Guid staffMemberId)
  {
    await using var command = connection.CreateCommand();
    command.CommandText = """
                          INSERT INTO "Orders" ("Id", "ClientOrderId", "FestivalId", "GlobalOrderNumber", "StaffMemberId", "TableName", "Note", "CreatedAtUtc")
                          VALUES ($id, $clientOrderId, $festivalId, $globalOrderNumber, $staffMemberId, $tableName, $note, $createdAtUtc);
                          """;
    command.Parameters.AddWithValue("$id", ToStoredText(orderId));
    command.Parameters.AddWithValue("$clientOrderId", ToStoredText(Guid.NewGuid()));
    command.Parameters.AddWithValue("$festivalId", ToStoredText(festivalId));
    command.Parameters.AddWithValue("$globalOrderNumber", 4);
    command.Parameters.AddWithValue("$staffMemberId", ToStoredText(staffMemberId));
    command.Parameters.AddWithValue("$tableName", "Tisch 7");
    command.Parameters.AddWithValue("$note", "An old note that nobody reads any more");
    command.Parameters.AddWithValue("$createdAtUtc", "2026-09-20 17:30:00");
    await command.ExecuteNonQueryAsync();
  }

  private async Task InsertStationAndCatalogItemAsync(SqliteConnection connection, Guid stationId, Guid catalogItemId, Guid categoryId)
  {
    await using var command = connection.CreateCommand();
    command.CommandText = """
                          INSERT INTO "Stations" ("Id", "Name", "SortOrder", "IsActive", "DeviceId", "EnrolmentInvitationId")
                          VALUES ($stationId, 'Kueche', 1, 1, NULL, NULL);
                          INSERT INTO "CatalogItems" ("Id", "Name", "CategoryId", "SortOrder", "IsActive", "ProductionMinutes", "IsQueueIndependent")
                          VALUES ($catalogItemId, 'Bratwurst', $categoryId, 1, 1, NULL, 0);
                          """;
    command.Parameters.AddWithValue("$stationId", ToStoredText(stationId));
    command.Parameters.AddWithValue("$catalogItemId", ToStoredText(catalogItemId));
    command.Parameters.AddWithValue("$categoryId", ToStoredText(categoryId));
    await command.ExecuteNonQueryAsync();
  }

  private async Task InsertOrderWithoutStationOrdersAsync(SqliteConnection connection, ReleasedOrder order, Guid staffMemberId)
  {
    await using var command = connection.CreateCommand();
    command.CommandText = """
                          INSERT INTO "Orders" ("Id", "ClientOrderId", "FestivalId", "GlobalOrderNumber", "StaffMemberId", "TableName", "Note", "CreatedAtUtc")
                          VALUES ($orderId, $clientOrderId, $festivalId, $globalOrderNumber, $staffMemberId, $tableName, NULL, $createdAtUtc);
                          """;
    command.Parameters.AddWithValue("$orderId", ToStoredText(order.Id));
    command.Parameters.AddWithValue("$clientOrderId", ToStoredText(Guid.NewGuid()));
    command.Parameters.AddWithValue("$festivalId", ToStoredText(order.FestivalId));
    command.Parameters.AddWithValue("$globalOrderNumber", order.GlobalOrderNumber);
    command.Parameters.AddWithValue("$staffMemberId", ToStoredText(staffMemberId));
    command.Parameters.AddWithValue("$tableName", order.TableName);
    command.Parameters.AddWithValue("$createdAtUtc", order.CreatedAtUtc);
    await command.ExecuteNonQueryAsync();
  }

  private async Task InsertOrderWithOneItemAsync(SqliteConnection connection, ReleasedOrder order, Guid staffMemberId, Guid stationId, Guid catalogItemId)
  {
    await InsertOrderWithoutStationOrdersAsync(connection, order, staffMemberId);

    await using var command = connection.CreateCommand();
    command.CommandText = """
                          INSERT INTO "StationOrders" ("Id", "OrderId", "FestivalId", "StationId", "StationOrderNumber", "DeliveryMode", "IsHiddenFromAsItComesQueue")
                          VALUES ($stationOrderId, $orderId, $festivalId, $stationId, $globalOrderNumber, 0, 0);
                          INSERT INTO "OrderItems" ("Id", "StationOrderId", "CatalogItemId", "ItemName", "UnitPriceCents", "Note", "FulfilledAtUtc", "SettledAtUtc", "ChargedPriceCents", "SettledByStaffMemberId", "PaymentNotice")
                          VALUES ($orderItemId, $stationOrderId, $catalogItemId, 'Bratwurst', 350, NULL, NULL, NULL, NULL, NULL, NULL);
                          """;
    command.Parameters.AddWithValue("$orderId", ToStoredText(order.Id));
    command.Parameters.AddWithValue("$festivalId", ToStoredText(order.FestivalId));
    command.Parameters.AddWithValue("$globalOrderNumber", order.GlobalOrderNumber);
    command.Parameters.AddWithValue("$stationOrderId", ToStoredText(Guid.NewGuid()));
    command.Parameters.AddWithValue("$stationId", ToStoredText(stationId));
    command.Parameters.AddWithValue("$orderItemId", ToStoredText(Guid.NewGuid()));
    command.Parameters.AddWithValue("$catalogItemId", ToStoredText(catalogItemId));
    await command.ExecuteNonQueryAsync();
  }

  private async Task InsertManyOrdersWithFiveItemsEachAsync(SqliteConnection connection, Guid festivalId, Guid staffMemberId, Guid stationId, Guid catalogItemId, int orderCount)
  {
    await using var command = connection.CreateCommand();
    command.CommandText = """
                          WITH RECURSIVE "Numbers"("N") AS (SELECT 1 UNION ALL SELECT "N" + 1 FROM "Numbers" WHERE "N" < $orderCount)
                          INSERT INTO "Orders" ("Id", "ClientOrderId", "FestivalId", "GlobalOrderNumber", "StaffMemberId", "TableName", "Note", "CreatedAtUtc")
                          SELECT printf('00000000-0000-4000-8000-%012d', "N"), printf('00000000-0000-4000-8001-%012d', "N"), $festivalId, "N", $staffMemberId, 'Tisch 7', NULL, '2026-09-20 17:30:00'
                          FROM "Numbers";

                          INSERT INTO "StationOrders" ("Id", "OrderId", "FestivalId", "StationId", "StationOrderNumber", "DeliveryMode", "IsHiddenFromAsItComesQueue")
                          SELECT printf('00000000-0000-4000-9000-%012d', "GlobalOrderNumber"), "Id", $festivalId, $stationId, "GlobalOrderNumber", 0, 0
                          FROM "Orders";

                          WITH RECURSIVE "Numbers"("N") AS (SELECT 1 UNION ALL SELECT "N" + 1 FROM "Numbers" WHERE "N" < $orderCount * 5)
                          INSERT INTO "OrderItems" ("Id", "StationOrderId", "CatalogItemId", "ItemName", "UnitPriceCents", "Note", "FulfilledAtUtc", "SettledAtUtc", "ChargedPriceCents", "SettledByStaffMemberId", "PaymentNotice")
                          SELECT printf('00000000-0000-4000-A000-%012d', "N"), printf('00000000-0000-4000-9000-%012d', ("N" - 1) / 5 + 1), $catalogItemId, 'Bratwurst', 350, NULL,
                                 CASE WHEN "N" % 2 = 0 THEN '2026-09-20 17:45:00' END, NULL, NULL, NULL, NULL
                          FROM "Numbers";
                          """;
    command.Parameters.AddWithValue("$orderCount", orderCount);
    command.Parameters.AddWithValue("$festivalId", ToStoredText(festivalId));
    command.Parameters.AddWithValue("$staffMemberId", ToStoredText(staffMemberId));
    command.Parameters.AddWithValue("$stationId", ToStoredText(stationId));
    command.Parameters.AddWithValue("$catalogItemId", ToStoredText(catalogItemId));
    await command.ExecuteNonQueryAsync();
  }

  private async Task InsertOpenAndHandedOutItemsAsync(SqliteConnection connection, Guid orderId, Guid festivalId, Guid stationId, Guid catalogItemId, Guid openItemId, Guid handedOutItemId)
  {
    await using var command = connection.CreateCommand();
    command.CommandText = """
                          INSERT INTO "StationOrders" ("Id", "OrderId", "FestivalId", "StationId", "StationOrderNumber", "DeliveryMode", "IsHiddenFromAsItComesQueue")
                          VALUES ($stationOrderId, $orderId, $festivalId, $stationId, 1, 1, 0);
                          INSERT INTO "OrderItems" ("Id", "StationOrderId", "CatalogItemId", "ItemName", "UnitPriceCents", "Note", "FulfilledAtUtc", "SettledAtUtc", "ChargedPriceCents", "SettledByStaffMemberId", "PaymentNotice")
                          VALUES ($openItemId, $stationOrderId, $catalogItemId, 'Bratwurst', 350, NULL, NULL, NULL, NULL, NULL, NULL);
                          INSERT INTO "OrderItems" ("Id", "StationOrderId", "CatalogItemId", "ItemName", "UnitPriceCents", "Note", "FulfilledAtUtc", "SettledAtUtc", "ChargedPriceCents", "SettledByStaffMemberId", "PaymentNotice")
                          VALUES ($handedOutItemId, $stationOrderId, $catalogItemId, 'Bratwurst', 350, 'Ohne Senf', '2026-09-20 17:45:00', NULL, NULL, NULL, NULL);
                          """;
    command.Parameters.AddWithValue("$stationOrderId", ToStoredText(Guid.NewGuid()));
    command.Parameters.AddWithValue("$orderId", ToStoredText(orderId));
    command.Parameters.AddWithValue("$festivalId", ToStoredText(festivalId));
    command.Parameters.AddWithValue("$stationId", ToStoredText(stationId));
    command.Parameters.AddWithValue("$catalogItemId", ToStoredText(catalogItemId));
    command.Parameters.AddWithValue("$openItemId", ToStoredText(openItemId));
    command.Parameters.AddWithValue("$handedOutItemId", ToStoredText(handedOutItemId));
    await command.ExecuteNonQueryAsync();
  }

  private async Task InsertSettledOrderItemAsync(SqliteConnection connection, Guid orderItemId, Guid orderId, Guid festivalId, Guid categoryId, Guid staffMemberId)
  {
    var stationId = Guid.NewGuid();
    var catalogItemId = Guid.NewGuid();
    var stationOrderId = Guid.NewGuid();
    await using var command = connection.CreateCommand();
    command.CommandText = """
                          INSERT INTO "Stations" ("Id", "Name", "SortOrder", "IsActive", "DeviceId", "EnrolmentInvitationId")
                          VALUES ($stationId, 'Kueche', 1, 1, NULL, NULL);
                          INSERT INTO "CatalogItems" ("Id", "Name", "CategoryId", "SortOrder", "IsActive", "ProductionMinutes", "IsQueueIndependent")
                          VALUES ($catalogItemId, 'Bratwurst', $categoryId, 1, 1, NULL, 0);
                          INSERT INTO "StationOrders" ("Id", "OrderId", "FestivalId", "StationId", "StationOrderNumber", "DeliveryMode", "IsHiddenFromAsItComesQueue")
                          VALUES ($stationOrderId, $orderId, $festivalId, $stationId, 1, 0, 0);
                          INSERT INTO "OrderItems" ("Id", "StationOrderId", "CatalogItemId", "ItemName", "UnitPriceCents", "Note", "FulfilledAtUtc", "SettledAtUtc", "ChargedPriceCents", "SettledByStaffMemberId", "PaymentNotice")
                          VALUES ($orderItemId, $stationOrderId, $catalogItemId, 'Bratwurst', 350, NULL, NULL, '2026-09-20 18:00:00', 350, $staffMemberId, NULL);
                          """;
    command.Parameters.AddWithValue("$stationId", ToStoredText(stationId));
    command.Parameters.AddWithValue("$catalogItemId", ToStoredText(catalogItemId));
    command.Parameters.AddWithValue("$categoryId", ToStoredText(categoryId));
    command.Parameters.AddWithValue("$stationOrderId", ToStoredText(stationOrderId));
    command.Parameters.AddWithValue("$orderId", ToStoredText(orderId));
    command.Parameters.AddWithValue("$festivalId", ToStoredText(festivalId));
    command.Parameters.AddWithValue("$orderItemId", ToStoredText(orderItemId));
    command.Parameters.AddWithValue("$staffMemberId", ToStoredText(staffMemberId));
    await command.ExecuteNonQueryAsync();
  }
}
