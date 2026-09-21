using ErrorOr;
using GastronomyApp.Api.ErrorHandling;
using GastronomyApp.Contracts.Admin.Festivals;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Services;
using MapsterMapper;
using Microsoft.AspNetCore.Http;

namespace GastronomyApp.Api.Handlers;

public sealed class AdminFestivalHandler
{
  private readonly IMapper _mapper;
  private readonly ResultEnvelope _resultEnvelope;
  private readonly FestivalAdministrationService _service;

  public AdminFestivalHandler(FestivalAdministrationService service, ResultEnvelope resultEnvelope, IMapper mapper)
  {
    _service = service;
    _resultEnvelope = resultEnvelope;
    _mapper = mapper;
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

    return await _service.CreateAsync(request.Name, request.StartsAtUtc, request.EndsAtUtc, cancellationToken).Match(festival => Results.Json(new SavedFestivalView(festival.Id), statusCode: StatusCodes.Status201Created), _resultEnvelope.Refuse);
  }

  public async Task<IResult> UpdateAsync(Guid festivalId, SaveFestivalRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    return await _service.UpdateAsync(festivalId, request.Name, request.StartsAtUtc, request.EndsAtUtc, cancellationToken).Match(festival => Results.Ok(new SavedFestivalView(festival.Id)), _resultEnvelope.Refuse);
  }

  public async Task<IResult> CopyAsync(Guid festivalId, SaveFestivalRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    return await _service.CopyAsync(festivalId, request.Name, request.StartsAtUtc, request.EndsAtUtc, cancellationToken).Match(festival => Results.Json(new SavedFestivalView(festival.Id), statusCode: StatusCodes.Status201Created), _resultEnvelope.Refuse);
  }

  public async Task<IResult> HideAsync(Guid festivalId, CancellationToken cancellationToken)
  {
    return await _service.HideAsync(festivalId, cancellationToken).Match(festival => Results.Ok(new SavedFestivalView(festival.Id)), _resultEnvelope.Refuse);
  }

  public async Task<IResult> ShowAsync(Guid festivalId, CancellationToken cancellationToken)
  {
    return await _service.ShowAsync(festivalId, cancellationToken).Match(festival => Results.Ok(new SavedFestivalView(festival.Id)), _resultEnvelope.Refuse);
  }

  private AdminFestivalView BuildFestivalView(Festival festival, IReadOnlyDictionary<Guid, int> orderCounts)
  {
    return _mapper.Map<AdminFestivalView>(festival) with
           {
             IsRunning = _service.IsRunning(festival),
             OrderCount = orderCounts.GetValueOrDefault(festival.Id)
           };
  }
}
