using FakeItEasy;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using GastronomyApp.Core.ReadModels;
using GastronomyApp.Core.Services;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class OpenItemsServiceTest
{
  [SetUp]
  public void SetUp()
  {
    _repository = A.Fake<IOpenItemRepository>();
    _festivalRepository = A.Fake<IFestivalRepository>();
    _clock = A.Fake<IClock>();

    A.CallTo(() => _clock.UtcNow).Returns(_now);
    A.CallTo(() => _festivalRepository.FindRunningAsync(A<DateTime>._, A<CancellationToken>._))
     .Returns(Task.FromResult<Festival?>(RunningFestival()));
    A.CallTo(() => _repository.FindOpenAtFestivalAsync(A<Guid>._, A<CancellationToken>._))
     .Returns(Task.FromResult<IReadOnlyList<OrderItem>>([]));
    A.CallTo(() => _repository.FindGivenAwayAtFestivalSinceAsync(A<Guid>._, A<DateTime>._, A<CancellationToken>._))
     .Returns(Task.FromResult<IReadOnlyList<OrderItem>>([]));
    A.CallTo(() => _repository.FindOwnersAsync(A<IReadOnlyCollection<Guid>>._, A<CancellationToken>._))
     .Returns(Task.FromResult<IReadOnlyDictionary<Guid, OrderItemOwner>>(_owners));
    A.CallTo(() => _repository.FindTableNamesAtFestivalAsync(A<Guid>._, A<CancellationToken>._))
     .Returns(Task.FromResult<IReadOnlyList<string>>([]));

    _service = new(_repository,
                   _festivalRepository,
                   new(_repository, _festivalRepository, A.Fake<ITransactionRunner>(), _clock),
                   _clock);
  }

  private readonly DateTime _now = new(2026, 9, 5, 20, 15, 0, DateTimeKind.Utc);
  private readonly DateTime _orderedAtUtc = new(2026, 9, 5, 19, 5, 0, DateTimeKind.Utc);
  private readonly Guid _festivalId = Guid.Parse("eeeeeeee-0000-0000-0000-000000000001");
  private readonly Guid _settlingWaiter = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
  private readonly Dictionary<Guid, OrderItemOwner> _owners = [];

  private IClock _clock = null!;
  private IFestivalRepository _festivalRepository = null!;
  private IOpenItemRepository _repository = null!;
  private OpenItemsService _service = null!;

  [Test]
  public async Task ReadAsync_NoFestivalIsRunning_ReportsNoTables()
  {
    A.CallTo(() => _festivalRepository.FindRunningAsync(A<DateTime>._, A<CancellationToken>._))
     .Returns(Task.FromResult<Festival?>(null));

    OpenItemsReport report = await _service.ReadAsync(CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(report.Tables, Is.Empty);
                      Assert.That(report.OrderItemIdsWithoutAnOrder, Is.Empty);
                    });
  }

  [Test]
  public async Task ReadAsync_OpenItemsAtTwoTables_ReportsEachTableWithWhatItStillOwes()
  {
    GivenOpenItems(At("Tisch 3", OpenItem("Bier", 400)),
                   At("Tisch 12", OpenItem("Bratwurst", 350)),
                   At("Tisch 12", OpenItem("Limonade", 250)));

    OpenItemsReport report = await _service.ReadAsync(CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(report.Tables.Select(table => table.TableName),
                                  Is.EqualTo(new[] { "Tisch 12", "Tisch 3" }));
                      Assert.That(report.Tables[0].OpenAmountCents, Is.EqualTo(600));
                      Assert.That(report.Tables[1].OpenAmountCents, Is.EqualTo(400));
                    });
  }

  [Test]
  public async Task ReadAsync_AnItemTheTableDidNotPayInFull_ReportsWhatWasGivenAway()
  {
    OrderItem bier = At("Tisch 12", OpenItem("Bier", 400));
    bier.SettledAtUtc = _now.AddMinutes(-10);
    bier.SettledByStaffMemberId = _settlingWaiter;
    bier.ChargedPriceCents = 150;
    bier.PaymentNotice = "Kapelle";

    A.CallTo(() => _repository.FindGivenAwayAtFestivalSinceAsync(_festivalId,
                                                                 A<DateTime>._,
                                                                 A<CancellationToken>._))
     .Returns(Task.FromResult<IReadOnlyList<OrderItem>>([bier]));

    OpenItemsReport report = await _service.ReadAsync(CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(report.Tables, Has.Count.EqualTo(1));
                      Assert.That(report.Tables[0].GivenAwayAmountCents, Is.EqualTo(250));
                      Assert.That(report.Tables[0].GivenAwayItems[0].WaivedAmountCents, Is.EqualTo(250));
                      Assert.That(report.Tables[0].GivenAwayItems[0].PaymentNotice, Is.EqualTo("Kapelle"));
                    });
  }

  [Test]
  public async Task ReadAsync_AnItemWhoseOrderCannotBeFound_LeavesItOutOfTheTablesAndNamesIt()
  {
    OrderItem stray = OpenItem("Bratwurst", 350);
    GivenOpenItems(stray);

    OpenItemsReport report = await _service.ReadAsync(CancellationToken.None);

    Assert.Multiple(() =>
                    {
                      Assert.That(report.Tables, Is.Empty);
                      Assert.That(report.OrderItemIdsWithoutAnOrder, Is.EqualTo(new[] { stray.Id }));
                    });
  }

  [Test]
  public async Task ReadAsync_ItemsOfOneOrder_CarryTheNoteTheWaiterTypedForThem()
  {
    OrderItem bratwurst = At("Tisch 12", OpenItem("Bratwurst", 350));
    bratwurst.Note = "Ohne Ketchup";
    GivenOpenItems(bratwurst);

    OpenItemsReport report = await _service.ReadAsync(CancellationToken.None);

    Assert.That(report.Tables[0].Items[0].Note, Is.EqualTo("Ohne Ketchup"));
  }

  [Test]
  public async Task ReadTableNamesAsync_NoFestivalIsRunning_ReportsNoNames()
  {
    A.CallTo(() => _festivalRepository.FindRunningAsync(A<DateTime>._, A<CancellationToken>._))
     .Returns(Task.FromResult<Festival?>(null));

    Assert.That(await _service.ReadTableNamesAsync(CancellationToken.None), Is.Empty);
  }

  [Test]
  public async Task ReadTableNamesAsync_AFestivalIsRunning_ReportsTheNamesUsedAtThatFestival()
  {
    A.CallTo(() => _repository.FindTableNamesAtFestivalAsync(_festivalId, A<CancellationToken>._))
     .Returns(Task.FromResult<IReadOnlyList<string>>(["Tisch 12", "Tisch 3"]));

    Assert.That(await _service.ReadTableNamesAsync(CancellationToken.None),
                Is.EqualTo(new[] { "Tisch 12", "Tisch 3" }));
  }

  private void GivenOpenItems(params OrderItem[] items)
  {
    A.CallTo(() => _repository.FindOpenAtFestivalAsync(_festivalId, A<CancellationToken>._))
     .Returns(Task.FromResult<IReadOnlyList<OrderItem>>([.. items]));
  }

  private OrderItem At(string tableName, OrderItem item)
  {
    _owners[item.Id] = new()
                       {
                         OrderId = Guid.NewGuid(),
                         TableName = tableName,
                         GlobalOrderNumber = _owners.Count + 1,
                         OrderedAtUtc = _orderedAtUtc
                       };

    return item;
  }

  private OrderItem OpenItem(string itemName, int unitPriceCents)
  {
    return new()
           {
             Id = Guid.NewGuid(),
             StationOrderId = Guid.NewGuid(),
             CatalogItemId = Guid.NewGuid(),
             ItemName = itemName,
             UnitPriceCents = unitPriceCents
           };
  }

  private Festival RunningFestival()
  {
    return new()
           {
             Id = _festivalId,
             Name = "Sommerfest",
             StartsAtUtc = _orderedAtUtc,
             EndsAtUtc = _now.AddHours(5),
             NextOrderNumber = 1,
             IsHidden = false
           };
  }
}
