using FakeItEasy;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using Microsoft.Extensions.Time.Testing;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class TablePlanServiceTest
{
  [SetUp]
  public void SetUp()
  {
    _repository = A.Fake<ITablePlanRepository>();
    _festivalRepository = A.Fake<IFestivalRepository>();

    A.CallTo(() => _festivalRepository.FindRunningAsync(A<DateTime>._, A<CancellationToken>._)).Returns(Task.FromResult<Festival?>(RunningFestival()));

    _service = new(_repository, new(_festivalRepository, new(), new FakeTimeProvider(new(_now))));
  }

  private readonly DateTime _now = new(2026, 9, 5, 20, 15, 0, DateTimeKind.Utc);
  private readonly Guid _festivalId = Guid.Parse("eeeeeeee-0000-0000-0000-000000000001");

  private IFestivalRepository _festivalRepository = null!;
  private ITablePlanRepository _repository = null!;
  private TablePlanService _service = null!;

  [Test]
  public async Task ReadRunningFestivalTablePlanAsync_NoFestivalIsRunning_ReadsNoAreaAndAsksTheDatabaseForNone()
  {
    A.CallTo(() => _festivalRepository.FindRunningAsync(A<DateTime>._, A<CancellationToken>._)).Returns(Task.FromResult<Festival?>(null));

    IReadOnlyList<ShownArea> areas = await _service.ReadRunningFestivalTablePlanAsync(CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(areas, Is.Empty);
                      A.CallTo(() => _repository.FindShownAreasWithItemFlagsAsync(A<Guid>._, A<CancellationToken>._)).MustNotHaveHappened();
                    });
  }

  [Test]
  public async Task ReadRunningFestivalTablePlanAsync_AFestivalIsRunning_ReadsTheShownPlanOfThatFestival()
  {
    ShownArea garden = new(new()
                           {
                             Id = Guid.NewGuid(),
                             FestivalId = _festivalId,
                             Name = "Garten",
                             SortOrder = 1,
                             IsHidden = false
                           },
                           [],
                           []);
    A.CallTo(() => _repository.FindShownAreasWithItemFlagsAsync(_festivalId, A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<ShownArea>>([garden]));

    IReadOnlyList<ShownArea> areas = await _service.ReadRunningFestivalTablePlanAsync(CancellationToken.None);

    Assert.That(areas, Is.EqualTo(new[] { garden }));
  }

  private Festival RunningFestival()
  {
    return new()
           {
             Id = _festivalId,
             Name = "Sommerfest",
             StartsAtUtc = _now.AddHours(-2),
             EndsAtUtc = _now.AddHours(6),
             NextOrderNumber = 1,
             IsHidden = false
           };
  }
}
