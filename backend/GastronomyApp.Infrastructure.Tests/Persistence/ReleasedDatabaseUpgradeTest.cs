using GastronomyApp.Core.Entities;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Tests.Persistence;

public sealed class ReleasedDatabaseUpgradeTest
{
  private readonly ReleasedSchemaScript _releasedSchema = new();

  [Test]
  public async Task MigrateAsync_OnReleasedDatabaseWithData_KeepsRowsAndAppendsOneMigration()
  {
    using SqliteTempFileFixture fixture = new(TemporaryDatabaseSchema.None);
    var categoryId = Guid.NewGuid();
    var festivalId = Guid.NewGuid();
    var invitationId = Guid.NewGuid();
    var orderId = Guid.NewGuid();
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
    await InsertOrderAsync(connection, orderId, festivalId);

    var context = fixture.CreateContext();
    await context.Database.MigrateAsync();

    var category = await context.Set<CatalogCategory>().SingleAsync(persisted => persisted.Id == categoryId);
    var invitation = await context.Set<EnrolmentInvitation>().SingleAsync(persisted => persisted.Id == invitationId);
    var order = await context.Set<Order>().SingleAsync(persisted => persisted.Id == orderId);
    List<string> appliedMigrations = (await context.Database.GetAppliedMigrationsAsync()).ToList();

    Assert.Multiple(() =>
                    {
                      Assert.That(category.Name, Is.EqualTo("Getränke"));
                      Assert.That(invitation.QRCodeHash, Is.EqualTo(qrCodeHash));
                      Assert.That(invitation.QRCodeSalt, Is.EqualTo(qrCodeSalt));
                      Assert.That(invitation.QRCodeIterations, Is.EqualTo(210000));
                      Assert.That(invitation.QRCodeAlgorithm, Is.EqualTo("PBKDF2-HMAC-SHA512"));
                      Assert.That(order.TableName, Is.EqualTo("Tisch 7"));
                      Assert.That(order.GlobalOrderNumber, Is.EqualTo(4));
                      Assert.That(order.CreatedAtUtc, Is.EqualTo(new DateTime(2026, 9, 20, 17, 30, 0)));
                      Assert.That(appliedMigrations, Has.Count.EqualTo(3));
                      Assert.That(appliedMigrations[0], Is.EqualTo(_releasedSchema.MigrationId));
                      Assert.That(appliedMigrations[1], Does.EndWith("_CollateCategoryNamesAndCapitalizeQRColumns"));
                      Assert.That(appliedMigrations[2], Does.EndWith("_DropOrderNote"));
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

  private async Task InsertOrderAsync(SqliteConnection connection, Guid orderId, Guid festivalId)
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
    command.Parameters.AddWithValue("$staffMemberId", ToStoredText(Guid.NewGuid()));
    command.Parameters.AddWithValue("$tableName", "Tisch 7");
    command.Parameters.AddWithValue("$note", "An old note that nobody reads any more");
    command.Parameters.AddWithValue("$createdAtUtc", "2026-09-20 17:30:00");
    await command.ExecuteNonQueryAsync();
  }
}
