using GastronomyApp.Infrastructure.Security;

namespace GastronomyApp.Infrastructure.Tests;

public sealed class Pbkdf2SecretHasherTest
{
  [Test]
  public void Hash_NullSecret_ThrowsArgumentNullException()
  {
    Pbkdf2SecretHasher hasher = new();

    Assert.That(() => hasher.Hash(null!), Throws.ArgumentNullException);
  }

  [Test]
  public void Verify_NullSecret_ThrowsArgumentNullException()
  {
    Pbkdf2SecretHasher hasher = new();
    var hashed = hasher.Hash("the-secret");

    Assert.That(() => hasher.Verify(null!, hashed.Hash, hashed.Salt, hashed.Iterations, hashed.Algorithm), Throws.ArgumentNullException);
  }

  [Test]
  public void Verify_NullStoredHash_ThrowsArgumentNullException()
  {
    Pbkdf2SecretHasher hasher = new();
    var hashed = hasher.Hash("the-secret");

    Assert.That(() => hasher.Verify("the-secret", null!, hashed.Salt, hashed.Iterations, hashed.Algorithm), Throws.ArgumentNullException);
  }

  [Test]
  public void Verify_NullStoredSalt_ThrowsArgumentNullException()
  {
    Pbkdf2SecretHasher hasher = new();
    var hashed = hasher.Hash("the-secret");

    Assert.That(() => hasher.Verify("the-secret", hashed.Hash, null!, hashed.Iterations, hashed.Algorithm), Throws.ArgumentNullException);
  }

  [Test]
  public void Verify_NullStoredAlgorithm_ThrowsArgumentNullException()
  {
    Pbkdf2SecretHasher hasher = new();
    var hashed = hasher.Hash("the-secret");

    Assert.That(() => hasher.Verify("the-secret", hashed.Hash, hashed.Salt, hashed.Iterations, null!), Throws.ArgumentNullException);
  }

  [Test]
  public void Hash_ThenVerify_SameSecret_ReturnsTrue()
  {
    Pbkdf2SecretHasher hasher = new();

    var hashed = hasher.Hash("the-secret");

    Assert.That(hasher.Verify("the-secret", hashed.Hash, hashed.Salt, hashed.Iterations, hashed.Algorithm), Is.True);
  }

  [Test]
  public void Verify_DifferentSecret_ReturnsFalse()
  {
    Pbkdf2SecretHasher hasher = new();

    var hashed = hasher.Hash("the-secret");

    Assert.That(hasher.Verify("another-secret", hashed.Hash, hashed.Salt, hashed.Iterations, hashed.Algorithm), Is.False);
  }

  [Test]
  public void Hash_TwoCallsSameSecret_ProducesDifferentSaltAndDifferentHash()
  {
    Pbkdf2SecretHasher hasher = new();

    var first = hasher.Hash("the-secret");
    var second = hasher.Hash("the-secret");

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
    var hashed = hasher.Hash("the-secret");

    Assert.Multiple(() =>
                    {
                      Assert.That(hasher.Verify("the-secret", hashed.Hash, hashed.Salt, hashed.Iterations, hashed.Algorithm), Is.True);
                      Assert.That(hasher.Verify("the-secret", hashed.Hash, hashed.Salt, hashed.Iterations + 1, hashed.Algorithm), Is.False);
                      Assert.That(hasher.Verify("the-secret", hashed.Hash, hashed.Salt, hashed.Iterations, "PBKDF2-HMAC-SHA256"), Is.False);
                    });
  }
}
