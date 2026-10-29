using System.Security.Cryptography;
using System.Text;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Results;

namespace GastronomyApp.Infrastructure.Security;

public sealed class SecretHasher : ISecretHasher
{
  private const int SaltLengthBytes = 16;
  private const string Sha256AlgorithmName = "SHA-256";
  private const string Pbkdf2AlgorithmName = "PBKDF2-HMAC-SHA512";

  public HashedSecret Hash(string secret)
  {
    ArgumentNullException.ThrowIfNull(secret);

    var salt = RandomNumberGenerator.GetBytes(SaltLengthBytes);

    return new(HashWithSha256(secret, salt), salt, Sha256AlgorithmName);
  }

  public bool Verify(string secret, byte[] storedHash, byte[] storedSalt, int? storedIterations, string storedAlgorithm)
  {
    ArgumentNullException.ThrowIfNull(secret);
    ArgumentNullException.ThrowIfNull(storedHash);
    ArgumentNullException.ThrowIfNull(storedSalt);
    ArgumentNullException.ThrowIfNull(storedAlgorithm);

    return storedAlgorithm switch
           {
             Sha256AlgorithmName => CryptographicOperations.FixedTimeEquals(HashWithSha256(secret, storedSalt), storedHash),
             Pbkdf2AlgorithmName => CryptographicOperations.FixedTimeEquals(HashWithPbkdf2(secret, storedSalt, storedIterations, storedHash.Length), storedHash),
             _ => false
           };
  }

  private byte[] HashWithSha256(string secret, byte[] salt)
  {
    using var sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    sha256.AppendData(salt);
    sha256.AppendData(Encoding.UTF8.GetBytes(secret));

    return sha256.GetHashAndReset();
  }

  private byte[] HashWithPbkdf2(string secret, byte[] salt, int? storedIterations, int hashLengthBytes)
  {
    if (storedIterations is null)
      throw new ArgumentException("A secret stored with PBKDF2 must carry the iteration count it was hashed with.", nameof(storedIterations));

    return Rfc2898DeriveBytes.Pbkdf2(secret, salt, storedIterations.Value, HashAlgorithmName.SHA512, hashLengthBytes);
  }
}
