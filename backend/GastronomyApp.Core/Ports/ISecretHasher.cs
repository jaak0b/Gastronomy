using GastronomyApp.Core.Results;

namespace GastronomyApp.Core.Ports;

public interface ISecretHasher
{
  public HashedSecret Hash(string secret);

  public bool Verify(string secret, byte[] storedHash, byte[] storedSalt, int? storedIterations, string storedAlgorithm);
}
