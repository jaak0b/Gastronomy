using GastronomyApp.Api.Announcers;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Contracts;
using GastronomyApp.Core.ReadModels;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using MapsterMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace GastronomyApp.Api.Handlers;

public sealed class AdminFestivalHandler
{
  private readonly FestivalChangeAnnouncer _announcer;
  private readonly ILogger<AdminFestivalHandler> _logger;
  private readonly IMapper _mapper;
  private readonly ResultEnvelope _resultEnvelope;
  private readonly FestivalAdministrationService _service;

  public AdminFestivalHandler(FestivalAdministrationService service, FestivalChangeAnnouncer announcer, ResultEnvelope resultEnvelope, ILogger<AdminFestivalHandler> logger, IMapper mapper)
  {
    _service = service;
    _announcer = announcer;
    _resultEnvelope = resultEnvelope;
    _logger = logger;
    _mapper = mapper;
  }

  public async Task<IResult> ListAsync(CancellationToken cancellationToken)
  {
    IReadOnlyList<AdministeredFestival> festivals = await _service.ListAsync(cancellationToken);

    return Results.Ok(new AdminFestivalListView(_mapper.Map<IReadOnlyList<AdminFestivalView>>(festivals)));
  }

  public async Task<IResult> CreateAsync(SaveFestivalRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    Result<SavedFestival, FestivalAdministrationFailure> created = await _service.CreateAsync(request.Name, request.StartsAtUtc, request.EndsAtUtc, cancellationToken);

    return await AnsweredAsync(created, festivalId => Results.Json(new SavedFestivalView(festivalId), statusCode: StatusCodes.Status201Created));
  }

  public async Task<IResult> UpdateAsync(Guid festivalId, SaveFestivalRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    Result<SavedFestival, FestivalAdministrationFailure> updated = await _service.UpdateAsync(festivalId, request.Name, request.StartsAtUtc, request.EndsAtUtc, cancellationToken);

    return await AnsweredAsync(updated, savedFestivalId => Results.Ok(new SavedFestivalView(savedFestivalId)));
  }

  public async Task<IResult> CopyAsync(Guid festivalId, SaveFestivalRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    Result<SavedFestival, FestivalAdministrationFailure> copied = await _service.CopyAsync(festivalId, request.Name, request.StartsAtUtc, request.EndsAtUtc, cancellationToken);

    return await AnsweredAsync(copied, newFestivalId => Results.Json(new SavedFestivalView(newFestivalId), statusCode: StatusCodes.Status201Created));
  }

  public async Task<IResult> HideAsync(Guid festivalId, CancellationToken cancellationToken)
  {
    Result<SavedFestival, FestivalAdministrationFailure> hidden = await _service.HideAsync(festivalId, cancellationToken);

    return await AnsweredAsync(hidden, savedFestivalId => Results.Ok(new SavedFestivalView(savedFestivalId)));
  }

  public async Task<IResult> ShowAsync(Guid festivalId, CancellationToken cancellationToken)
  {
    Result<SavedFestival, FestivalAdministrationFailure> shown = await _service.ShowAsync(festivalId, cancellationToken);

    return await AnsweredAsync(shown, savedFestivalId => Results.Ok(new SavedFestivalView(savedFestivalId)));
  }

  private async Task<IResult> AnsweredAsync(Result<SavedFestival, FestivalAdministrationFailure> written, Func<Guid, IResult> buildResponse)
  {
    if (!written.IsSuccess)
      return RefusalFor(written.Failure);

    if (written.Value.SomethingChanged)
      await _announcer.AnnounceAsync();

    return buildResponse(written.Value.FestivalId);
  }

  private IResult RefusalFor(FestivalAdministrationFailure failure)
  {
    return failure.Reason switch
           {
             FestivalAdministrationFailureReason.FestivalNotFound => Results.NotFound(),
             FestivalAdministrationFailureReason.NameMissing => _resultEnvelope.Problem(StatusCodes.Status400BadRequest, "ValidationFailed", "admin.festivalNameMissing"),
             FestivalAdministrationFailureReason.PeriodInvalid => _resultEnvelope.Problem(StatusCodes.Status400BadRequest, "ValidationFailed", "admin.festivalPeriodInvalid"),
             FestivalAdministrationFailureReason.PeriodOverlapsAnotherFestival => _resultEnvelope.Problem(StatusCodes.Status409Conflict, "FestivalOverlaps", "admin.festivalOverlaps", new Dictionary<string, string> { ["name"] = failure.OverlappingFestivalName ?? string.Empty }),
             FestivalAdministrationFailureReason.FestivalIsRunning => RefusedHidingARunningFestival(failure.OffendingFestivalId),
             _ => new UnreachableCase().Throw<IResult>(failure.Reason)
           };
  }

  private IResult RefusedHidingARunningFestival(Guid? offendingFestivalId)
  {
    _logger.LogWarning("The festival {FestivalId} was not hidden because it is running right now, and hiding it would empty every phone and every station tablet in the middle of service. The festivals page draws no hide control on a running festival, so this call did not come from that screen.",
                       offendingFestivalId);

    return _resultEnvelope.Problem(StatusCodes.Status400BadRequest, "ValidationFailed", "admin.actionFailed");
  }
}
