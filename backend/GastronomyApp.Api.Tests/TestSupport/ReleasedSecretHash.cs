namespace GastronomyApp.Api.Tests.TestSupport;

public sealed class ReleasedSecretHash
{
  public string Secret { get; } = "released-secret";

  public byte[] Hash { get; } = Convert.FromHexString("4B1F449981AA257D68A8565357D2D4DE00EE3FDBF52B2C6E90E72AE16149B283");

  public byte[] Salt { get; } = Convert.FromHexString("CB3A9EE3A28926C74694BBD7C5E103F7");

  public int Iterations { get; } = 210_000;

  public string Algorithm { get; } = "PBKDF2-HMAC-SHA512";
}
