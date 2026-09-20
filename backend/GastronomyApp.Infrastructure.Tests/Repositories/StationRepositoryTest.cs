using GastronomyApp.Core.Entities;
using GastronomyApp.Core.ReadModels;
using GastronomyApp.Infrastructure.Repositories;
using GastronomyApp.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Tests.Repositories;

[TestFixture]
public sealed class StationRepositoryTest
{
  private readonly DateTime _now = new(2026, 8, 27, 18, 0, 0, DateTimeKind.Utc);

  [Test]
  public async Task FindAtFestivalAsync_TheStationsOfOneFestival_ReturnsThemByTheirPlaceInTheList()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    StationRepository repository = new(fixture.DbContext, new ProjectionConfiguration().Build());

    IReadOnlyCollection<Station> stations = await repository.FindAtFestivalAsync(seeded.FestivalId, TestContext.CurrentContext.CancellationToken);

    Assert.That(stations.Select(station => station.Id),
                Is.EqualTo(new[]
                           {
                             seeded.KitchenStationId,
                             seeded.BarStationId
                           }));
  }

  [Test]
  public async Task FindAdministeredAsync_WithoutAFestival_SaysNoStationBelongsToOne()
  {
    using SqliteInMemoryFixture fixture = new();
    await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    StationRepository repository = new(fixture.DbContext, new ProjectionConfiguration().Build());

    IReadOnlyList<AdministeredStation> stations = await repository.FindAdministeredAsync(null, _now, TestContext.CurrentContext.CancellationToken);

    Assert.That(stations.Select(station => station.IsAtTheFestival), Is.All.False);
  }

  [Test]
  public async Task FindAdministeredAsync_AFestivalTheStationTakesPartIn_SaysTheStationBelongsToIt()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    StationRepository repository = new(fixture.DbContext, new ProjectionConfiguration().Build());

    IReadOnlyList<AdministeredStation> stations = await repository.FindAdministeredAsync(seeded.FestivalId, _now, TestContext.CurrentContext.CancellationToken);

    Assert.That(stations.Select(station => station.IsAtTheFestival), Is.All.True);
  }

  [Test]
  public async Task FindAdministeredAsync_AStationHoldingATablet_CarriesWhenThatTabletWasLastSeen()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    var lastSeenAtUtc = _now.AddMinutes(-3);
    await GiveTheKitchenATabletAsync(fixture, seeded, lastSeenAtUtc);

    StationRepository repository = new(fixture.DbContext, new ProjectionConfiguration().Build());

    IReadOnlyList<AdministeredStation> stations = await repository.FindAdministeredAsync(null, _now, TestContext.CurrentContext.CancellationToken);

    var kitchen = stations.First(station => station.StationId == seeded.KitchenStationId);

    Assert.Multiple(() =>
                    {
                      Assert.That(kitchen.HasDevice, Is.True);
                      Assert.That(kitchen.LastSeenAtUtc, Is.EqualTo(lastSeenAtUtc));
                    });
  }

  [Test]
  public async Task FindAdministeredAsync_AnInvitationWhoseTimeRanOut_NoLongerCountsAsOutstanding()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);
    await InviteTheKitchenAsync(fixture, seeded, _now.AddMinutes(2));

    StationRepository repository = new(fixture.DbContext, new ProjectionConfiguration().Build());

    IReadOnlyList<AdministeredStation> stillValid = await repository.FindAdministeredAsync(null, _now, TestContext.CurrentContext.CancellationToken);
    IReadOnlyList<AdministeredStation> afterItRanOut = await repository.FindAdministeredAsync(null, _now.AddMinutes(5), TestContext.CurrentContext.CancellationToken);

    Assert.Multiple(() =>
                    {
                      Assert.That(stillValid.First(station => station.StationId == seeded.KitchenStationId).HasOutstandingInvitation, Is.True);
                      Assert.That(afterItRanOut.First(station => station.StationId == seeded.KitchenStationId).HasOutstandingInvitation, Is.False);
                    });
  }

  [Test]
  public async Task ExistsAsync_AStationNobodyEverCreated_AnswersFalse()
  {
    using SqliteInMemoryFixture fixture = new();
    var seeded = await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    StationRepository repository = new(fixture.DbContext, new ProjectionConfiguration().Build());

    Assert.Multiple(async () =>
                    {
                      Assert.That(await repository.ExistsAsync(seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken), Is.True);
                      Assert.That(await repository.ExistsAsync(Guid.NewGuid(), TestContext.CurrentContext.CancellationToken), Is.False);
                    });
  }

  [Test]
  public async Task AddAsync_ANewStation_StoresItWhenTheChangesAreSaved()
  {
    using SqliteInMemoryFixture fixture = new();
    await new DomainSeeder().SeedAsync(fixture.DbContext, TestContext.CurrentContext.CancellationToken);

    StationRepository repository = new(fixture.DbContext, new ProjectionConfiguration().Build());
    var stationId = Guid.NewGuid();

    await repository.AddAsync(new()
                              {
                                Id = stationId,
                                Name = "Kuchenbuffet",
                                SortOrder = 3,
                                IsActive = true
                              },
                              TestContext.CurrentContext.CancellationToken);
    await repository.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);

    await using var readContext = fixture.CreateContext();

    Assert.That(await readContext.Stations.AnyAsync(station => station.Id == stationId, TestContext.CurrentContext.CancellationToken), Is.True);
  }

  private async Task GiveTheKitchenATabletAsync(SqliteInMemoryFixture fixture, SeededDomain seeded, DateTime lastSeenAtUtc)
  {
    fixture.DbContext.Devices.Add(new()
                                  {
                                    Id = seeded.DeviceId,
                                    Language = "de",
                                    TokenHash = [1],
                                    TokenSalt = [2],
                                    TokenIterations = 1,
                                    TokenAlgorithm = "PBKDF2-HMAC-SHA512",
                                    TokenLookupId = Guid.NewGuid().ToString(),
                                    CreatedAtUtc = _now.AddHours(-1),
                                    LastSeenAtUtc = lastSeenAtUtc
                                  });

    var kitchen = await fixture.DbContext.Stations.FirstAsync(station => station.Id == seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);
    kitchen.DeviceId = seeded.DeviceId;

    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
  }

  private async Task InviteTheKitchenAsync(SqliteInMemoryFixture fixture, SeededDomain seeded, DateTime expiresAtUtc)
  {
    var invitationId = Guid.NewGuid();

    fixture.DbContext.EnrolmentInvitations.Add(new()
                                               {
                                                 Id = invitationId,
                                                 QRCodeHash = [1],
                                                 QRCodeSalt = [2],
                                                 QRCodeIterations = 1,
                                                 QRCodeAlgorithm = "PBKDF2-HMAC-SHA512",
                                                 CreatedAtUtc = _now.AddMinutes(-1),
                                                 ExpiresAtUtc = expiresAtUtc,
                                                 ConsumedAtUtc = null,
                                                 ConsumedByDeviceId = null
                                               });

    var kitchen = await fixture.DbContext.Stations.FirstAsync(station => station.Id == seeded.KitchenStationId, TestContext.CurrentContext.CancellationToken);
    kitchen.EnrolmentInvitationId = invitationId;

    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
  }
}
