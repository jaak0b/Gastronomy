using System.Security.Cryptography;

namespace GastronomyApp.Infrastructure.Security;

public sealed class Pbkdf2SecretHasher
{
  private const int DefaultIterations = 210_000;
  private const int SaltLengthBytes = 16;
  private const int HashLengthBytes = 32;
  private const string AlgorithmName = "PBKDF2-HMAC-SHA512";

  public HashedSecret Hash(string secret)
  {
    ArgumentNullException.ThrowIfNull(secret);

    var salt = RandomNumberGenerator.GetBytes(SaltLengthBytes);
    var hash = Rfc2898DeriveBytes.Pbkdf2(secret,
                                         salt,
                                         DefaultIterations,
                                         HashAlgorithmName.SHA512,
                                         HashLengthBytes);

    return new(hash, salt, DefaultIterations, AlgorithmName);
  }

  public bool Verify(string secret, byte[] storedHash, byte[] storedSalt, int storedIterations, string storedAlgorithm)
  {
    ArgumentNullException.ThrowIfNull(secret);
    ArgumentNullException.ThrowIfNull(storedHash);
    ArgumentNullException.ThrowIfNull(storedSalt);
    ArgumentNullException.ThrowIfNull(storedAlgorithm);

    if (storedAlgorithm != AlgorithmName)
    {
      return false;
    }

    var computed = Rfc2898DeriveBytes.Pbkdf2(secret,
                                             storedSalt,
                                             storedIterations,
                                             HashAlgorithmName.SHA512,
                                             storedHash.Length);

    return CryptographicOperations.FixedTimeEquals(computed, storedHash);
  }
}
