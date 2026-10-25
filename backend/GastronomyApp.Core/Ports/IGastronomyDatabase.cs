using GastronomyApp.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Core.Ports;

public interface IGastronomyDatabase
{
  public DbSet<Station> Stations { get; }

  public DbSet<CatalogCategory> CatalogCategories { get; }

  public DbSet<CatalogItem> CatalogItems { get; }

  public DbSet<ItemStationAssignment> ItemStationAssignments { get; }

  public DbSet<StaffMember> StaffMembers { get; }

  public DbSet<Device> Devices { get; }

  public DbSet<EnrolmentInvitation> EnrolmentInvitations { get; }

  public DbSet<Order> Orders { get; }

  public DbSet<StationOrder> StationOrders { get; }

  public DbSet<OrderItem> OrderItems { get; }

  public DbSet<Festival> Festivals { get; }

  public DbSet<FestivalStation> FestivalStations { get; }

  public DbSet<FestivalCatalogItem> FestivalCatalogItems { get; }

  public DbSet<Unit> Units { get; }

  public DbSet<Ingredient> Ingredients { get; }

  public DbSet<CatalogItemIngredient> CatalogItemIngredients { get; }

  public DbSet<FestivalIngredient> FestivalIngredients { get; }

  public DbSet<Area> Areas { get; }

  public DbSet<Table> Tables { get; }

  public DbSet<FestivalStationShape> FestivalStationShapes { get; }

  public DbSet<TEntity> Set<TEntity>() where TEntity : class;

  public Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
