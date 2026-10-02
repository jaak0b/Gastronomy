using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Ports;

public interface ICatalogItemIngredientRepository
{
  public Task<CatalogItemIngredient?> FindAsync(Guid catalogItemId, Guid ingredientId, CancellationToken cancellationToken);

  public Task AddAsync(CatalogItemIngredient recipeLine, CancellationToken cancellationToken);

  public void Remove(CatalogItemIngredient recipeLine);

  public Task SaveChangesAsync(CancellationToken cancellationToken);
}
