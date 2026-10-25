using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Ports;

public interface IIngredientRepository
{
  public Task<IReadOnlyList<Ingredient>> FindAllOrderedByNameAsync(CancellationToken cancellationToken);
}
