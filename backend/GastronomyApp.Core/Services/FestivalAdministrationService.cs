using ErrorOr;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Refusals;

namespace GastronomyApp.Core.Services;

public sealed class FestivalAdministrationService
{
  private const int FirstNumber = 1;

  private readonly IAfterCommitActions _afterCommitActions;
  private readonly IFestivalChangeAnnouncer _announcer;
  private readonly FestivalMoment _moment;
  private readonly IFestivalRepository _repository;
  private readonly RunningFestivalLookup _runningFestival;
  private readonly FestivalSchedule _schedule;
  private readonly ITransactionRunner _transactionRunner;

  public FestivalAdministrationService(IFestivalRepository repository, FestivalSchedule schedule, FestivalMoment moment, IFestivalChangeAnnouncer announcer, IAfterCommitActions afterCommitActions, ITransactionRunner transactionRunner, RunningFestivalLookup runningFestival)
  {
    _repository = repository;
    _announcer = announcer;
    _afterCommitActions = afterCommitActions;
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

  public Task<ErrorOr<Festival>> CreateAsync(string? name, DateTime startsAtUtc, DateTime endsAtUtc, CancellationToken cancellationToken)
  {
    return _transactionRunner.RunAsync(transactionCancellationToken => CreatedAsync(name, startsAtUtc, endsAtUtc, transactionCancellationToken).ThenDoAsync(saved => _afterCommitActions.RunWhenCommittedAsync(_announcer.AnnounceFestivalChangedAsync, transactionCancellationToken)), cancellationToken);
  }

  public Task<ErrorOr<Festival>> UpdateAsync(Guid festivalId, string? name, DateTime startsAtUtc, DateTime endsAtUtc, CancellationToken cancellationToken)
  {
    return _transactionRunner.RunAsync(transactionCancellationToken => UpdatedAsync(festivalId, name, startsAtUtc, endsAtUtc, transactionCancellationToken).ThenDoAsync(saved => _afterCommitActions.RunWhenCommittedAsync(_announcer.AnnounceFestivalChangedAsync, transactionCancellationToken)),
                                       cancellationToken);
  }

  public Task<ErrorOr<Festival>> CopyAsync(Guid festivalId, string? name, DateTime startsAtUtc, DateTime endsAtUtc, CancellationToken cancellationToken)
  {
    return _transactionRunner.RunAsync(transactionCancellationToken => CopiedAsync(festivalId, name, startsAtUtc, endsAtUtc, transactionCancellationToken).ThenDoAsync(saved => _afterCommitActions.RunWhenCommittedAsync(_announcer.AnnounceFestivalChangedAsync, transactionCancellationToken)),
                                       cancellationToken);
  }

  public Task<ErrorOr<Festival>> HideAsync(Guid festivalId, CancellationToken cancellationToken)
  {
    return _transactionRunner.RunAsync(transactionCancellationToken => HiddenAsync(festivalId, transactionCancellationToken).ThenDoAsync(saved => _afterCommitActions.RunWhenCommittedAsync(_announcer.AnnounceFestivalChangedAsync, transactionCancellationToken)), cancellationToken);
  }

  public Task<ErrorOr<Festival>> ShowAsync(Guid festivalId, CancellationToken cancellationToken)
  {
    return _transactionRunner.RunAsync(transactionCancellationToken => ShownAsync(festivalId, transactionCancellationToken).ThenDoAsync(saved => _afterCommitActions.RunWhenCommittedAsync(_announcer.AnnounceFestivalChangedAsync, transactionCancellationToken)), cancellationToken);
  }

  private async Task<ErrorOr<Festival>> CreatedAsync(string? name, DateTime startsAtUtcRaw, DateTime endsAtUtcRaw, CancellationToken cancellationToken)
  {
    var startsAtUtc = _moment.AsUtc(startsAtUtcRaw);
    var endsAtUtc = _moment.AsUtc(endsAtUtcRaw);

    if (await PeriodRefusalAsync(startsAtUtc, endsAtUtc, Guid.Empty, cancellationToken) is { } refusal)
      return refusal;

    var created = BuildFestival(name!, startsAtUtc, endsAtUtc);

    await _repository.AddAsync(created, cancellationToken);
    await _repository.SaveChangesAsync(cancellationToken);

    return created;
  }

  private async Task<ErrorOr<Festival>> UpdatedAsync(Guid festivalId, string? name, DateTime startsAtUtcRaw, DateTime endsAtUtcRaw, CancellationToken cancellationToken)
  {
    var festival = await _repository.FindByIdAsync(festivalId, cancellationToken);

    if (festival is null)
      return Refusal.Festival.FestivalNotFound(festivalId);

    var startsAtUtc = _moment.AsUtc(startsAtUtcRaw);
    var endsAtUtc = _moment.AsUtc(endsAtUtcRaw);

    if (await PeriodRefusalAsync(startsAtUtc, endsAtUtc, festivalId, cancellationToken) is { } refusal)
      return refusal;

    festival.Name = name!;
    festival.StartsAtUtc = startsAtUtc;
    festival.EndsAtUtc = endsAtUtc;

    await _repository.SaveChangesAsync(cancellationToken);

    return festival;
  }

  private async Task<ErrorOr<Festival>> CopiedAsync(Guid festivalId, string? name, DateTime startsAtUtcRaw, DateTime endsAtUtcRaw, CancellationToken cancellationToken)
  {
    if (!await _repository.ExistsAsync(festivalId, cancellationToken))
      return Refusal.Festival.FestivalNotFound(festivalId);

    var startsAtUtc = _moment.AsUtc(startsAtUtcRaw);
    var endsAtUtc = _moment.AsUtc(endsAtUtcRaw);

    if (await PeriodRefusalAsync(startsAtUtc, endsAtUtc, Guid.Empty, cancellationToken) is { } refusal)
      return refusal;

    var copy = BuildFestival(name!, startsAtUtc, endsAtUtc);

    await _repository.AddAsync(copy, cancellationToken);
    await _repository.CopyContentsAsync(festivalId, copy.Id, cancellationToken);
    await _repository.SaveChangesAsync(cancellationToken);

    return copy;
  }

  private async Task<ErrorOr<Festival>> HiddenAsync(Guid festivalId, CancellationToken cancellationToken)
  {
    var festival = await _repository.FindByIdAsync(festivalId, cancellationToken);

    if (festival is null)
      return Refusal.Festival.FestivalNotFound(festivalId);

    if (_runningFestival.IsRunning(festival))
      return Refusal.Festival.FestivalIsRunning(festivalId);

    if (festival.IsHidden)
      return festival;

    festival.IsHidden = true;
    await _repository.SaveChangesAsync(cancellationToken);

    return festival;
  }

  private async Task<ErrorOr<Festival>> ShownAsync(Guid festivalId, CancellationToken cancellationToken)
  {
    var festival = await _repository.FindByIdAsync(festivalId, cancellationToken);

    if (festival is null)
      return Refusal.Festival.FestivalNotFound(festivalId);

    if (!festival.IsHidden)
      return festival;

    festival.IsHidden = false;
    await _repository.SaveChangesAsync(cancellationToken);

    return festival;
  }

  private async Task<Error?> PeriodRefusalAsync(DateTime startsAtUtc, DateTime endsAtUtc, Guid festivalKeepingItsOwnPeriod, CancellationToken cancellationToken)
  {
    if (endsAtUtc <= startsAtUtc)
      return Refusal.Festival.PeriodInvalid(startsAtUtc, endsAtUtc);

    IReadOnlyCollection<Festival> others = await _repository.FindAllAsync(cancellationToken);
    var inTheWay = _schedule.FindOverlapping(festivalKeepingItsOwnPeriod, startsAtUtc, endsAtUtc, others);

    if (inTheWay is null)
      return null;

    return Refusal.Festival.PeriodOverlapsAnotherFestival(inTheWay.Name);
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
}
