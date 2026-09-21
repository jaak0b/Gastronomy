namespace GastronomyApp.Core.Services;

public sealed class UnreachableCase
{
  public TResult Throw<TResult>(object unexpectedValue)
  {
    throw new InvalidOperationException($"Unhandled value: {unexpectedValue}");
  }
}
