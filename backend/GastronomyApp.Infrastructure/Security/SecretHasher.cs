using System.Security.Cryptography;
using System.Text;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Results;

namespace GastronomyApp.Infrastructure.Security;

public sealed class SecretHasher : ISecretHasher
{
  private const int SaltLengthBytes = 16;
  private const string Sha256AlgorithmName = "SHA-256";

  public HashedSecret Hash(string secret)
  {
    ArgumentNullException.ThrowIfNull(secret);

    var salt = RandomNumberGenerator.GetBytes(SaltLengthBytes);

    return new(HashWithSha256(secret, salt), salt, Sha256AlgorithmName);
  }

  public bool Verify(string secret, byte[] storedHash, byte[] storedSalt, string storedAlgorithm)
  {
    ArgumentNullException.ThrowIfNull(secret);
    ArgumentNullException.ThrowIfNull(storedHash);
    ArgumentNullException.ThrowIfNull(storedSalt);
    ArgumentNullException.ThrowIfNull(storedAlgorithm);

    if (storedAlgorithm != Sha256AlgorithmName)
      return false;

    return CryptographicOperations.FixedTimeEquals(HashWithSha256(secret, storedSalt), storedHash);
  }

  private byte[] HashWithSha256(string secret, byte[] salt)
  {
    using var sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    sha256.AppendData(salt);
    sha256.AppendData(Encoding.UTF8.GetBytes(secret));

    return sha256.GetHashAndReset();
  }
}
