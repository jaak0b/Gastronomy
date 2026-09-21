using ErrorOr;
using GastronomyApp.Contracts.Stations;
using GastronomyApp.Contracts.Session;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Api.Values;
using GastronomyApp.Contracts.Admin.Staff;
using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Services;
using Microsoft.AspNetCore.Http;

namespace GastronomyApp.Api.Handlers;

public sealed class SessionHandler
{
  private readonly DeviceLanguageService _languageService;
  private readonly IDeviceOwnerStore _ownerStore;
  private readonly ResultEnvelope _resultEnvelope;

  public SessionHandler(IDeviceOwnerStore ownerStore, DeviceLanguageService languageService, ResultEnvelope resultEnvelope)
  {
    _ownerStore = ownerStore;
    _languageService = languageService;
    _resultEnvelope = resultEnvelope;
  }

  public async Task<IResult> ReadAsync(DeviceCaller caller, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(caller);

    var owner = await _ownerStore.FindAsync(caller.OwnerKind, caller.OwnerId, cancellationToken);

    if (owner is null)
      return Results.Unauthorized();

    return Results.Ok(new SessionView(caller.DeviceId, caller.OwnerKind, BuildStaffMemberView(caller, owner), BuildStationSummaryView(caller, owner), caller.Language));
  }

  private StaffMemberView? BuildStaffMemberView(DeviceCaller caller, IDeviceOwner owner)
  {
    if (caller.OwnerKind != DeviceOwnerKind.StaffMember)
      return null;

    return new(caller.OwnerId, owner.Name);
  }

  private StationSummaryView? BuildStationSummaryView(DeviceCaller caller, IDeviceOwner owner)
  {
    if (caller.OwnerKind != DeviceOwnerKind.Station)
      return null;

    return new(caller.OwnerId, owner.Name);
  }

  public async Task<IResult> ChangeLanguageAsync(LanguageChangeRequest request, DeviceCaller caller, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);
    ArgumentNullException.ThrowIfNull(caller);

    return await _languageService.ChangeAsync(caller.DeviceId, request.Language, cancellationToken)
                                 .Match(device => Results.NoContent(), _resultEnvelope.Refuse);
  }

}
