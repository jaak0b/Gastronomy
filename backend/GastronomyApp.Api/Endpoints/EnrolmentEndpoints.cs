using GastronomyApp.Api.Contracts;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Api.Hosting;
using GastronomyApp.Api.Hub;
using GastronomyApp.Api.RateLimiting;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure.Ports;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace GastronomyApp.Api.Endpoints;

public static class EnrolmentEndpoints
{
  public static IEndpointRouteBuilder MapEnrolmentEndpoints(this IEndpointRouteBuilder routes)
  {
    routes.MapPost("/api/enrolment/redeem",
                   async (RedeemEnrolmentRequest request,
                          HttpContext httpContext,
                          EnrolmentRedemptionHandler handler,
                          CancellationToken cancellationToken) => await handler.RedeemAsync(request, httpContext, cancellationToken))
          .AllowAnonymous()
          .RequireRateLimiting(new RateLimitPolicyNames().PerAddress);

    return routes;
  }
}

public sealed class EnrolmentRedemptionHandler
{
  private const string AcceptLanguageHeaderName = "Accept-Language";
  private readonly HubNotificationDispatcher _dispatcher;
  private readonly OutstandingInvitationCache _invitationCache;

  private readonly IEnrolmentInvitationStore _invitationStore;
  private readonly ILogger<EnrolmentRedemptionHandler> _log;
  private readonly ResultEnvelope _resultEnvelope;

  public EnrolmentRedemptionHandler(IEnrolmentInvitationStore invitationStore,
                                    HubNotificationDispatcher dispatcher,
                                    OutstandingInvitationCache invitationCache,
                                    ResultEnvelope resultEnvelope,
                                    ILogger<EnrolmentRedemptionHandler> log)
  {
    _invitationStore = invitationStore;
    _dispatcher = dispatcher;
    _invitationCache = invitationCache;
    _resultEnvelope = resultEnvelope;
    _log = log;
  }

  public async Task<IResult> RedeemAsync(RedeemEnrolmentRequest request,
                                         HttpContext httpContext,
                                         CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(request.Code))
    {
      _log.LogWarning("A phone asked to be set up without sending a code at all.");

      return _resultEnvelope.Problem(StatusCodes.Status400BadRequest,
                                    "ValidationFailed",
                                    "enrolment.codeMissing");
    }

    var redemption = await _invitationStore.RedeemAsync(new(request.Code,
                                                           request.Name?.Trim(),
                                                           request.UserAgent ?? string.Empty,
                                                           httpContext.Request.Headers[AcceptLanguageHeaderName].ToString()),
                                                       cancellationToken);

    return redemption.Outcome switch
           {
             EnrolmentRedemptionOutcome.Redeemed => await CompletedAsync(redemption, cancellationToken),
             EnrolmentRedemptionOutcome.CodeInvalid => Refused(redemption,
                                                               "the code sent does not match the invitation that is outstanding",
                                                               StatusCodes.Status404NotFound,
                                                               "EnrolmentCodeUnknown",
                                                               "enrolment.codeUnknown"),
             EnrolmentRedemptionOutcome.CodeExpired => Refused(redemption,
                                                               "the outstanding invitation had already expired",
                                                               StatusCodes.Status410Gone,
                                                               "EnrolmentCodeNoLongerValid",
                                                               "enrolment.codeNoLongerValid"),
             EnrolmentRedemptionOutcome.NoInvitationOutstanding =>
               Refused(redemption,
                       "no invitation is outstanding, so the last one was already used or was replaced by a newer one",
                       StatusCodes.Status410Gone,
                       "EnrolmentCodeNoLongerValid",
                       "enrolment.codeNoLongerValid"),
             EnrolmentRedemptionOutcome.StaffMemberIsOffTheList => Refused(redemption,
                                                                           "the person the invitation names is off the list",
                                                                           StatusCodes.Status410Gone,
                                                                           "StaffMemberIsOffTheList",
                                                                           "enrolment.staffMemberIsOffTheList"),
             EnrolmentRedemptionOutcome.NameRequired => Refused(redemption,
                                                                "the invitation names nobody and the phone sent no name",
                                                                StatusCodes.Status400BadRequest,
                                                                "ValidationFailed",
                                                                "enrolment.nameMissing"),
             _ => new Never().OfType<IResult>(redemption.Outcome)
           };
  }

  private IResult Refused(EnrolmentRedemptionResult redemption,
                          string reason,
                          int statusCode,
                          string code,
                          string messageKey)
  {
    _log.LogWarning("Enrolment refused for invitation {InvitationId}, because {Reason}.",
                    redemption.InvitationId,
                    reason);

    return _resultEnvelope.Problem(statusCode, code, messageKey);
  }

  private async Task<IResult> CompletedAsync(EnrolmentRedemptionResult redemption,
                                             CancellationToken cancellationToken)
  {
    _invitationCache.Forget();

    var device = redemption.Device!;
    var staffMember = redemption.StaffMember!;

    _log.LogInformation("Enrolment invitation {InvitationId} was redeemed. Device {DeviceId} now belongs to "
                        + "staff member {StaffMemberId}.",
                        redemption.InvitationId,
                        device.Id,
                        staffMember.Id);

    await _dispatcher.PushEnrolmentCompletedAsync(new(staffMember.Id, staffMember.Name, device.Id),
                                                 cancellationToken);

    return Results.Ok(new RedeemedEnrolmentView(device.Id,
                                                redemption.PlaintextToken!,
                                                new(staffMember.Id, staffMember.Name),
                                                device.Language));
  }
}
