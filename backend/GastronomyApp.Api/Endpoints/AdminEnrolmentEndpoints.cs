using GastronomyApp.Api.Contracts;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Api.Hosting;
using GastronomyApp.Core.Enums;
using GastronomyApp.Infrastructure.Ports;
using GastronomyApp.Infrastructure.Repositories;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace GastronomyApp.Api.Endpoints;

public static class AdminEnrolmentEndpoints
{
  public static IEndpointRouteBuilder MapAdminEnrolmentEndpoints(this IEndpointRouteBuilder routes)
  {
    routes.MapPost("/api/admin/enrolment/invitations",
                   async (CreateInvitationRequest request,
                          AdminEnrolmentHandler handler,
                          CancellationToken cancellationToken) => await handler.CreateInvitationAsync(request, cancellationToken));

    return routes;
  }
}

public sealed class AdminEnrolmentHandler
{
  private readonly DeviceRevoker _deviceRevoker;
  private readonly OutstandingInvitationCache _invitationCache;
  private readonly IEnrolmentInvitationStore _invitationStore;
  private readonly ILogger<AdminEnrolmentHandler> _log;
  private readonly DeviceOwnerStore _ownerStore;
  private readonly ResultEnvelope _resultEnvelope;
  private readonly EnrolmentUrlBuilder _urlBuilder;

  public AdminEnrolmentHandler(IEnrolmentInvitationStore invitationStore,
                               DeviceOwnerStore ownerStore,
                               EnrolmentUrlBuilder urlBuilder,
                               OutstandingInvitationCache invitationCache,
                               DeviceRevoker deviceRevoker,
                               ResultEnvelope resultEnvelope,
                               ILogger<AdminEnrolmentHandler> log)
  {
    _invitationStore = invitationStore;
    _ownerStore = ownerStore;
    _urlBuilder = urlBuilder;
    _invitationCache = invitationCache;
    _deviceRevoker = deviceRevoker;
    _resultEnvelope = resultEnvelope;
    _log = log;
  }

  public async Task<IResult> CreateInvitationAsync(CreateInvitationRequest request,
                                                   CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    if (request.StaffMemberId is not null && request.StationId is not null)
    {
      return _resultEnvelope.Problem(StatusCodes.Status400BadRequest,
                                    "ValidationFailed",
                                    "enrolment.atMostOneOwner");
    }

    var owner = OwnerOf(request);
    var ownerRecord = owner is null ? null : await _ownerStore.FindAsync(owner, cancellationToken);

    if (owner is not null && ownerRecord is null)
    {
      return Results.NotFound();
    }

    var deviceToReplace = ownerRecord?.DeviceId;

    var created = await _invitationStore.CreateAsync(owner, cancellationToken);
    var qrUrl = _urlBuilder.BuildEnrolmentUrl(created.QrCodeValue);
    _invitationCache.Remember(new(created.InvitationId, created.QrCodeValue, qrUrl, created.ExpiresAtUtc));

    _log.LogInformation("Enrolment invitation {InvitationId} was created for the {OwnerKind} {OwnerId} "
                        + "at {Origin}, and is valid until {ExpiresAtUtc}. A missing owner means a waiter "
                        + "who types their name when they scan it.",
                        created.InvitationId,
                        owner?.Kind,
                        owner?.Id,
                        _urlBuilder.Origin(),
                        created.ExpiresAtUtc);

    if (deviceToReplace is not null)
    {
      await _deviceRevoker.RevokeAsync(deviceToReplace.Value, cancellationToken);
    }

    return Results.Json(new InvitationView(created.InvitationId,
                                           qrUrl,
                                           created.ExpiresAtUtc,
                                           owner?.Kind,
                                           owner?.Kind == DeviceOwnerKind.StaffMember
                                             ? new StaffMemberView(owner.Id, ownerRecord!.Name)
                                             : null,
                                           owner?.Kind == DeviceOwnerKind.Station
                                             ? new StationSummaryView(owner.Id, ownerRecord!.Name)
                                             : null,
                                           _urlBuilder.ReachableAddresses()),
                        statusCode: StatusCodes.Status201Created);
  }

  private DeviceOwner? OwnerOf(CreateInvitationRequest request)
  {
    if (request.StaffMemberId is not null)
    {
      return new(DeviceOwnerKind.StaffMember, request.StaffMemberId.Value);
    }

    if (request.StationId is not null)
    {
      return new(DeviceOwnerKind.Station, request.StationId.Value);
    }

    return null;
  }
}
