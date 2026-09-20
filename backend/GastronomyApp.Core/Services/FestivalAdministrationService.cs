using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.ReadModels;
using GastronomyApp.Core.Requests;
using GastronomyApp.Core.Results;

namespace GastronomyApp.Core.Services;

public sealed class FestivalAdministrationService
{
  private const int FirstNumber = 1;

  private readonly IClock _clock;
  private readonly FestivalMoment _moment;
  private readonly IFestivalRepository _repository;
  private readonly FestivalSchedule _schedule;
  private readonly ITransactionRunner _transactionRunner;

  public FestivalAdministrationService(IFestivalRepository repository, FestivalSchedule schedule, FestivalMoment moment, ITransactionRunner transactionRunner, IClock clock)
  {
    _repository = repository;
    _schedule = schedule;
    _moment = moment;
    _transactionRunner = transactionRunner;
    _clock = clock;
  }

  public async Task<IReadOnlyList<AdministeredFestival>> ListAsync(CancellationToken cancellationToken)
  {
    IReadOnlyCollection<Festival> festivals = await _repository.FindAllAsync(cancellationToken);
    IReadOnlyList<FestivalContentCounts> counts = await _repository.FindContentCountsAsync(cancellationToken);

    Dictionary<Guid, FestivalContentCounts> countsByFestivalId = counts.ToDictionary(count => count.FestivalId);
    var nowUtc = _clock.UtcNow;

    return festivals.Select(festival => BuildAdministeredFestival(festival, countsByFestivalId, nowUtc)).ToList();
  }

  public Task<Result<SavedFestival, FestivalAdministrationFailure>> CreateAsync(FestivalPeriodRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    return RunAsync(transactionCancellationToken => CreatedAsync(request, transactionCancellationToken), cancellationToken);
  }

  public Task<Result<SavedFestival, FestivalAdministrationFailure>> UpdateAsync(Guid festivalId, FestivalPeriodRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    return RunAsync(transactionCancellationToken => UpdatedAsync(festivalId, request, transactionCancellationToken), cancellationToken);
  }

  public Task<Result<SavedFestival, FestivalAdministrationFailure>> CopyAsync(Guid festivalId, FestivalPeriodRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    return RunAsync(transactionCancellationToken => CopiedAsync(festivalId, request, transactionCancellationToken), cancellationToken);
  }

  public Task<Result<SavedFestival, FestivalAdministrationFailure>> HideAsync(Guid festivalId, CancellationToken cancellationToken)
  {
    return RunAsync(transactionCancellationToken => HiddenAsync(festivalId, transactionCancellationToken), cancellationToken);
  }

  public Task<Result<SavedFestival, FestivalAdministrationFailure>> ShowAsync(Guid festivalId, CancellationToken cancellationToken)
  {
    return RunAsync(transactionCancellationToken => ShownAsync(festivalId, transactionCancellationToken), cancellationToken);
  }

  private async Task<Result<SavedFestival, FestivalAdministrationFailure>> CreatedAsync(FestivalPeriodRequest request, CancellationToken cancellationToken)
  {
    Result<FestivalPeriod, FestivalAdministrationFailure> period = await ReadPeriodAsync(request, Guid.Empty, cancellationToken);

    if (!period.IsSuccess)
      return Result<SavedFestival, FestivalAdministrationFailure>.Failed(period.Failure);

    var festivalId = Guid.NewGuid();

    await _repository.AddAsync(BuildFestival(festivalId, period.Value), cancellationToken);
    await _repository.SaveChangesAsync(cancellationToken);

    return Saved(festivalId, true);
  }

  private async Task<Result<SavedFestival, FestivalAdministrationFailure>> UpdatedAsync(Guid festivalId, FestivalPeriodRequest request, CancellationToken cancellationToken)
  {
    var festival = await _repository.FindByIdAsync(festivalId, cancellationToken);

    if (festival is null)
      return Failed(FestivalAdministrationFailureReason.FestivalNotFound);

    Result<FestivalPeriod, FestivalAdministrationFailure> period = await ReadPeriodAsync(request, festivalId, cancellationToken);

    if (!period.IsSuccess)
      return Result<SavedFestival, FestivalAdministrationFailure>.Failed(period.Failure);

    festival.Name = period.Value.Name;
    festival.StartsAtUtc = period.Value.StartsAtUtc;
    festival.EndsAtUtc = period.Value.EndsAtUtc;

    await _repository.SaveChangesAsync(cancellationToken);

    return Saved(festivalId, true);
  }

  private async Task<Result<SavedFestival, FestivalAdministrationFailure>> CopiedAsync(Guid festivalId, FestivalPeriodRequest request, CancellationToken cancellationToken)
  {
    if (!await _repository.ExistsAsync(festivalId, cancellationToken))
      return Failed(FestivalAdministrationFailureReason.FestivalNotFound);

    Result<FestivalPeriod, FestivalAdministrationFailure> period = await ReadPeriodAsync(request, Guid.Empty, cancellationToken);

    if (!period.IsSuccess)
      return Result<SavedFestival, FestivalAdministrationFailure>.Failed(period.Failure);

    var newFestivalId = Guid.NewGuid();

    await _repository.AddAsync(BuildFestival(newFestivalId, period.Value), cancellationToken);
    await _repository.CopyContentsAsync(festivalId, newFestivalId, cancellationToken);
    await _repository.SaveChangesAsync(cancellationToken);

    return Saved(newFestivalId, true);
  }

