using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Ports;

public interface IIngredientRepository
{
  public Task<IReadOnlyList<Ingredient>> FindAllOrderedByNameAsync(CancellationToken cancellationToken);

  public Task<IReadOnlyList<Ingredient>> FindAllWithRecipeLinesOrderedByNameAsync(CancellationToken cancellationToken);

  public Task<Ingredient?> FindByIdAsync(Guid ingredientId, CancellationToken cancellationToken);

  public Task<Ingredient?> FindWithRecipeLinesAsync(Guid ingredientId, CancellationToken cancellationToken);

  public Task AddAsync(Ingredient ingredient, CancellationToken cancellationToken);

  public Task SaveChangesAsync(CancellationToken cancellationToken);
}
