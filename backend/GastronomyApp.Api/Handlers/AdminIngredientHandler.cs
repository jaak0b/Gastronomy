using ErrorOr;
using GastronomyApp.Api.Answers;
using GastronomyApp.Contracts.Admin.Ingredients;
using GastronomyApp.Core.Services;
using MapsterMapper;

namespace GastronomyApp.Api.Handlers;

public sealed class AdminIngredientHandler
{
  private readonly IMapper _mapper;
  private readonly IngredientAdministrationService _service;

  public AdminIngredientHandler(IngredientAdministrationService service, IMapper mapper)
  {
    _service = service;
    _mapper = mapper;
  }

  public async Task<ApiAnswer<AdminIngredientListView>> ListAsync(CancellationToken cancellationToken)
  {
    return new AdminIngredientListView(_mapper.Map<IReadOnlyList<AdminIngredientView>>(await _service.ListAsync(cancellationToken)));
  }

  public async Task<CreatedAnswer<AdminIngredientView>> CreateAsync(SaveIngredientRequest request, CancellationToken cancellationToken)
  {
    return await _service.CreateAsync(request.Name, request.Unit, cancellationToken).Then(_mapper.Map<AdminIngredientView>);
  }

  public async Task<ApiAnswer<SavedIngredientView>> UpdateAsync(Guid ingredientId, SaveIngredientRequest request, CancellationToken cancellationToken)
  {
    return await _service.UpdateAsync(ingredientId, request.Name, request.Unit, cancellationToken).Then(ingredient => new SavedIngredientView(ingredient.Id));
  }

  public async Task<ApiAnswer<SavedIngredientView>> ActivateAsync(Guid ingredientId, CancellationToken cancellationToken)
  {
    return await _service.ActivateAsync(ingredientId, cancellationToken).Then(ingredient => new SavedIngredientView(ingredient.Id));
  }

  public async Task<ApiAnswer<SavedIngredientView>> DeactivateAsync(Guid ingredientId, CancellationToken cancellationToken)
  {
    return await _service.DeactivateAsync(ingredientId, cancellationToken).Then(ingredient => new SavedIngredientView(ingredient.Id));
  }
}
