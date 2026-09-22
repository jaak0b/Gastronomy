using ErrorOr;
using GastronomyApp.Api.Answers;
using GastronomyApp.Api.Values;
using GastronomyApp.Contracts.Session;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Services;
using MapsterMapper;

namespace GastronomyApp.Api.Handlers;

public sealed class SessionHandler
{
  private readonly DeviceLanguageService _languageService;
  private readonly IMapper _mapper;
  private readonly SessionService _sessionService;

  public SessionHandler(SessionService sessionService, DeviceLanguageService languageService, IMapper mapper)
  {
    _sessionService = sessionService;
    _languageService = languageService;
    _mapper = mapper;
  }

  public async Task<ApiAnswer<SessionView>> ReadAsync(DeviceCaller caller, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(caller);

    return await _sessionService.ReadOwnerAsync(caller.OwnerKind, caller.OwnerId, cancellationToken).Then(owner => BuildSessionView(owner, caller));
  }

  public async Task<NoContentAnswer> ChangeLanguageAsync(LanguageChangeRequest request, DeviceCaller caller, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(caller);

    return await _languageService.ChangeAsync(caller.DeviceId, request.Language, cancellationToken).Then(device => Result.Success);
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
