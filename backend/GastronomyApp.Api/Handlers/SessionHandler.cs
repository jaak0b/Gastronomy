using ErrorOr;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Api.Values;
using GastronomyApp.Contracts.Session;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Services;
using MapsterMapper;
using Microsoft.AspNetCore.Http;

namespace GastronomyApp.Api.Handlers;

public sealed class SessionHandler
{
  private readonly DeviceLanguageService _languageService;
  private readonly IMapper _mapper;
  private readonly ResultEnvelope _resultEnvelope;
  private readonly SessionService _sessionService;

  public SessionHandler(SessionService sessionService, DeviceLanguageService languageService, ResultEnvelope resultEnvelope, IMapper mapper)
  {
    _sessionService = sessionService;
    _languageService = languageService;
    _resultEnvelope = resultEnvelope;
    _mapper = mapper;
  }

  public async Task<IResult> ReadAsync(DeviceCaller caller, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(caller);

    return await _sessionService.ReadOwnerAsync(caller.OwnerKind, caller.OwnerId, cancellationToken).Match(owner => Results.Ok(BuildSessionView(owner, caller)), _resultEnvelope.Refuse);
  }

  public async Task<IResult> ChangeLanguageAsync(LanguageChangeRequest request, DeviceCaller caller, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);
    ArgumentNullException.ThrowIfNull(caller);

    return await _languageService.ChangeAsync(caller.DeviceId, request.Language, cancellationToken).Match(device => Results.NoContent(), _resultEnvelope.Refuse);
  }

  private SessionView BuildSessionView(IDeviceOwner owner, DeviceCaller caller)
  {
    return _mapper.Map<IDeviceOwner, SessionView>(owner) with
           {
             DeviceId = caller.DeviceId,
             Language = caller.Language
           };
  }
}
