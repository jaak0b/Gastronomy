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
    var invitationId = Guid.NewGuid();
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

    var context = fixture.CreateContext();
    await context.Database.MigrateAsync();

    var category = await context.Set<CatalogCategory>().SingleAsync(persisted => persisted.Id == categoryId);
    var invitation = await context.Set<EnrolmentInvitation>().SingleAsync(persisted => persisted.Id == invitationId);
    List<string> appliedMigrations = (await context.Database.GetAppliedMigrationsAsync()).ToList();

    Assert.Multiple(() =>
                    {
                      Assert.That(category.Name, Is.EqualTo("Getränke"));
                      Assert.That(invitation.QRCodeHash, Is.EqualTo(qrCodeHash));
                      Assert.That(invitation.QRCodeSalt, Is.EqualTo(qrCodeSalt));
                      Assert.That(invitation.QRCodeIterations, Is.EqualTo(210000));
                      Assert.That(invitation.QRCodeAlgorithm, Is.EqualTo("PBKDF2-HMAC-SHA512"));
                      Assert.That(appliedMigrations, Has.Count.EqualTo(2));
                      Assert.That(appliedMigrations[0], Is.EqualTo(_releasedSchema.MigrationId));
                      Assert.That(appliedMigrations[1], Does.EndWith("_CollateCategoryNamesAndCapitalizeQRColumns"));
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
}
