using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Results;

namespace GastronomyApp.Core.Services;

public sealed class FestivalAdministrationService
{
  private const int FirstNumber = 1;

  private readonly FestivalMoment _moment;
  private readonly IFestivalRepository _repository;
  private readonly RunningFestivalLookup _runningFestival;
  private readonly FestivalSchedule _schedule;
  private readonly ITransactionRunner _transactionRunner;

  public FestivalAdministrationService(IFestivalRepository repository, FestivalSchedule schedule, FestivalMoment moment, ITransactionRunner transactionRunner, RunningFestivalLookup runningFestival)
  {
    _repository = repository;
    _schedule = schedule;
    _moment = moment;
    _transactionRunner = transactionRunner;
    _runningFestival = runningFestival;
  }

  public Task<IReadOnlyList<Festival>> ListAsync(CancellationToken cancellationToken)
  {
    return _repository.FindAllWithContentsAsync(cancellationToken);
  }

  public Task<IReadOnlyDictionary<Guid, int>> CountOrdersByFestivalAsync(CancellationToken cancellationToken)
  {
    return _repository.CountOrdersByFestivalAsync(cancellationToken);
  }

  public bool IsRunning(Festival festival)
  {
    return _runningFestival.IsRunning(festival);
  }

  public Task<Result<Festival, FestivalAdministrationFailure>> CreateAsync(string? name, DateTime startsAtUtc, DateTime endsAtUtc, CancellationToken cancellationToken)
  {
    return RunAsync(transactionCancellationToken => CreatedAsync(name, startsAtUtc, endsAtUtc, transactionCancellationToken), written => written.IsSuccess, cancellationToken);
  }

  public Task<Result<Festival, FestivalAdministrationFailure>> UpdateAsync(Guid festivalId, string? name, DateTime startsAtUtc, DateTime endsAtUtc, CancellationToken cancellationToken)
  {
    return RunAsync(transactionCancellationToken => UpdatedAsync(festivalId, name, startsAtUtc, endsAtUtc, transactionCancellationToken), written => written.IsSuccess, cancellationToken);
  }

  public Task<Result<Festival, FestivalAdministrationFailure>> CopyAsync(Guid festivalId, string? name, DateTime startsAtUtc, DateTime endsAtUtc, CancellationToken cancellationToken)
  {
    return RunAsync(transactionCancellationToken => CopiedAsync(festivalId, name, startsAtUtc, endsAtUtc, transactionCancellationToken), written => written.IsSuccess, cancellationToken);
  }

  public Task<Result<Festival?, FestivalAdministrationFailure>> HideAsync(Guid festivalId, CancellationToken cancellationToken)
  {
    return RunAsync(transactionCancellationToken => HiddenAsync(festivalId, transactionCancellationToken), written => written.IsSuccess && written.Value is not null, cancellationToken);
  }

  public Task<Result<Festival?, FestivalAdministrationFailure>> ShowAsync(Guid festivalId, CancellationToken cancellationToken)
  {
    return RunAsync(transactionCancellationToken => ShownAsync(festivalId, transactionCancellationToken), written => written.IsSuccess && written.Value is not null, cancellationToken);
  }

  private async Task<Result<Festival, FestivalAdministrationFailure>> CreatedAsync(string? name, DateTime startsAtUtcRaw, DateTime endsAtUtcRaw, CancellationToken cancellationToken)
  {
    var startsAtUtc = _moment.AsUtc(startsAtUtcRaw);
    var endsAtUtc = _moment.AsUtc(endsAtUtcRaw);

    var refusal = await PeriodRefusalAsync(name, startsAtUtc, endsAtUtc, Guid.Empty, cancellationToken);

    if (refusal is not null)
      return Result<Festival, FestivalAdministrationFailure>.Failed(refusal);

    var created = BuildFestival(name!, startsAtUtc, endsAtUtc);

    await _repository.AddAsync(created, cancellationToken);
    await _repository.SaveChangesAsync(cancellationToken);

    return Result<Festival, FestivalAdministrationFailure>.Success(created);
  }

  private async Task<Result<Festival, FestivalAdministrationFailure>> UpdatedAsync(Guid festivalId, string? name, DateTime startsAtUtcRaw, DateTime endsAtUtcRaw, CancellationToken cancellationToken)
  {
    var festival = await _repository.FindByIdAsync(festivalId, cancellationToken);

    if (festival is null)
      return Failed<Festival>(FestivalAdministrationFailureReason.FestivalNotFound);

    var startsAtUtc = _moment.AsUtc(startsAtUtcRaw);
    var endsAtUtc = _moment.AsUtc(endsAtUtcRaw);

    var refusal = await PeriodRefusalAsync(name, startsAtUtc, endsAtUtc, festivalId, cancellationToken);

    if (refusal is not null)
      return Result<Festival, FestivalAdministrationFailure>.Failed(refusal);

    festival.Name = name!;
    festival.StartsAtUtc = startsAtUtc;
    festival.EndsAtUtc = endsAtUtc;

    await _repository.SaveChangesAsync(cancellationToken);

    return Result<Festival, FestivalAdministrationFailure>.Success(festival);
  }

