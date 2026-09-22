using GastronomyApp.Api.Mapping;
using GastronomyApp.Contracts.Catalog;
using GastronomyApp.Core.Entities;
using MapsterMapper;

namespace GastronomyApp.Api.Tests.Mapping;

[TestFixture]
public sealed class CatalogMappingTest
{
  [SetUp]
  public void SetUp()
  {
    _mapper = new Mapper(new MappingConfiguration(new(), new(), new()).Build());
  }

  private readonly Guid _barId = Guid.Parse("dddddddd-0000-0000-0000-000000000002");
  private readonly Guid _beerId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
  private readonly Guid _bratwurstId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
  private readonly Guid _drinkCategoryId = Guid.Parse("cccccccc-0000-0000-0000-000000000002");
  private readonly Guid _festivalId = Guid.Parse("eeeeeeee-0000-0000-0000-000000000001");
  private readonly Guid _foodCategoryId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
  private readonly Guid _kitchenId = Guid.Parse("dddddddd-0000-0000-0000-000000000001");

  private IMapper _mapper = null!;

  [Test]
  public void Map_AFestivalWithAMenu_NamesTheFestivalThePhoneIsOrderingFor()
  {
    var view = _mapper.Map<CatalogView>(BuildFestival());

    Assert.Multiple(() =>
                    {
                      Assert.That(view.Festival!.FestivalId, Is.EqualTo(_festivalId));
                      Assert.That(view.Festival.Name, Is.EqualTo("Sommerfest"));
                    });
  }

  [Test]
  public void Map_StationsInAnyOrder_PutsThemInThePlaceTheAdminGaveThem()
  {
    var view = _mapper.Map<CatalogView>(BuildFestival());

    Assert.Multiple(() =>
                    {
                      Assert.That(view.Stations.Select(station => station.Id),
                                  Is.EqualTo(new[]
                                             {
                                               _kitchenId,
                                               _barId
                                             }));
                      Assert.That(view.Stations[0].Name, Is.EqualTo("Kueche"));
                    });
  }

  [Test]
  public void Map_ItemsInAnyOrder_PutsThemInThePlaceTheAdminGaveThem()
  {
    var view = _mapper.Map<CatalogView>(BuildFestival());

    Assert.That(view.Items.Select(item => item.Id),
                Is.EqualTo(new[]
                           {
                             _bratwurstId,
                             _beerId
                           }));
  }

  [Test]
  public void Map_CategoriesInAnyOrder_PutsThemInThePlaceTheAdminGaveThem()
  {
    var view = _mapper.Map<CatalogView>(BuildFestival());

    Assert.That(view.Categories.Select(category => category.CategoryId),
                Is.EqualTo(new[]
                           {
                             _foodCategoryId,
                             _drinkCategoryId
                           }));
  }

  [Test]
  public void Map_TwoItemsOfOneCategory_NamesThatCategoryOnce()
  {
    var festival = BuildFestival();
    festival.CatalogItems.Add(BuildMenuRow(Guid.NewGuid(), "Currywurst", 400, 3, BuildCategory(_foodCategoryId, "Speisen", 1), _kitchenId));

    var view = _mapper.Map<CatalogView>(festival);

    Assert.That(view.Categories.Count(category => category.CategoryId == _foodCategoryId), Is.EqualTo(1));
  }

  [Test]
  public void Map_AnItemOnTheMenu_CarriesWhatItCostsAndWhoPreparesIt()
  {
    var view = _mapper.Map<CatalogView>(BuildFestival());

    var bratwurst = view.Items.Single(item => item.Id == _bratwurstId);

    Assert.Multiple(() =>
                    {
                      Assert.That(bratwurst.Name, Is.EqualTo("Bratwurst"));
                      Assert.That(bratwurst.CategoryId, Is.EqualTo(_foodCategoryId));
                      Assert.That(bratwurst.PriceCents, Is.EqualTo(350));
                      Assert.That(bratwurst.IsAvailable, Is.True);
                      Assert.That(bratwurst.ProductionMinutes, Is.EqualTo(5));
                      Assert.That(bratwurst.IsQueueIndependent, Is.False);
                      Assert.That(bratwurst.StationIds, Is.EqualTo(new[] { _kitchenId }));
                    });
  }

  private Festival BuildFestival()
  {
    Festival festival = new()
                        {
                          Id = _festivalId,
                          Name = "Sommerfest",
                          StartsAtUtc = new(2026, 8, 27, 16, 0, 0, DateTimeKind.Utc),
                          EndsAtUtc = new(2026, 8, 27, 23, 0, 0, DateTimeKind.Utc),
                          NextOrderNumber = 1,
                          IsHidden = false
                        };

    festival.Stations.Add(BuildStationLink(_barId, "Theke", 2));
    festival.Stations.Add(BuildStationLink(_kitchenId, "Kueche", 1));

    festival.CatalogItems.Add(BuildMenuRow(_beerId, "Bier", 250, 2, BuildCategory(_drinkCategoryId, "Getraenke", 2), _barId));
    festival.CatalogItems.Add(BuildMenuRow(_bratwurstId, "Bratwurst", 350, 1, BuildCategory(_foodCategoryId, "Speisen", 1), _kitchenId));

    return festival;
  }

  private FestivalStation BuildStationLink(Guid stationId, string name, int sortOrder)
  {
    return new()
           {
             Id = Guid.NewGuid(),
             FestivalId = _festivalId,
             StationId = stationId,
             NextStationOrderNumber = 1,
             Station = new()
                       {
                         Id = stationId,
                         Name = name,
                         SortOrder = sortOrder,
                         IsActive = true
                       }
           };
  }

  private FestivalCatalogItem BuildMenuRow(Guid itemId, string name, int priceCents, int sortOrder, CatalogCategory category, Guid stationId)
  {
    CatalogItem item = new()
                       {
                         Id = itemId,
                         Name = name,
                         CategoryId = category.Id,
                         SortOrder = sortOrder,
                         IsActive = true,
                         ProductionMinutes = 5,
                         Category = category
                       };

    item.StationAssignments.Add(new()
                                {
                                  Id = Guid.NewGuid(),
                                  FestivalId = _festivalId,
                                  CatalogItemId = itemId,
                                  StationId = stationId
                                });

    return new()
           {
             Id = Guid.NewGuid(),
             FestivalId = _festivalId,
             CatalogItemId = itemId,
             PriceCents = priceCents,
             IsAvailable = true,
             CatalogItem = item
           };
  }

  private CatalogCategory BuildCategory(Guid categoryId, string name, int sortOrder)
  {
    return new()
           {
             Id = categoryId,
             Name = name,
             ColourHex = "#C62828",
             SortOrder = sortOrder,
             IsActive = true
           };
  }
}