  private async Task<Result<SavedFestival, FestivalAdministrationFailure>> HiddenAsync(Guid festivalId, CancellationToken cancellationToken)
  {
    var festival = await _repository.FindByIdAsync(festivalId, cancellationToken);

    if (festival is null)
      return Failed(FestivalAdministrationFailureReason.FestivalNotFound);

    if (_schedule.IsRunning(festival, _clock.UtcNow))
    {
      return Result<SavedFestival, FestivalAdministrationFailure>.Failed(new()
                                                                         {
                                                                           Reason = FestivalAdministrationFailureReason.FestivalIsRunning,
                                                                           OffendingFestivalId = festivalId
                                                                         });
    }

    if (festival.IsHidden)
      return Saved(festivalId, false);

    festival.IsHidden = true;
    await _repository.SaveChangesAsync(cancellationToken);

    return Saved(festivalId, true);
  }

  private async Task<Result<SavedFestival, FestivalAdministrationFailure>> ShownAsync(Guid festivalId, CancellationToken cancellationToken)
  {
    var festival = await _repository.FindByIdAsync(festivalId, cancellationToken);

    if (festival is null)
      return Failed(FestivalAdministrationFailureReason.FestivalNotFound);

    if (!festival.IsHidden)
      return Saved(festivalId, false);

    festival.IsHidden = false;
    await _repository.SaveChangesAsync(cancellationToken);

    return Saved(festivalId, true);
  }

  private async Task<Result<FestivalPeriod, FestivalAdministrationFailure>> ReadPeriodAsync(FestivalPeriodRequest request, Guid candidateId, CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(request.Name))
      return Result<FestivalPeriod, FestivalAdministrationFailure>.Failed(new() { Reason = FestivalAdministrationFailureReason.NameMissing });

    var startsAtUtc = _moment.AsUtc(request.StartsAtUtc);
    var endsAtUtc = _moment.AsUtc(request.EndsAtUtc);

    if (endsAtUtc <= startsAtUtc)
      return Result<FestivalPeriod, FestivalAdministrationFailure>.Failed(new() { Reason = FestivalAdministrationFailureReason.PeriodInvalid });

    IReadOnlyCollection<Festival> others = await _repository.FindAllAsync(cancellationToken);
    var inTheWay = _schedule.FindOverlapping(candidateId, startsAtUtc, endsAtUtc, others);

    if (inTheWay is not null)
    {
      return Result<FestivalPeriod, FestivalAdministrationFailure>.Failed(new()
                                                                          {
                                                                            Reason = FestivalAdministrationFailureReason.PeriodOverlapsAnotherFestival,
                                                                            OverlappingFestivalName = inTheWay.Name
                                                                          });
    }

    return Result<FestivalPeriod, FestivalAdministrationFailure>.Success(new(request.Name, startsAtUtc, endsAtUtc));
  }

  private Festival BuildFestival(Guid festivalId, FestivalPeriod period)
  {
    return new()
           {
             Id = festivalId,
             Name = period.Name,
             StartsAtUtc = period.StartsAtUtc,
             EndsAtUtc = period.EndsAtUtc,
             NextOrderNumber = FirstNumber,
             IsHidden = false
           };
  }

  private AdministeredFestival BuildAdministeredFestival(Festival festival, IReadOnlyDictionary<Guid, FestivalContentCounts> countsByFestivalId, DateTime nowUtc)
  {
    countsByFestivalId.TryGetValue(festival.Id, out var counts);

    return new(festival.Id, festival.Name, festival.StartsAtUtc, festival.EndsAtUtc, festival.IsHidden, _schedule.IsRunning(festival, nowUtc), counts?.StationCount ?? 0, counts?.MenuItemCount ?? 0, counts?.OrderCount ?? 0);
  }

  private Result<SavedFestival, FestivalAdministrationFailure> Saved(Guid festivalId, bool somethingChanged)
  {
    return Result<SavedFestival, FestivalAdministrationFailure>.Success(new(festivalId, somethingChanged));
  }

  private Result<SavedFestival, FestivalAdministrationFailure> Failed(FestivalAdministrationFailureReason reason)
  {
    return Result<SavedFestival, FestivalAdministrationFailure>.Failed(new() { Reason = reason });
  }

  private async Task<Result<SavedFestival, FestivalAdministrationFailure>> RunAsync(Func<CancellationToken, Task<Result<SavedFestival, FestivalAdministrationFailure>>> write, CancellationToken cancellationToken)
  {
    return await _transactionRunner.RunAsync(async transactionCancellationToken =>
                                             {
                                               Result<SavedFestival, FestivalAdministrationFailure> written = await write(transactionCancellationToken);

                                               return new TransactionOutcome<Result<SavedFestival, FestivalAdministrationFailure>>
                                                      {
                                                        Value = written,
                                                        ShouldCommit = written.IsSuccess && written.Value.SomethingChanged
                                                      };
                                             },
                                             cancellationToken);
  }
}
