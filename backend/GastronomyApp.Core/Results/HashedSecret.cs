namespace GastronomyApp.Core.Results;

public sealed record HashedSecret(byte[] Hash, byte[] Salt, string Algorithm);
