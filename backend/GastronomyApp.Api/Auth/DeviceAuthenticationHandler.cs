using System.Security.Claims;
using System.Text.Encodings.Web;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GastronomyApp.Api.Auth;

public sealed class DeviceAuthenticationHandler : AuthenticationHandler<DeviceAuthenticationSchemeOptions>
{
  private const string BearerPrefix = "Bearer ";
  private const string AccessTokenQueryKey = "access_token";
  private const string HubPathPrefix = "/hub";
  private readonly DeviceOwnerLookup _ownerLookup;
  private readonly IDeviceTokenSplitter _tokenSplitter;

  public DeviceAuthenticationHandler(IOptionsMonitor<DeviceAuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, DeviceOwnerLookup ownerLookup, IDeviceTokenSplitter tokenSplitter) : base(options, logger, encoder)
  {
    _ownerLookup = ownerLookup;
    _tokenSplitter = tokenSplitter;
  }

  override protected async Task<AuthenticateResult> HandleAuthenticateAsync()
  {
    var presentedToken = ReadPresentedToken();
    if (presentedToken is null)
      return AuthenticateResult.NoResult();

    var tokenParts = _tokenSplitter.Split(presentedToken);
    if (tokenParts is null)
      return AuthenticateResult.Fail("The device token is not in the form TokenLookupId.secret.");

    var owner = await _ownerLookup.FindByTokenAsync(tokenParts.TokenLookupId, tokenParts.Secret, Context.RequestAborted);

    if (owner?.Device is null)
      return AuthenticateResult.Fail("The device token was not accepted.");

    var device = owner.Device;
    ClaimsIdentity identity = new([
                                    new(ClaimTypes.NameIdentifier, owner.Id.ToString()),
                                    new(Names.DeviceClaims.DeviceId, device.Id.ToString()),
                                    new(Names.DeviceClaims.Language, device.Language)
                                  ],
                                  Scheme.Name);

    return AuthenticateResult.Success(new(new(identity), Scheme.Name));
  }

  private string? ReadPresentedToken()
  {
    var header = Request.Headers.Authorization.ToString();
    if (!string.IsNullOrWhiteSpace(header) && header.StartsWith(BearerPrefix, StringComparison.Ordinal))
      return header[BearerPrefix.Length..].Trim();

    if (Request.Path.StartsWithSegments(HubPathPrefix))
    {
      string? queryToken = Request.Query[AccessTokenQueryKey];
      if (!string.IsNullOrWhiteSpace(queryToken))
        return queryToken;
    }

    return null;
  }
}
