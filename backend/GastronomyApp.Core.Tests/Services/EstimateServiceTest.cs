using FakeItEasy;
using GastronomyApp.Contracts.Orders;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Results;
using GastronomyApp.Core.Services;
using Microsoft.Extensions.Time.Testing;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class EstimateServiceTest
{
  [SetUp]
  public void SetUp()
  {
    _stationRepository = A.Fake<IStationRepository>();
    _catalogItemRepository = A.Fake<ICatalogItemRepository>();
    _festivalRepository = A.Fake<IFestivalRepository>();
    _clock = new FakeTimeProvider(new(_now));

    A.CallTo(() => _festivalRepository.FindRunningAsync(A<DateTime>._, A<CancellationToken>._)).Returns(Task.FromResult<Festival?>(RunningFestival()));
    A.CallTo(() => _stationRepository.FindAtFestivalWithOpenItemsAsync(_festivalId, A<CancellationToken>._))
     .Returns(Task.FromResult<IReadOnlyList<Station>>([
                                                        Station(_kitchenId, "Kueche"),
                                                        Station(_barId, "Theke")
                                                      ]));
    A.CallTo(() => _catalogItemRepository.FindByIdsAsync(A<IReadOnlyCollection<Guid>>._, A<CancellationToken>._)).Returns(Task.FromResult<IReadOnlyList<CatalogItem>>([_bratwurst, _beer]));

    _service = new(_stationRepository, _catalogItemRepository, new(_festivalRepository, new(), _clock), new());
  }

  private readonly DateTime _now = new(2026, 9, 5, 20, 15, 0, DateTimeKind.Utc);
  private readonly Guid _festivalId = Guid.Parse("eeeeeeee-0000-0000-0000-000000000001");
  private readonly Guid _kitchenId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
  private readonly Guid _barId = Guid.Parse("cccccccc-0000-0000-0000-000000000002");

  private readonly CatalogItem _bratwurst = new()
                                            {
                                              Id = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"),
                                              Name = "Bratwurst",
                                              CategoryId = Guid.NewGuid(),
                                              SortOrder = 1,
                                              IsActive = true,
                                              ProductionMinutes = 4
                                            };

  private readonly CatalogItem _beer = new()
                                       {
                                         Id = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002"),
                                         Name = "Bier",
                                         CategoryId = Guid.NewGuid(),
                                         SortOrder = 2,
                                         IsActive = true
                                       };

  private TimeProvider _clock = null!;
  private IFestivalRepository _festivalRepository = null!;
  private IStationRepository _stationRepository = null!;
  private ICatalogItemRepository _catalogItemRepository = null!;
  private EstimateService _service = null!;

  [Test]
  public async Task ReadTimedAssignmentsAsync_NoFestivalIsRunning_ReportsNothing()
  {
    A.CallTo(() => _festivalRepository.FindRunningAsync(A<DateTime>._, A<CancellationToken>._)).Returns(Task.FromResult<Festival?>(null));

    Assert.That(await _service.ReadTimedAssignmentsAsync(TestContext.CurrentContext.CancellationToken), Is.Empty);
  }

  [Test]
  public async Task ReadTimedAssignmentsAsync_AFestivalIsRunning_ReadsTheTimedAssignmentsOfThatFestival()
  {
    await _service.ReadTimedAssignmentsAsync(TestContext.CurrentContext.CancellationToken);

    A.CallTo(() => _catalogItemRepository.FindTimedAssignmentsIncludingOpenItemsAsync(_festivalId, A<CancellationToken>._)).MustHaveHappened();
  }

  [Test]
  public async Task QuoteAsync_LinesAtTwoStations_AnswersOnceForEachStationInTheOrderOfTheRequest()
  {
    EstimateQuoteRequest request = new()
                                   {
                                     Lines =
                                     [
                                       new() { CatalogItemId = _beer.Id, StationId = _barId, Units = 1 },
                                       new() { CatalogItemId = _bratwurst.Id, StationId = _kitchenId, Units = 2 },
                                       new() { CatalogItemId = _bratwurst.Id, StationId = _barId, Units = 1 }
                                     ]
                                   };

    var quote = await _service.QuoteAsync(request, TestContext.CurrentContext.CancellationToken);

    Assert.That(quote.Value,
                Is.EqualTo(new[]
                           {
                             new StationQuote(_barId, 4),
                             new StationQuote(_kitchenId, 8)
                           }));
  }

  [Test]
  public async Task QuoteAsync_AnUnknownArticle_IsRefused()
  {
    var unknownArticleId = Guid.NewGuid();
    EstimateQuoteRequest request = new() { Lines = [new() { CatalogItemId = unknownArticleId, StationId = _kitchenId, Units = 1 }] };

    var quote = await _service.QuoteAsync(request, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(quote.IsError, Is.True);
                      Assert.That(quote.FirstError.Description, Does.Contain(unknownArticleId.ToString()));
                    });
  }

  [Test]
  public async Task QuoteAsync_AStationThatIsNotAtTheRunningFestival_IsRefused()
  {
    var unknownStationId = Guid.NewGuid();
    EstimateQuoteRequest request = new() { Lines = [new() { CatalogItemId = _bratwurst.Id, StationId = unknownStationId, Units = 1 }] };

    var quote = await _service.QuoteAsync(request, TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(quote.IsError, Is.True);
                      Assert.That(quote.FirstError.Description, Does.Contain(unknownStationId.ToString()));
                    });
  }

  private Station Station(Guid stationId, string name)
  {
    return new()
           {
             Id = stationId,
             Name = name,
             SortOrder = 1,
             IsActive = true
           };
  }

  private Festival RunningFestival()
  {
    return new()
           {
             Id = _festivalId,
             Name = "Sommerfest",
             StartsAtUtc = _now.AddHours(-2),
             EndsAtUtc = _now.AddHours(5),
             NextOrderNumber = 1,
             IsHidden = false
           };
  }
}
