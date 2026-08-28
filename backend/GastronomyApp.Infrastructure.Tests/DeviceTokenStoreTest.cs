using GastronomyApp.Infrastructure.Repositories;
using GastronomyApp.Infrastructure.Tests.TestSupport;

namespace GastronomyApp.Infrastructure.Tests;

public sealed class DeviceTokenStoreTest
{
  [Test]
  public async Task IssueAsync_ThenVerifyAsync_WithReturnedSecret_IsValid()
  {
    using SqliteInMemoryFixture fixture = new();
    var store = CreateStore(fixture);

    var issued = await store.IssueAsync(Guid.NewGuid(), "de", "Test agent", TestContext.CurrentContext.CancellationToken);
    var parts = SplitToken(issued.PlaintextToken);

    var verification = await store.VerifyAsync(parts.TokenLookupId, parts.Secret, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(verification.IsValid, Is.True);
                      Assert.That(verification.Device!.Id, Is.EqualTo(issued.Device.Id));
                    });
  }

  [Test]
  public async Task VerifyAsync_WrongSecret_IsInvalid()
  {
    using SqliteInMemoryFixture fixture = new();
    var store = CreateStore(fixture);

    var issued = await store.IssueAsync(Guid.NewGuid(), "de", "Test agent", TestContext.CurrentContext.CancellationToken);
    var parts = SplitToken(issued.PlaintextToken);

    var verification = await store.VerifyAsync(parts.TokenLookupId, "wrong-secret", TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(verification.IsValid, Is.False);
                      Assert.That(verification.Device, Is.Null);
                    });
  }

  [Test]
  public async Task VerifyAsync_RevokedDevice_IsInvalid()
  {
    using SqliteInMemoryFixture fixture = new();
    var store = CreateStore(fixture);

    var issued = await store.IssueAsync(Guid.NewGuid(), "de", "Test agent", TestContext.CurrentContext.CancellationToken);
    var parts = SplitToken(issued.PlaintextToken);

    await store.RevokeAsync(issued.Device.Id, TestContext.CurrentContext.CancellationToken);
    var verification = await store.VerifyAsync(parts.TokenLookupId, parts.Secret, TestContext.CurrentContext.CancellationToken);

    Assert.That(verification.IsValid, Is.False);
  }

  [Test]
  public async Task VerifyAsync_UnknownTokenLookupId_IsInvalid()
  {
    using SqliteInMemoryFixture fixture = new();
    var store = CreateStore(fixture);

    var verification = await store.VerifyAsync(Guid.NewGuid().ToString("N"), "any-secret", TestContext.CurrentContext.CancellationToken);

    Assert.That(verification.IsValid, Is.False);
  }

  [Test]
  public async Task IssueAsync_NewToken_NeverPersistsPlaintextSecretAnywhere()
  {
    using SqliteInMemoryFixture fixture = new();
    var store = CreateStore(fixture);

    var issued = await store.IssueAsync(Guid.NewGuid(), "de", "Test agent", TestContext.CurrentContext.CancellationToken);
    var parts = SplitToken(issued.PlaintextToken);

    List<string> storedValues = [];
    using (var command = fixture.Connection.CreateCommand())
    {
      command.CommandText = "SELECT * FROM Devices";
      using var reader = await command.ExecuteReaderAsync(TestContext.CurrentContext.CancellationToken);
      while (await reader.ReadAsync(TestContext.CurrentContext.CancellationToken))
      {
        for (var column = 0; column < reader.FieldCount; column++)
        {
          var value = reader.GetValue(column);
          storedValues.Add(value is byte[] bytes ? Convert.ToHexString(bytes) : value.ToString() ?? string.Empty);
        }
      }
    }

    Assert.Multiple(() =>
                    {
                      Assert.That(storedValues, Is.Not.Empty);
                      Assert.That(storedValues.Any(value => value.Contains(parts.Secret, StringComparison.Ordinal)), Is.False);
                      Assert.That(storedValues.Any(value => value.Contains(issued.PlaintextToken, StringComparison.Ordinal)), Is.False);
                    });
  }

  private TokenParts SplitToken(string plaintextToken)
  {
    var separatorIndex = plaintextToken.IndexOf('.', StringComparison.Ordinal);

    return new(plaintextToken[..separatorIndex],
               plaintextToken[(separatorIndex + 1)..]);
  }

  private DeviceTokenStore CreateStore(SqliteInMemoryFixture fixture)
  {
    return new(fixture.DbContext, new(), new SystemClock());
  }

  private sealed record TokenParts(string TokenLookupId, string Secret);
}
