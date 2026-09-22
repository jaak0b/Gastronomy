using ErrorOr;
using GastronomyApp.Api.Answers;
using GastronomyApp.Contracts.Admin.Festivals;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Services;
using MapsterMapper;

namespace GastronomyApp.Api.Handlers;

public sealed class AdminFestivalHandler
{
  private readonly IMapper _mapper;
  private readonly FestivalAdministrationService _service;

  public AdminFestivalHandler(FestivalAdministrationService service, IMapper mapper)
  {
    _service = service;
    _mapper = mapper;
  }

  public async Task<ApiAnswer<AdminFestivalListView>> ListAsync(CancellationToken cancellationToken)
  {
    IReadOnlyList<Festival> festivals = await _service.ListAsync(cancellationToken);
    IReadOnlyDictionary<Guid, int> orderCounts = await _service.CountOrdersByFestivalAsync(cancellationToken);

    return new AdminFestivalListView(festivals.Select(festival => BuildFestivalView(festival, orderCounts)).ToList());
  }

  public async Task<CreatedAnswer<SavedFestivalView>> CreateAsync(SaveFestivalRequest request, CancellationToken cancellationToken)
  {
    return await _service.CreateAsync(request.Name, request.StartsAtUtc, request.EndsAtUtc, cancellationToken).Then(festival => new SavedFestivalView(festival.Id));
  }

  public async Task<ApiAnswer<SavedFestivalView>> UpdateAsync(Guid festivalId, SaveFestivalRequest request, CancellationToken cancellationToken)
  {
    return await _service.UpdateAsync(festivalId, request.Name, request.StartsAtUtc, request.EndsAtUtc, cancellationToken).Then(festival => new SavedFestivalView(festival.Id));
  }

  public async Task<CreatedAnswer<SavedFestivalView>> CopyAsync(Guid festivalId, SaveFestivalRequest request, CancellationToken cancellationToken)
  {
    return await _service.CopyAsync(festivalId, request.Name, request.StartsAtUtc, request.EndsAtUtc, cancellationToken).Then(festival => new SavedFestivalView(festival.Id));
  }

  public async Task<ApiAnswer<SavedFestivalView>> HideAsync(Guid festivalId, CancellationToken cancellationToken)
  {
    return await _service.HideAsync(festivalId, cancellationToken).Then(festival => new SavedFestivalView(festival.Id));
  }

  public async Task<ApiAnswer<SavedFestivalView>> ShowAsync(Guid festivalId, CancellationToken cancellationToken)
  {
    return await _service.ShowAsync(festivalId, cancellationToken).Then(festival => new SavedFestivalView(festival.Id));
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
