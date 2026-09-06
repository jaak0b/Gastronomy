using GastronomyApp.Core.Entities;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Tests;

public sealed class SqliteFailureTranslatorTest
{
  private readonly SqliteFailureTranslator _translator = new();

  [Test]
  public void IsUniqueConstraintViolation_RealUniqueIndexCollision_IsRecognised()
  {
    using SqliteInMemoryFixture fixture = new();
    DateTime now = new(2026, 8, 27, 18, 0, 0, DateTimeKind.Utc);

    fixture.DbContext.EnrolmentInvitations.Add(BuildUnconsumedInvitation(now));
    fixture.DbContext.EnrolmentInvitations.Add(BuildUnconsumedInvitation(now));

    var failure = Assert.ThrowsAsync<DbUpdateException>(async () => await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken))!;

    var inner = (SqliteException)failure.InnerException!;

    Assert.Multiple(() =>
                    {
                      Assert.That(_translator.IsUniqueConstraintViolation(inner), Is.True);
                      Assert.That(_translator.IsDatabaseUnavailable(inner), Is.False);
                    });
  }

  [Test]
  public void TranslateConflict_UniqueViolation_CarriesTheConflictingChangeReason()
  {
    SqliteException violation = new("UNIQUE constraint failed", 19, 2067);

    var translated = _translator.TranslateConflict(violation);

    Assert.Multiple(() =>
                    {
                      Assert.That(translated.Reason, Is.EqualTo(InfrastructureFailureReason.ConflictingChange));
                      Assert.That(translated.InnerException, Is.SameAs(violation));
                    });
  }

  private EnrolmentInvitation BuildUnconsumedInvitation(DateTime now)
  {
    return new()
           {
             Id = Guid.NewGuid(),
             QrCodeHash = [1],
             QrCodeSalt = [2],
             QrCodeIterations = 1,
             QrCodeAlgorithm = "PBKDF2-HMAC-SHA512",
             CreatedAtUtc = now,
             ExpiresAtUtc = now.AddMinutes(5),
             ConsumedAtUtc = null,
             ConsumedByDeviceId = null
           };
  }
}
