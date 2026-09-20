using GastronomyApp.Api.Auth.Callers;
using GastronomyApp.Api.Contracts;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Core.Enums;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.ReadModels;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using Microsoft.AspNetCore.Http;
using GastronomyApp.Api.Values;

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

    var owner = await _ownerStore.FindAsync(new(caller.OwnerKind, caller.OwnerId), cancellationToken);

    if (owner is null)
      return Results.Unauthorized();

    return Results.Ok(new SessionView(caller.DeviceId, caller.OwnerKind, BuildStaffMemberView(caller, owner), BuildStationSummaryView(caller, owner), caller.Language));
  }

  private StaffMemberView? BuildStaffMemberView(DeviceCaller caller, DeviceOwnerRecord owner)
  {
    if (caller.OwnerKind != DeviceOwnerKind.StaffMember)
      return null;

    return new(caller.OwnerId, owner.Name);
  }

  private StationSummaryView? BuildStationSummaryView(DeviceCaller caller, DeviceOwnerRecord owner)
  {
    if (caller.OwnerKind != DeviceOwnerKind.Station)
      return null;

    return new(caller.OwnerId, owner.Name);
  }

  public async Task<IResult> ChangeLanguageAsync(LanguageChangeRequest request, DeviceCaller caller, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);
    ArgumentNullException.ThrowIfNull(caller);

    Result<ChangedDeviceLanguage, Failure<DeviceLanguageFailureReason>> changed = await _languageService.ChangeAsync(caller.DeviceId, request.Language, cancellationToken);

    if (changed.IsSuccess)
      return Results.NoContent();

    return changed.Failure.Reason switch
           {
             DeviceLanguageFailureReason.UnsupportedLanguage => _resultEnvelope.Problem(StatusCodes.Status400BadRequest, "ValidationFailed", "session.unsupportedLanguage"),
             DeviceLanguageFailureReason.DeviceNotFound => Results.Unauthorized(),
             _ => new UnreachableCase().Throw<IResult>(changed.Failure.Reason)
           };
  }
}
