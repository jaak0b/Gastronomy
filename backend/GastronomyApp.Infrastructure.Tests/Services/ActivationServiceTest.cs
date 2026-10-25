using ErrorOr;
using GastronomyApp.Contracts.Admin.Festivals;
using GastronomyApp.Contracts.Admin.Ingredients;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Refusals;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure.Tests.TestSupport;

namespace GastronomyApp.Infrastructure.Tests.Services;

[TestFixture]
public sealed class ActivationServiceTest
{
  private readonly TablePlanSeeder _plan = new();

  [Test]
  public async Task ActivateAsync_ASwitchedOffRow_SwitchesItOnAndStoresIt()
  {
    using SqliteInMemoryFixture fixture = new();
    var ingredient = await StoreIngredientAsync(fixture, false);
    ActivationService service = new(fixture.DbContext, new CoreMappingFactory().Create());

    ErrorOr<SavedIngredientView> activated = await service.ActivateAsync<Ingredient, SavedIngredientView>(ingredient.Id, Refusal.Ingredients.IngredientNotFound, TestContext.CurrentContext.CancellationToken);
    fixture.DbContext.ChangeTracker.Clear();

    Assert.Multiple(() =>
                    {
                      Assert.That(activated.Value, Is.EqualTo(new SavedIngredientView(ingredient.Id)));
                      Assert.That(fixture.DbContext.Ingredients.Single().IsActive, Is.True);
                    });
  }

  [Test]
  public async Task ActivateAsync_AnUnknownRow_IsRefusedWithTheNotFoundRefusalOfItsArea()
  {
    using SqliteInMemoryFixture fixture = new();
    ActivationService service = new(fixture.DbContext, new CoreMappingFactory().Create());

    ErrorOr<SavedIngredientView> activated = await service.ActivateAsync<Ingredient, SavedIngredientView>(Guid.NewGuid(), Refusal.Ingredients.IngredientNotFound, TestContext.CurrentContext.CancellationToken);

    Assert.That(activated.FirstError.NumericType, Is.EqualTo(RefusalType.NotFound));
  }

  [Test]
  public async Task ShowAsync_AHiddenRow_ShowsItAndStoresIt()
  {
    using SqliteInMemoryFixture fixture = new();
    var festival = await StoreFestivalAsync(fixture, true);
    ActivationService service = new(fixture.DbContext, new CoreMappingFactory().Create());

    ErrorOr<SavedFestivalView> shown = await service.ShowAsync<Festival, SavedFestivalView>(festival.Id, Refusal.Festival.FestivalNotFound, TestContext.CurrentContext.CancellationToken);
    fixture.DbContext.ChangeTracker.Clear();

    Assert.Multiple(() =>
                    {
                      Assert.That(shown.Value, Is.EqualTo(new SavedFestivalView(festival.Id)));
                      Assert.That(fixture.DbContext.Festivals.Single().IsHidden, Is.False);
                    });
  }

  [Test]
  public async Task ShowAsync_ARowThatIsAlreadyShown_AnswersWithItAgain()
  {
    using SqliteInMemoryFixture fixture = new();
    var festival = await StoreFestivalAsync(fixture, false);
    ActivationService service = new(fixture.DbContext, new CoreMappingFactory().Create());

    ErrorOr<SavedFestivalView> shown = await service.ShowAsync<Festival, SavedFestivalView>(festival.Id, Refusal.Festival.FestivalNotFound, TestContext.CurrentContext.CancellationToken);

    Assert.That(shown.Value, Is.EqualTo(new SavedFestivalView(festival.Id)));
  }

  [Test]
  public async Task ShowAsync_AnUnknownRow_IsRefusedWithTheNotFoundRefusalOfItsArea()
  {
    using SqliteInMemoryFixture fixture = new();
    ActivationService service = new(fixture.DbContext, new CoreMappingFactory().Create());

    ErrorOr<SavedFestivalView> shown = await service.ShowAsync<Festival, SavedFestivalView>(Guid.NewGuid(), Refusal.Festival.FestivalNotFound, TestContext.CurrentContext.CancellationToken);

    Assert.That(shown.FirstError.NumericType, Is.EqualTo(RefusalType.NotFound));
  }

  [Test]
  public async Task ShowAsync_AHiddenAreaOfTheFestival_ShowsItAndStoresIt()
  {
    using SqliteInMemoryFixture fixture = new();
    var festivalId = await _plan.AddFestivalAsync(fixture.DbContext);
    var areaId = await _plan.AddAreaAsync(fixture.DbContext, festivalId, "Garten", 1, true);
    ActivationService service = new(fixture.DbContext, new CoreMappingFactory().Create());

    ErrorOr<SavedAreaView> shown = await service.ShowAsync<Area, SavedAreaView>(festivalId, areaId, id => Refusal.TablePlan.AreaNotFound(festivalId, id), TestContext.CurrentContext.CancellationToken);
    fixture.DbContext.ChangeTracker.Clear();

    Assert.Multiple(() =>
                    {
                      Assert.That(shown.Value.AreaId, Is.EqualTo(areaId));
                      Assert.That(fixture.DbContext.Areas.Single().IsHidden, Is.False);
                    });
  }

