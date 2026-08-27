using GastronomyApp.Infrastructure.Ports;
using GastronomyApp.Infrastructure.Repositories;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.Data.Sqlite;

namespace GastronomyApp.Infrastructure.Tests;

public sealed class DeviceTokenStoreTest
{
    [Test]
    public async Task IssueAsync_ThenVerifyAsync_WithReturnedSecret_IsValid()
    {
        using SqliteInMemoryFixture fixture = new();
        DeviceTokenStore store = CreateStore(fixture);

        IssuedDeviceToken issued = await store.IssueAsync(Guid.NewGuid(), "de", "Test agent", TestContext.CurrentContext.CancellationToken);
        TokenParts parts = SplitToken(issued.PlaintextToken);

        DeviceVerificationResult verification = await store.VerifyAsync(parts.TokenLookupId, parts.Secret, TestContext.CurrentContext.CancellationToken);

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
        DeviceTokenStore store = CreateStore(fixture);

        IssuedDeviceToken issued = await store.IssueAsync(Guid.NewGuid(), "de", "Test agent", TestContext.CurrentContext.CancellationToken);
        TokenParts parts = SplitToken(issued.PlaintextToken);

        DeviceVerificationResult verification = await store.VerifyAsync(parts.TokenLookupId, "wrong-secret", TestContext.CurrentContext.CancellationToken);

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
        DeviceTokenStore store = CreateStore(fixture);

        IssuedDeviceToken issued = await store.IssueAsync(Guid.NewGuid(), "de", "Test agent", TestContext.CurrentContext.CancellationToken);
        TokenParts parts = SplitToken(issued.PlaintextToken);

        await store.RevokeAsync(issued.Device.Id, TestContext.CurrentContext.CancellationToken);
        DeviceVerificationResult verification = await store.VerifyAsync(parts.TokenLookupId, parts.Secret, TestContext.CurrentContext.CancellationToken);

        Assert.That(verification.IsValid, Is.False);
    }

    [Test]
    public async Task VerifyAsync_UnknownTokenLookupId_IsInvalid()
    {
        using SqliteInMemoryFixture fixture = new();
        DeviceTokenStore store = CreateStore(fixture);

        DeviceVerificationResult verification = await store.VerifyAsync(
            Guid.NewGuid().ToString("N"), "any-secret", TestContext.CurrentContext.CancellationToken);

        Assert.That(verification.IsValid, Is.False);
    }

    [Test]
    public async Task IssueAsync_NewToken_NeverPersistsPlaintextSecretAnywhere()
    {
        using SqliteInMemoryFixture fixture = new();
        DeviceTokenStore store = CreateStore(fixture);

        IssuedDeviceToken issued = await store.IssueAsync(Guid.NewGuid(), "de", "Test agent", TestContext.CurrentContext.CancellationToken);
        TokenParts parts = SplitToken(issued.PlaintextToken);

        List<string> storedValues = [];
        using (SqliteCommand command = fixture.Connection.CreateCommand())
        {
            command.CommandText = "SELECT * FROM Devices";
            using SqliteDataReader reader = await command.ExecuteReaderAsync(TestContext.CurrentContext.CancellationToken);
            while (await reader.ReadAsync(TestContext.CurrentContext.CancellationToken))
            {
                for (int column = 0; column < reader.FieldCount; column++)
                {
                    object value = reader.GetValue(column);
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

    private sealed record TokenParts(string TokenLookupId, string Secret);

    private TokenParts SplitToken(string plaintextToken)
    {
        int separatorIndex = plaintextToken.IndexOf('.', StringComparison.Ordinal);

        return new TokenParts(
            plaintextToken[..separatorIndex],
            plaintextToken[(separatorIndex + 1)..]);
    }

    private DeviceTokenStore CreateStore(SqliteInMemoryFixture fixture)
    {
        return new DeviceTokenStore(fixture.DbContext, new Security.Pbkdf2SecretHasher(), new SystemClock());
    }
}
