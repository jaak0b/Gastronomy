using GastronomyApp.Api.Announcers;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Contracts.Admin.Festivals;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace GastronomyApp.Api.Handlers;

public sealed class AdminFestivalHandler
{
  private readonly FestivalChangeAnnouncer _announcer;
  private readonly ILogger<AdminFestivalHandler> _logger;
  private readonly ResultEnvelope _resultEnvelope;
  private readonly FestivalAdministrationService _service;

  public AdminFestivalHandler(FestivalAdministrationService service, FestivalChangeAnnouncer announcer, ResultEnvelope resultEnvelope, ILogger<AdminFestivalHandler> logger)
  {
    _service = service;
    _announcer = announcer;
    _resultEnvelope = resultEnvelope;
    _logger = logger;
  }

  public async Task<IResult> ListAsync(CancellationToken cancellationToken)
  {
    IReadOnlyList<Festival> festivals = await _service.ListAsync(cancellationToken);
    IReadOnlyDictionary<Guid, int> orderCounts = await _service.CountOrdersByFestivalAsync(cancellationToken);

    return Results.Ok(new AdminFestivalListView(festivals.Select(festival => BuildFestivalView(festival, orderCounts)).ToList()));
  }

  public async Task<IResult> CreateAsync(SaveFestivalRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    Result<Festival, FestivalAdministrationFailure> created = await _service.CreateAsync(request.Name, request.StartsAtUtc, request.EndsAtUtc, cancellationToken);

    if (!created.IsSuccess)
      return RefusalFor(created.Failure);

    await _announcer.AnnounceAsync();

    return Results.Json(new SavedFestivalView(created.Value.Id), statusCode: StatusCodes.Status201Created);
  }

  public async Task<IResult> UpdateAsync(Guid festivalId, SaveFestivalRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    Result<Festival, FestivalAdministrationFailure> updated = await _service.UpdateAsync(festivalId, request.Name, request.StartsAtUtc, request.EndsAtUtc, cancellationToken);

    if (!updated.IsSuccess)
      return RefusalFor(updated.Failure);

    await _announcer.AnnounceAsync();

    return Results.Ok(new SavedFestivalView(updated.Value.Id));
  }

  public async Task<IResult> CopyAsync(Guid festivalId, SaveFestivalRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    Result<Festival, FestivalAdministrationFailure> copied = await _service.CopyAsync(festivalId, request.Name, request.StartsAtUtc, request.EndsAtUtc, cancellationToken);

    if (!copied.IsSuccess)
      return RefusalFor(copied.Failure);

    await _announcer.AnnounceAsync();

    return Results.Json(new SavedFestivalView(copied.Value.Id), statusCode: StatusCodes.Status201Created);
  }

  public async Task<IResult> HideAsync(Guid festivalId, CancellationToken cancellationToken)
  {
    Result<Festival?, FestivalAdministrationFailure> hidden = await _service.HideAsync(festivalId, cancellationToken);

    return await AnsweredAsync(hidden, festivalId);
  }

  public async Task<IResult> ShowAsync(Guid festivalId, CancellationToken cancellationToken)
  {
    Result<Festival?, FestivalAdministrationFailure> shown = await _service.ShowAsync(festivalId, cancellationToken);

    return await AnsweredAsync(shown, festivalId);
  }

  private async Task<IResult> AnsweredAsync(Result<Festival?, FestivalAdministrationFailure> written, Guid festivalId)
  {
    if (!written.IsSuccess)
      return RefusalFor(written.Failure);

    if (written.Value is not null)
      await _announcer.AnnounceAsync();

    return Results.Ok(new SavedFestivalView(festivalId));
  }

  private AdminFestivalView BuildFestivalView(Festival festival, IReadOnlyDictionary<Guid, int> orderCounts)
  {
    return new(festival.Id, festival.Name, festival.StartsAtUtc, festival.EndsAtUtc, festival.IsHidden, _service.IsRunning(festival), festival.StationCount(), festival.MenuItemCount(), orderCounts.GetValueOrDefault(festival.Id));
  }

  private IResult RefusalFor(FestivalAdministrationFailure failure)
  {
    return failure.Reason switch
           {
             FestivalAdministrationFailureReason.FestivalNotFound => Results.NotFound(),
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
