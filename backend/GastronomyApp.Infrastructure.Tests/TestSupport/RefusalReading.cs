using ErrorOr;

namespace GastronomyApp.Infrastructure.Tests.TestSupport;

public static class RefusalReading
{
  public static TRefusalType RefusalType<TRefusalType>(this IErrorOr refused) where TRefusalType : struct, Enum
  {
    ArgumentNullException.ThrowIfNull(refused);

    return (TRefusalType)Enum.ToObject(typeof(TRefusalType), FirstErrorOf(refused).NumericType);
  }

  public static string RefusalMessageKey(this IErrorOr refused)
  {
    ArgumentNullException.ThrowIfNull(refused);

    return FirstErrorOf(refused).Code;
  }

  public static string RefusalDescription(this IErrorOr refused)
  {
    ArgumentNullException.ThrowIfNull(refused);

    return FirstErrorOf(refused).Description;
  }

  public static string RefusalMetadata(this IErrorOr refused, string key)
  {
    ArgumentNullException.ThrowIfNull(refused);

    return FirstErrorOf(refused).Metadata![key].ToString()!;
  }

  public static IReadOnlyList<Error> RefusedLines(this IErrorOr refused)
  {
    ArgumentNullException.ThrowIfNull(refused);

    return refused.Errors!;
  }

  private static Error FirstErrorOf(IErrorOr refused)
  {
    return refused.Errors![0];
  }
}
