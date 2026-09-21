using ErrorOr;
using GastronomyApp.Api.Announcers;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Contracts.Admin.Festivals;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Services;
using Microsoft.AspNetCore.Http;

namespace GastronomyApp.Api.Handlers;

public sealed class AdminFestivalHandler
{
  private readonly FestivalChangeAnnouncer _announcer;
  private readonly ResultEnvelope _resultEnvelope;
  private readonly FestivalAdministrationService _service;

  public AdminFestivalHandler(FestivalAdministrationService service, FestivalChangeAnnouncer announcer, ResultEnvelope resultEnvelope)
  {
    _service = service;
    _announcer = announcer;
    _resultEnvelope = resultEnvelope;
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

    return await _service.CreateAsync(request.Name, request.StartsAtUtc, request.EndsAtUtc, cancellationToken)
                         .ThenDoAsync(festival => _announcer.AnnounceAsync())
                         .Match(festival => Results.Json(new SavedFestivalView(festival.Id), statusCode: StatusCodes.Status201Created), _resultEnvelope.Refuse);
  }

  public async Task<IResult> UpdateAsync(Guid festivalId, SaveFestivalRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    return await _service.UpdateAsync(festivalId, request.Name, request.StartsAtUtc, request.EndsAtUtc, cancellationToken)
                         .ThenDoAsync(festival => _announcer.AnnounceAsync())
                         .Match(festival => Results.Ok(new SavedFestivalView(festival.Id)), _resultEnvelope.Refuse);
  }

  public async Task<IResult> CopyAsync(Guid festivalId, SaveFestivalRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    return await _service.CopyAsync(festivalId, request.Name, request.StartsAtUtc, request.EndsAtUtc, cancellationToken)
                         .ThenDoAsync(festival => _announcer.AnnounceAsync())
                         .Match(festival => Results.Json(new SavedFestivalView(festival.Id), statusCode: StatusCodes.Status201Created), _resultEnvelope.Refuse);
  }

  public async Task<IResult> HideAsync(Guid festivalId, CancellationToken cancellationToken)
  {
    return await _service.HideAsync(festivalId, cancellationToken)
                         .ThenDoAsync(festival => _announcer.AnnounceAsync())
                         .Match(festival => Results.Ok(new SavedFestivalView(festival.Id)), _resultEnvelope.Refuse);
  }

  public async Task<IResult> ShowAsync(Guid festivalId, CancellationToken cancellationToken)
  {
    return await _service.ShowAsync(festivalId, cancellationToken)
                         .ThenDoAsync(festival => _announcer.AnnounceAsync())
                         .Match(festival => Results.Ok(new SavedFestivalView(festival.Id)), _resultEnvelope.Refuse);
  }

  private AdminFestivalView BuildFestivalView(Festival festival, IReadOnlyDictionary<Guid, int> orderCounts)
  {
    return new(festival.Id, festival.Name, festival.StartsAtUtc, festival.EndsAtUtc, festival.IsHidden, _service.IsRunning(festival), festival.StationCount(), festival.MenuItemCount(), orderCounts.GetValueOrDefault(festival.Id));
  }
}
