using GastronomyApp.Infrastructure.Security;

namespace GastronomyApp.Infrastructure.Tests;

public sealed class Pbkdf2SecretHasherTest
{
  [Test]
  public void Hash_ThenVerify_SameSecret_ReturnsTrue()
  {
    Pbkdf2SecretHasher hasher = new();

    HashedSecret hashed = hasher.Hash("the-secret");

    Assert.That(
        hasher.Verify("the-secret", hashed.Hash, hashed.Salt, hashed.Iterations, hashed.Algorithm),
        Is.True);
  }

  [Test]
  public void Verify_DifferentSecret_ReturnsFalse()
  {
    Pbkdf2SecretHasher hasher = new();

    HashedSecret hashed = hasher.Hash("the-secret");

    Assert.That(
        hasher.Verify("another-secret", hashed.Hash, hashed.Salt, hashed.Iterations, hashed.Algorithm),
        Is.False);
  }

  [Test]
  public void Hash_TwoCallsSameSecret_ProducesDifferentSaltAndDifferentHash()
  {
    Pbkdf2SecretHasher hasher = new();

    HashedSecret first = hasher.Hash("the-secret");
    HashedSecret second = hasher.Hash("the-secret");

    Assert.Multiple(() =>
    {
      Assert.That(second.Salt, Is.Not.EqualTo(first.Salt));
      Assert.That(second.Hash, Is.Not.EqualTo(first.Hash));
    });
  }

  [Test]
  public void Verify_HonoursStoredIterationCountEvenIfDefaultChanges()
  {
    Pbkdf2SecretHasher hasher = new();
    HashedSecret hashed = hasher.Hash("the-secret");

    Assert.Multiple(() =>
    {
      Assert.That(
              hasher.Verify("the-secret", hashed.Hash, hashed.Salt, hashed.Iterations, hashed.Algorithm),
              Is.True);
      Assert.That(
              hasher.Verify("the-secret", hashed.Hash, hashed.Salt, hashed.Iterations + 1, hashed.Algorithm),
              Is.False);
      Assert.That(
              hasher.Verify("the-secret", hashed.Hash, hashed.Salt, hashed.Iterations, "PBKDF2-HMAC-SHA256"),
              Is.False);
    });
  }
}
