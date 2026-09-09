namespace GastronomyApp.Api.Auth;

public sealed record DeviceTokenParts(string TokenLookupId, string Secret);

public sealed class DeviceTokenSplitter
{
  private const char LookupIdSeparator = '.';

  public DeviceTokenParts? Split(string? presentedToken)
  {
    if (string.IsNullOrEmpty(presentedToken))
    {
      return null;
    }

    var separatorIndex = presentedToken.IndexOf(LookupIdSeparator);

    if (separatorIndex <= 0 || separatorIndex == presentedToken.Length - 1)
    {
      return null;
    }

    return new(presentedToken[..separatorIndex], presentedToken[(separatorIndex + 1)..]);
  }
}
