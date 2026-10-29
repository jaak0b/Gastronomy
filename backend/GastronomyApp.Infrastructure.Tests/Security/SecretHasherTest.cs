using GastronomyApp.Infrastructure.Security;
using GastronomyApp.Infrastructure.Tests.TestSupport;

namespace GastronomyApp.Infrastructure.Tests.Security;

public sealed class SecretHasherTest
{
  private readonly ReleasedSecretHash _released = new();

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

    Assert.That(() => hasher.Verify(null!, hashed.Hash, hashed.Salt, null, hashed.Algorithm), Throws.ArgumentNullException);
  }

  [Test]
  public void Verify_NullStoredHash_ThrowsArgumentNullException()
  {
    SecretHasher hasher = new();
    var hashed = hasher.Hash("the-secret");

    Assert.That(() => hasher.Verify("the-secret", null!, hashed.Salt, null, hashed.Algorithm), Throws.ArgumentNullException);
  }

  [Test]
  public void Verify_NullStoredSalt_ThrowsArgumentNullException()
  {
    SecretHasher hasher = new();
    var hashed = hasher.Hash("the-secret");

    Assert.That(() => hasher.Verify("the-secret", hashed.Hash, null!, null, hashed.Algorithm), Throws.ArgumentNullException);
  }

  [Test]
  public void Verify_NullStoredAlgorithm_ThrowsArgumentNullException()
  {
    SecretHasher hasher = new();
    var hashed = hasher.Hash("the-secret");

    Assert.That(() => hasher.Verify("the-secret", hashed.Hash, hashed.Salt, null, null!), Throws.ArgumentNullException);
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

    Assert.That(hasher.Verify("the-secret", hashed.Hash, hashed.Salt, null, hashed.Algorithm), Is.True);
  }

  [Test]
  public void Hash_ThenVerify_DifferentSecret_ReturnsFalse()
  {
    SecretHasher hasher = new();

    var hashed = hasher.Hash("the-secret");

    Assert.That(hasher.Verify("another-secret", hashed.Hash, hashed.Salt, null, hashed.Algorithm), Is.False);
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
                      Assert.That(hasher.Verify("stored-by-this-release", storedHash, storedSalt, null, "SHA-256"), Is.True);
                      Assert.That(hasher.Verify("another-secret", storedHash, storedSalt, null, "SHA-256"), Is.False);
                    });
  }

  [Test]
  public void Verify_ARowTheReleasedBuildStoredWithPbkdf2_AcceptsItsSecretAndRefusesAnother()
  {
    SecretHasher hasher = new();

    Assert.Multiple(() =>
                    {
                      Assert.That(hasher.Verify(_released.Secret, _released.Hash, _released.Salt, _released.Iterations, _released.Algorithm), Is.True);
                      Assert.That(hasher.Verify("another-secret", _released.Hash, _released.Salt, _released.Iterations, _released.Algorithm), Is.False);
                    });
  }

  [Test]
  public void Verify_APbkdf2RowReadWithAnotherIterationCount_ReturnsFalse()
  {
    SecretHasher hasher = new();

    Assert.That(hasher.Verify(_released.Secret, _released.Hash, _released.Salt, _released.Iterations + 1, _released.Algorithm), Is.False);
  }

  [Test]
  public void Verify_APbkdf2RowWithoutItsIterationCount_ThrowsArgumentException()
  {
    SecretHasher hasher = new();

    Assert.That(() => hasher.Verify(_released.Secret, _released.Hash, _released.Salt, null, _released.Algorithm), Throws.ArgumentException.With.Property(nameof(ArgumentException.ParamName)).EqualTo("storedIterations"));
  }

  [Test]
  public void Verify_AnAlgorithmItDoesNotKnow_ReturnsFalse()
  {
    SecretHasher hasher = new();

    Assert.That(hasher.Verify(_released.Secret, _released.Hash, _released.Salt, _released.Iterations, "PBKDF2-HMAC-SHA256"), Is.False);
  }
}
