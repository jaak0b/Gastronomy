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
  private readonly ResultEnvelope _resultEnvelope;

  public EnrolmentRedemptionHandler(IEnrolmentInvitationStore invitationStore,
                                    HubNotificationDispatcher dispatcher,
                                    OutstandingInvitationCache invitationCache,
                                    ResultEnvelope resultEnvelope)
  {
    _invitationStore = invitationStore;
    _dispatcher = dispatcher;
    _invitationCache = invitationCache;
    _resultEnvelope = resultEnvelope;
  }

  public async Task<IResult> RedeemAsync(RedeemEnrolmentRequest request,
                                         HttpContext httpContext,
                                         CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(request.Code))
    {
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
             EnrolmentRedemptionOutcome.CodeInvalid => _resultEnvelope.Problem(StatusCodes.Status404NotFound,
                                                                              "EnrolmentCodeUnknown",
                                                                              "enrolment.codeUnknown"),
             EnrolmentRedemptionOutcome.CodeExpired => _resultEnvelope.Problem(StatusCodes.Status410Gone,
                                                                              "EnrolmentCodeNoLongerValid",
                                                                              "enrolment.codeNoLongerValid"),
             EnrolmentRedemptionOutcome.StaffMemberIsOffTheList => _resultEnvelope.Problem(StatusCodes.Status410Gone,
                                                                                          "StaffMemberIsOffTheList",
                                                                                          "enrolment.staffMemberIsOffTheList"),
             EnrolmentRedemptionOutcome.NameRequired => _resultEnvelope.Problem(StatusCodes.Status400BadRequest,
                                                                               "ValidationFailed",
                                                                               "enrolment.nameMissing"),
             _ => new Never().OfType<IResult>(redemption.Outcome)
           };
  }

  private async Task<IResult> CompletedAsync(EnrolmentRedemptionResult redemption,
                                             CancellationToken cancellationToken)
  {
    _invitationCache.Forget();

    var device = redemption.Device!;
    var staffMember = redemption.StaffMember!;

    await _dispatcher.PushEnrolmentCompletedAsync(new(staffMember.Id, staffMember.Name, device.Id),
                                                 cancellationToken);

    return Results.Ok(new RedeemedEnrolmentView(device.Id,
                                                redemption.PlaintextToken!,
                                                new(staffMember.Id, staffMember.Name),
                                                device.Language));
  }
}
