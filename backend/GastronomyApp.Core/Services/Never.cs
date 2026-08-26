namespace GastronomyApp.Core.Services;

public sealed class Never
{
    public TResult OfType<TResult>(object unexpectedValue)
    {
        throw new InvalidOperationException($"Unhandled value: {unexpectedValue}");
    }
}