  private async Task<Result<Festival, FestivalAdministrationFailure>> CopiedAsync(Guid festivalId, string? name, DateTime startsAtUtcRaw, DateTime endsAtUtcRaw, CancellationToken cancellationToken)
  {
    if (!await _repository.ExistsAsync(festivalId, cancellationToken))
      return Failed<Festival>(FestivalAdministrationFailureReason.FestivalNotFound);

    var startsAtUtc = _moment.AsUtc(startsAtUtcRaw);
    var endsAtUtc = _moment.AsUtc(endsAtUtcRaw);

    var refusal = await PeriodRefusalAsync(name, startsAtUtc, endsAtUtc, Guid.Empty, cancellationToken);

    if (refusal is not null)
      return Result<Festival, FestivalAdministrationFailure>.Failed(refusal);

    var copy = BuildFestival(name!, startsAtUtc, endsAtUtc);

    await _repository.AddAsync(copy, cancellationToken);
    await _repository.CopyContentsAsync(festivalId, copy.Id, cancellationToken);
    await _repository.SaveChangesAsync(cancellationToken);

    return Result<Festival, FestivalAdministrationFailure>.Success(copy);
  }

  private async Task<Result<Festival?, FestivalAdministrationFailure>> HiddenAsync(Guid festivalId, CancellationToken cancellationToken)
  {
    var festival = await _repository.FindByIdAsync(festivalId, cancellationToken);

    if (festival is null)
      return Failed<Festival?>(FestivalAdministrationFailureReason.FestivalNotFound);

    if (_runningFestival.IsRunning(festival))
    {
      return Result<Festival?, FestivalAdministrationFailure>.Failed(new()
                                                                     {
                                                                       Reason = FestivalAdministrationFailureReason.FestivalIsRunning,
                                                                       OffendingFestivalId = festivalId
                                                                     });
    }

    if (festival.IsHidden)
      return Result<Festival?, FestivalAdministrationFailure>.Success(null);

    festival.IsHidden = true;
    await _repository.SaveChangesAsync(cancellationToken);

    return Result<Festival?, FestivalAdministrationFailure>.Success(festival);
  }

  private async Task<Result<Festival?, FestivalAdministrationFailure>> ShownAsync(Guid festivalId, CancellationToken cancellationToken)
  {
    var festival = await _repository.FindByIdAsync(festivalId, cancellationToken);

    if (festival is null)
      return Failed<Festival?>(FestivalAdministrationFailureReason.FestivalNotFound);

    if (!festival.IsHidden)
      return Result<Festival?, FestivalAdministrationFailure>.Success(null);

    festival.IsHidden = false;
    await _repository.SaveChangesAsync(cancellationToken);

    return Result<Festival?, FestivalAdministrationFailure>.Success(festival);
  }

  private async Task<FestivalAdministrationFailure?> PeriodRefusalAsync(string? name, DateTime startsAtUtc, DateTime endsAtUtc, Guid festivalKeepingItsOwnPeriod, CancellationToken cancellationToken)
  {
    if (endsAtUtc <= startsAtUtc)
      return new() { Reason = FestivalAdministrationFailureReason.PeriodInvalid };

    IReadOnlyCollection<Festival> others = await _repository.FindAllAsync(cancellationToken);
    var inTheWay = _schedule.FindOverlapping(festivalKeepingItsOwnPeriod, startsAtUtc, endsAtUtc, others);

    if (inTheWay is null)
      return null;

    return new()
           {
             Reason = FestivalAdministrationFailureReason.PeriodOverlapsAnotherFestival,
             OverlappingFestivalName = inTheWay.Name
           };
  }

  private Festival BuildFestival(string name, DateTime startsAtUtc, DateTime endsAtUtc)
  {
    return new()
           {
             Id = Guid.NewGuid(),
             Name = name,
             StartsAtUtc = startsAtUtc,
             EndsAtUtc = endsAtUtc,
             NextOrderNumber = FirstNumber,
             IsHidden = false
           };
  }

  private Result<TValue, FestivalAdministrationFailure> Failed<TValue>(FestivalAdministrationFailureReason reason)
  {
    return Result<TValue, FestivalAdministrationFailure>.Failed(new() { Reason = reason });
  }

  private async Task<Result<TValue, FestivalAdministrationFailure>> RunAsync<TValue>(Func<CancellationToken, Task<Result<TValue, FestivalAdministrationFailure>>> write, Func<Result<TValue, FestivalAdministrationFailure>, bool> shouldCommit, CancellationToken cancellationToken)
  {
    return await _transactionRunner.RunAsync(async transactionCancellationToken =>
                                             {
                                               Result<TValue, FestivalAdministrationFailure> written = await write(transactionCancellationToken);

                                               return new TransactionOutcome<Result<TValue, FestivalAdministrationFailure>>
                                                      {
                                                        Value = written,
                                                        ShouldCommit = shouldCommit(written)
                                                      };
                                             },
                                             cancellationToken);
  }
}
