using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Services;

namespace GastronomyApp.Core.Tests.Services;

[TestFixture]
public sealed class FestivalServiceTest
{
  private readonly FestivalService _festivalService = new();

  [Test]
  public void StationCountOf_AFestivalWithoutStations_IsZero()
  {
    Assert.That(_festivalService.StationCountOf(NewFestival()), Is.EqualTo(0));
  }

  [Test]
  public void StationCountOf_TwoStationsTakePart_CountsBoth()
  {
    var festival = NewFestival();
    festival.Stations.Add(new()
                          {
                            Id = Guid.NewGuid(),
                            FestivalId = festival.Id,
                            StationId = Guid.NewGuid(),
                            NextStationOrderNumber = 1
                          });
    festival.Stations.Add(new()
                          {
                            Id = Guid.NewGuid(),
                            FestivalId = festival.Id,
                            StationId = Guid.NewGuid(),
                            NextStationOrderNumber = 1
                          });

    Assert.That(_festivalService.StationCountOf(festival), Is.EqualTo(2));
  }

  [Test]
  public void MenuItemCountOf_AFestivalWithoutAMenu_IsZero()
  {
    Assert.That(_festivalService.MenuItemCountOf(NewFestival()), Is.EqualTo(0));
  }

  [Test]
  public void MenuItemCountOf_OneItemIsOnTheMenu_CountsThatItem()
  {
    var festival = NewFestival();
    festival.CatalogItems.Add(new()
                              {
                                Id = Guid.NewGuid(),
                                FestivalId = festival.Id,
                                CatalogItemId = Guid.NewGuid(),
                                PriceCents = 250,
                                IsAvailable = true
                              });

    Assert.That(_festivalService.MenuItemCountOf(festival), Is.EqualTo(1));
  }

  private static Festival NewFestival()
  {
    return new()
           {
             Id = Guid.NewGuid(),
             Name = "Sommerfest",
             StartsAtUtc = new(2026, 8, 26, 12, 0, 0, DateTimeKind.Utc),
             EndsAtUtc = new(2026, 8, 28, 12, 0, 0, DateTimeKind.Utc),
             NextOrderNumber = 1,
             IsHidden = false
           };
  }
}
