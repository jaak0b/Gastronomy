namespace GastronomyApp.Core.Exceptions;

public sealed class ConcurrentWriteException : Exception
{
  public ConcurrentWriteException()
  {
  }

  public ConcurrentWriteException(string message) : base(message)
  {
  }

  public ConcurrentWriteException(string message, Exception innerException) : base(message, innerException)
  {
  }
}
