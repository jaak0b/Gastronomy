namespace GastronomyApp.Infrastructure;

public enum InfrastructureFailureReason
{
    DatabaseUnavailable,
    ConflictingChange,
}

public sealed class InfrastructureException : Exception
{
    public InfrastructureException(InfrastructureFailureReason reason, string message, Exception? inner = null)
        : base(message, inner)
    {
        Reason = reason;
    }

    public InfrastructureFailureReason Reason { get; }
}
