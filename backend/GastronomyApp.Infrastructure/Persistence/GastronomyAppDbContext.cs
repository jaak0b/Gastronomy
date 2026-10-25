using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Persistence;

public sealed class GastronomyAppDbContext : DbContext, IGastronomyDatabase
{
  public GastronomyAppDbContext(DbContextOptions<GastronomyAppDbContext> options) : base(options)
  {
  }

  public bool IsReadOnly { get; private set; }

  public void MarkReadOnly()
  {
    IsReadOnly = true;
  }

  public DbSet<Station> Stations => Set<Station>();

  public DbSet<CatalogCategory> CatalogCategories => Set<CatalogCategory>();

  public DbSet<CatalogItem> CatalogItems => Set<CatalogItem>();

  public DbSet<ItemStationAssignment> ItemStationAssignments => Set<ItemStationAssignment>();

  public DbSet<StaffMember> StaffMembers => Set<StaffMember>();

  public DbSet<Device> Devices => Set<Device>();

  public DbSet<EnrolmentInvitation> EnrolmentInvitations => Set<EnrolmentInvitation>();

  public DbSet<Order> Orders => Set<Order>();

  public DbSet<StationOrder> StationOrders => Set<StationOrder>();

  public DbSet<OrderItem> OrderItems => Set<OrderItem>();

  public DbSet<Festival> Festivals => Set<Festival>();

  public DbSet<FestivalStation> FestivalStations => Set<FestivalStation>();

  public DbSet<FestivalCatalogItem> FestivalCatalogItems => Set<FestivalCatalogItem>();

  public DbSet<Unit> Units => Set<Unit>();

  public DbSet<Ingredient> Ingredients => Set<Ingredient>();

  public DbSet<CatalogItemIngredient> CatalogItemIngredients => Set<CatalogItemIngredient>();

  public DbSet<FestivalIngredient> FestivalIngredients => Set<FestivalIngredient>();

  public DbSet<Area> Areas => Set<Area>();

  public DbSet<Table> Tables => Set<Table>();

  public DbSet<FestivalStationShape> FestivalStationShapes => Set<FestivalStationShape>();

  override public int SaveChanges(bool acceptAllChangesOnSuccess)
  {
    throw new InvalidOperationException("Saving synchronously is not supported: use SaveChangesAsync.");
  }

  override public async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
  {
    if (IsReadOnly)
      throw new InvalidOperationException("This request only reads, so it must not save: a save here would commit outside the request transaction without anyone noticing.");

    return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
  }

  override protected void OnModelCreating(ModelBuilder modelBuilder)
  {
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(GastronomyAppDbContext).Assembly);
  }
}
