using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Api.Tests.TestSupport;

public sealed class StoredItemHandOut
{
  private readonly OrderItemLifecycle _lifecycle = new();

  public async Task HandOutEveryItemAsync(GastronomyAppDbContext database)
  {
    ArgumentNullException.ThrowIfNull(database);

    await HandOutAsync(database, await database.OrderItems.Include(item => item.StationOrder).ToListAsync());
  }

  public async Task HandOutItemAsync(GastronomyAppDbContext database, Guid orderItemId)
  {
    ArgumentNullException.ThrowIfNull(database);

    await HandOutAsync(database, await database.OrderItems.Include(item => item.StationOrder).Where(item => item.Id == orderItemId).ToListAsync());
  }

  private async Task HandOutAsync(GastronomyAppDbContext database, IReadOnlyList<OrderItem> items)
  {
    var handedOutAtUtc = DateTime.UtcNow;

    foreach (var item in items)
      _lifecycle.AppendStatus(item, OrderItemState.HandedOut, new(null, item.StationOrder.StationId), handedOutAtUtc);

    await database.SaveChangesAsync();
  }
}
