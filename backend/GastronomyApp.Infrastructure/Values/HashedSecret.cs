namespace GastronomyApp.Infrastructure.Values;

public sealed record HashedSecret(byte[] Hash, byte[] Salt, int Iterations, string Algorithm);
