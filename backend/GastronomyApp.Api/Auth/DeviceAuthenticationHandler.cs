using System.Security.Claims;
using System.Text.Encodings.Web;
using GastronomyApp.Core.Entities;
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

    private readonly IDeviceTokenStore deviceTokenStore;
    private readonly DeviceClaimTypes claimTypes = new();

    public DeviceAuthenticationHandler(
        IOptionsMonitor<DeviceAuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IDeviceTokenStore deviceTokenStore)
        : base(options, logger, encoder)
    {
        this.deviceTokenStore = deviceTokenStore;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? presentedToken = ReadPresentedToken();
        if (presentedToken is null)
        {
            return AuthenticateResult.NoResult();
        }

        int separatorIndex = presentedToken.IndexOf('.');
        if (separatorIndex <= 0 || separatorIndex == presentedToken.Length - 1)
        {
            return AuthenticateResult.Fail("The device token is not in the form TokenLookupId.secret.");
        }

        string tokenLookupId = presentedToken[..separatorIndex];
        string secret = presentedToken[(separatorIndex + 1)..];

        DeviceVerificationResult verification =
            await deviceTokenStore.VerifyAsync(tokenLookupId, secret, Context.RequestAborted);

        if (!verification.IsValid || verification.Device is null)
        {
            return AuthenticateResult.Fail("The device token was not accepted.");
        }

        Device device = verification.Device;
        ClaimsIdentity identity = new(
            [
                new Claim(ClaimTypes.NameIdentifier, device.StaffMemberId.ToString()),
                new Claim(claimTypes.DeviceId, device.Id.ToString()),
                new Claim(claimTypes.Language, device.Language),
            ],
            Scheme.Name);

        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
    }

    private string? ReadPresentedToken()
    {
        string? header = Request.Headers.Authorization.ToString();
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
