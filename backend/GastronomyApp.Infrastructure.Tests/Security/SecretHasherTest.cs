using GastronomyApp.Infrastructure.Security;

namespace GastronomyApp.Infrastructure.Tests.Security;

public sealed class SecretHasherTest
{
  [Test]
  public void Hash_NullSecret_ThrowsArgumentNullException()
  {
    SecretHasher hasher = new();

    Assert.That(() => hasher.Hash(null!), Throws.ArgumentNullException);
  }

  [Test]
  public void Verify_NullSecret_ThrowsArgumentNullException()
  {
    SecretHasher hasher = new();
    var hashed = hasher.Hash("the-secret");

    Assert.That(() => hasher.Verify(null!, hashed.Hash, hashed.Salt, hashed.Algorithm), Throws.ArgumentNullException);
  }

  [Test]
  public void Verify_NullStoredHash_ThrowsArgumentNullException()
  {
    SecretHasher hasher = new();
    var hashed = hasher.Hash("the-secret");

    Assert.That(() => hasher.Verify("the-secret", null!, hashed.Salt, hashed.Algorithm), Throws.ArgumentNullException);
  }

  [Test]
  public void Verify_NullStoredSalt_ThrowsArgumentNullException()
  {
    SecretHasher hasher = new();
    var hashed = hasher.Hash("the-secret");

    Assert.That(() => hasher.Verify("the-secret", hashed.Hash, null!, hashed.Algorithm), Throws.ArgumentNullException);
  }

  [Test]
  public void Verify_NullStoredAlgorithm_ThrowsArgumentNullException()
  {
    SecretHasher hasher = new();
    var hashed = hasher.Hash("the-secret");

    Assert.That(() => hasher.Verify("the-secret", hashed.Hash, hashed.Salt, null!), Throws.ArgumentNullException);
  }

  [Test]
  public void Hash_ASecret_NamesSha256AsItsAlgorithm()
  {
    SecretHasher hasher = new();

    var hashed = hasher.Hash("the-secret");

    Assert.That(hashed.Algorithm, Is.EqualTo("SHA-256"));
  }

  [Test]
  public void Hash_ThenVerify_SameSecret_ReturnsTrue()
  {
    SecretHasher hasher = new();

    var hashed = hasher.Hash("the-secret");

    Assert.That(hasher.Verify("the-secret", hashed.Hash, hashed.Salt, hashed.Algorithm), Is.True);
  }

  [Test]
  public void Hash_ThenVerify_DifferentSecret_ReturnsFalse()
  {
    SecretHasher hasher = new();

    var hashed = hasher.Hash("the-secret");

    Assert.That(hasher.Verify("another-secret", hashed.Hash, hashed.Salt, hashed.Algorithm), Is.False);
  }

  [Test]
  public void Hash_TwoCallsSameSecret_ProducesDifferentSaltAndDifferentHash()
  {
    SecretHasher hasher = new();

    var first = hasher.Hash("the-secret");
    var second = hasher.Hash("the-secret");

    Assert.Multiple(() =>
                    {
                      Assert.That(second.Salt, Is.Not.EqualTo(first.Salt));
                      Assert.That(second.Hash, Is.Not.EqualTo(first.Hash));
                    });
  }

  [Test]
  public void Verify_ARowThisReleaseStoredWithSha256_AcceptsItsSecretAndRefusesAnother()
  {
    SecretHasher hasher = new();
    var storedHash = Convert.FromHexString("2C1CCCA0418D677252BF068EEDA0910C694DA23A840E51F4E7241F0A37B4C2D9");
    var storedSalt = Convert.FromHexString("7B585621BA07F9061422E8D52C55C2A6");

    Assert.Multiple(() =>
                    {
                      Assert.That(hasher.Verify("stored-by-this-release", storedHash, storedSalt, "SHA-256"), Is.True);
                      Assert.That(hasher.Verify("another-secret", storedHash, storedSalt, "SHA-256"), Is.False);
                    });
  }

  [Test]
  public void Verify_AnAlgorithmItDoesNotKnow_ReturnsFalse()
  {
    SecretHasher hasher = new();
    var hashed = hasher.Hash("the-secret");

    Assert.That(hasher.Verify("the-secret", hashed.Hash, hashed.Salt, "SHA-512"), Is.False);
  }
}
