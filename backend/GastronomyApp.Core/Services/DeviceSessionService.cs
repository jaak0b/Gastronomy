using ErrorOr;
using GastronomyApp.Contracts.Session;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Refusals;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Core.Services;

public sealed class DeviceSessionService
{
  private readonly IGastronomyDatabase _database;
  private readonly TypeAdapterConfig _mapping;

  private readonly IReadOnlyCollection<string> _supportedLanguages =
  [
    "de",
    "en"
  ];

  public DeviceSessionService(IGastronomyDatabase database, TypeAdapterConfig mapping)
  {
    _database = database;
    _mapping = mapping;
  }

  public async Task<ErrorOr<SessionView>> ReadAsync(Guid deviceId, CancellationToken cancellationToken)
  {
    var staffSession = await _database.StaffMembers.AsNoTracking().Where(staffMember => staffMember.DeviceId == deviceId).ProjectToType<SessionView>(_mapping).FirstOrDefaultAsync(cancellationToken);

    if (staffSession is not null)
      return staffSession;

    var stationSession = await _database.Stations.AsNoTracking().Where(station => station.DeviceId == deviceId).ProjectToType<SessionView>(_mapping).FirstOrDefaultAsync(cancellationToken);

    if (stationSession is not null)
      return stationSession;

    return Refusal.Session.OwnerUnknown();
  }

  public async Task<ErrorOr<Success>> ChangeLanguageAsync(Guid deviceId, LanguageChangeRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    if (request.Language is null || !_supportedLanguages.Contains(request.Language, StringComparer.Ordinal))
      return Refusal.DeviceLanguage.UnsupportedLanguage(request.Language);

    var device = await _database.Devices.FirstOrDefaultAsync(candidate => candidate.Id == deviceId, cancellationToken);

    if (device is null)
      return Refusal.DeviceLanguage.DeviceNotFound(deviceId);

    device.Language = request.Language;
    await _database.SaveChangesAsync(cancellationToken);

    return Result.Success;
  }
}
