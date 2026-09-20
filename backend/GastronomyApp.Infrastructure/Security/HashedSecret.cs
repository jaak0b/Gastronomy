namespace GastronomyApp.Infrastructure.Security;

public sealed record HashedSecret(byte[] Hash, byte[] Salt, int Iterations, string Algorithm);
