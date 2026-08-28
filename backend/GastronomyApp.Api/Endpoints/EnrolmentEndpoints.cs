using GastronomyApp.Api.Contracts;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Api.Hosting;
using GastronomyApp.Api.Hub;
using GastronomyApp.Api.RateLimiting;
using GastronomyApp.Core.Entities;
using GastronomyApp.Infrastructure.Ports;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace GastronomyApp.Api.Endpoints;

public static class EnrolmentEndpoints
{
  public static IEndpointRouteBuilder MapEnrolmentEndpoints(this IEndpointRouteBuilder routes)
  {
    routes.MapPost("/api/enrolment/redeem", async (
        RedeemEnrolmentRequest request,
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

  private readonly IEnrolmentInvitationStore invitationStore;
  private readonly HubNotificationDispatcher dispatcher;
  private readonly OutstandingInvitationCache invitationCache;
  private readonly ResultEnvelope resultEnvelope;

  public EnrolmentRedemptionHandler(
      IEnrolmentInvitationStore invitationStore,
      HubNotificationDispatcher dispatcher,
      OutstandingInvitationCache invitationCache,
      ResultEnvelope resultEnvelope)
  {
    this.invitationStore = invitationStore;
    this.dispatcher = dispatcher;
    this.invitationCache = invitationCache;
    this.resultEnvelope = resultEnvelope;
  }

  public async Task<IResult> RedeemAsync(
      RedeemEnrolmentRequest request,
      HttpContext httpContext,
      CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(request.Code))
    {
      return resultEnvelope.Problem(
          StatusCodes.Status400BadRequest,
          "ValidationFailed",
          "enrolment.codeMissing");
    }

    EnrolmentRedemptionResult redemption = await invitationStore.RedeemAsync(
        new EnrolmentRedemptionRequest(
            request.Code,
            request.Name?.Trim(),
            request.UserAgent ?? string.Empty,
            httpContext.Request.Headers[AcceptLanguageHeaderName].ToString()),
        cancellationToken);

    return redemption.Outcome switch
    {
      EnrolmentRedemptionOutcome.Redeemed => await CompletedAsync(redemption, cancellationToken),
      EnrolmentRedemptionOutcome.CodeInvalid => resultEnvelope.Problem(
          StatusCodes.Status404NotFound,
          "EnrolmentCodeUnknown",
          "enrolment.codeUnknown"),
      EnrolmentRedemptionOutcome.CodeExpired => resultEnvelope.Problem(
          StatusCodes.Status410Gone,
          "EnrolmentCodeNoLongerValid",
          "enrolment.codeNoLongerValid"),
      EnrolmentRedemptionOutcome.StaffMemberIsOffTheList => resultEnvelope.Problem(
          StatusCodes.Status410Gone,
          "StaffMemberIsOffTheList",
          "enrolment.staffMemberIsOffTheList"),
      EnrolmentRedemptionOutcome.NameRequired => resultEnvelope.Problem(
          StatusCodes.Status400BadRequest,
          "ValidationFailed",
          "enrolment.nameMissing"),
      _ => new GastronomyApp.Core.Services.Never().OfType<IResult>(redemption.Outcome),
    };
  }

  private async Task<IResult> CompletedAsync(
      EnrolmentRedemptionResult redemption,
      CancellationToken cancellationToken)
  {
    invitationCache.Forget();

    Device device = redemption.Device!;
    StaffMember staffMember = redemption.StaffMember!;

    await dispatcher.PushEnrolmentCompletedAsync(
        new EnrolmentCompletedEvent(staffMember.Id, staffMember.Name, device.Id),
        cancellationToken);

    return Results.Ok(new RedeemedEnrolmentView(
        device.Id,
        redemption.PlaintextToken!,
        new StaffMemberView(staffMember.Id, staffMember.Name),
        device.Language));
  }
}
