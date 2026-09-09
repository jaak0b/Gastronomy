using System.Security.Claims;
using System.Text.Encodings.Web;
using GastronomyApp.Infrastructure.Ports;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GastronomyApp.Api.Auth;

public sealed class DeviceAuthenticationHandler : AuthenticationHandler<DeviceAuthenticationSchemeOptions>
{
  private const string BearerPrefix = "Bearer ";
  private const string AccessTokenQueryKey = "access_token";
  private const string HubPathPrefix = "/hub";
  private readonly DeviceClaimTypes _claimTypes = new();

  private readonly IDeviceTokenStore _deviceTokenStore;
  private readonly DeviceTokenSplitter _tokenSplitter = new();

  public DeviceAuthenticationHandler(IOptionsMonitor<DeviceAuthenticationSchemeOptions> options,
                                     ILoggerFactory logger,
                                     UrlEncoder encoder,
                                     IDeviceTokenStore deviceTokenStore)
    : base(options, logger, encoder)
  {
    _deviceTokenStore = deviceTokenStore;
  }

  override protected async Task<AuthenticateResult> HandleAuthenticateAsync()
  {
    var presentedToken = ReadPresentedToken();
    if (presentedToken is null)
    {
      return AuthenticateResult.NoResult();
    }

    var tokenParts = _tokenSplitter.Split(presentedToken);
    if (tokenParts is null)
    {
      return AuthenticateResult.Fail("The device token is not in the form TokenLookupId.secret.");
    }

    var verification =
      await _deviceTokenStore.VerifyAsync(tokenParts.TokenLookupId, tokenParts.Secret, Context.RequestAborted);

    if (!verification.IsValid || verification.Device is null || verification.Owner is null)
    {
      return AuthenticateResult.Fail("The device token was not accepted.");
    }

    var device = verification.Device;
    var owner = verification.Owner;
    ClaimsIdentity identity = new([
                                    new(ClaimTypes.NameIdentifier, owner.Id.ToString()),
                                    new(_claimTypes.OwnerKind, owner.Kind.ToString()),
                                    new(_claimTypes.DeviceId, device.Id.ToString()),
                                    new(_claimTypes.Language, device.Language)
                                  ],
                                  Scheme.Name);

    return AuthenticateResult.Success(new(new(identity), Scheme.Name));
  }

  private string? ReadPresentedToken()
  {
    var header = Request.Headers.Authorization.ToString();
    if (!string.IsNullOrWhiteSpace(header) && header.StartsWith(BearerPrefix, StringComparison.Ordinal))
    {
      return header[BearerPrefix.Length..].Trim();
    }

    if (Request.Path.StartsWithSegments(HubPathPrefix))
    {
      string? queryToken = Request.Query[AccessTokenQueryKey];
      if (!string.IsNullOrWhiteSpace(queryToken))
      {
        return queryToken;
      }
    }

    return null;
  }
}