  [Test]
  public async Task ShowAsync_AnAreaOfAnotherFestival_IsNotFoundAndStaysHidden()
  {
    using SqliteInMemoryFixture fixture = new();
    var festivalId = await _plan.AddFestivalAsync(fixture.DbContext);
    var otherFestivalId = await _plan.AddFestivalAsync(fixture.DbContext);
    var strangerAreaId = await _plan.AddAreaAsync(fixture.DbContext, otherFestivalId, "Garten", 1, true);
    ActivationService service = new(fixture.DbContext, new CoreMappingFactory().Create());

    ErrorOr<SavedAreaView> shown = await service.ShowAsync<Area, SavedAreaView>(festivalId, strangerAreaId, id => Refusal.TablePlan.AreaNotFound(festivalId, id), TestContext.CurrentContext.CancellationToken);
    fixture.DbContext.ChangeTracker.Clear();

    Assert.Multiple(() =>
                    {
                      Assert.That(shown.RefusalMessageKey(), Is.EqualTo("AreaNotFound"));
                      Assert.That(fixture.DbContext.Areas.Single().IsHidden, Is.True);
                    });
  }

  [Test]
  public async Task ShowAsync_AHiddenTableOfTheFestival_ShowsItAndStoresIt()
  {
    using SqliteInMemoryFixture fixture = new();
    var festivalId = await _plan.AddFestivalAsync(fixture.DbContext);
    var areaId = await _plan.AddAreaAsync(fixture.DbContext, festivalId, "Garten", 1, false);
    var tableId = await _plan.AddTableAsync(fixture.DbContext, festivalId, areaId, "Tisch 1", true);
    ActivationService service = new(fixture.DbContext, new CoreMappingFactory().Create());

    ErrorOr<SavedTableView> shown = await service.ShowAsync<Table, SavedTableView>(festivalId, tableId, id => Refusal.TablePlan.TableNotFound(festivalId, id), TestContext.CurrentContext.CancellationToken);
    fixture.DbContext.ChangeTracker.Clear();

    Assert.Multiple(() =>
                    {
                      Assert.That(shown.Value.TableId, Is.EqualTo(tableId));
                      Assert.That(fixture.DbContext.Tables.Single().IsHidden, Is.False);
                    });
  }

  [Test]
  public async Task ShowAsync_ATableOfAnotherFestival_IsNotFoundAndStaysHidden()
  {
    using SqliteInMemoryFixture fixture = new();
    var festivalId = await _plan.AddFestivalAsync(fixture.DbContext);
    var otherFestivalId = await _plan.AddFestivalAsync(fixture.DbContext);
    var strangerAreaId = await _plan.AddAreaAsync(fixture.DbContext, otherFestivalId, "Wiese", 1, false);
    var strangerTableId = await _plan.AddTableAsync(fixture.DbContext, otherFestivalId, strangerAreaId, "Tisch 1", true);
    ActivationService service = new(fixture.DbContext, new CoreMappingFactory().Create());

    ErrorOr<SavedTableView> shown = await service.ShowAsync<Table, SavedTableView>(festivalId, strangerTableId, id => Refusal.TablePlan.TableNotFound(festivalId, id), TestContext.CurrentContext.CancellationToken);
    fixture.DbContext.ChangeTracker.Clear();

    Assert.Multiple(() =>
                    {
                      Assert.That(shown.RefusalMessageKey(), Is.EqualTo("TableNotFound"));
                      Assert.That(fixture.DbContext.Tables.Single().IsHidden, Is.True);
                    });
  }

  private async Task<Festival> StoreFestivalAsync(SqliteInMemoryFixture fixture, bool isHidden)
  {
    Festival festival = new()
                        {
                          Id = Guid.NewGuid(),
                          Name = "Sommerfest",
                          StartsAtUtc = new(2030, 6, 1, 14, 0, 0, DateTimeKind.Utc),
                          EndsAtUtc = new(2030, 6, 2, 14, 0, 0, DateTimeKind.Utc),
                          NextOrderNumber = 1,
                          IsHidden = isHidden
                        };
    fixture.DbContext.Festivals.Add(festival);
    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
    fixture.DbContext.ChangeTracker.Clear();

    return festival;
  }

  private async Task<Ingredient> StoreIngredientAsync(SqliteInMemoryFixture fixture, bool isActive)
  {
    Unit unit = new()
                {
                  Id = Guid.NewGuid(),
                  Name = "Portion",
                  IsActive = true
                };
    Ingredient ingredient = new()
                            {
                              Id = Guid.NewGuid(),
                              Name = "Senf",
                              UnitId = unit.Id,
                              Unit = unit,
                              IsActive = isActive
                            };
    fixture.DbContext.Ingredients.Add(ingredient);
    await fixture.DbContext.SaveChangesAsync(TestContext.CurrentContext.CancellationToken);
    fixture.DbContext.ChangeTracker.Clear();

    return ingredient;
  }
}
