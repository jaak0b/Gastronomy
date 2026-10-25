using ErrorOr;
using GastronomyApp.Api.Answers;
using GastronomyApp.Contracts.Admin.Festivals;
using GastronomyApp.Core.Services;

namespace GastronomyApp.Api.Handlers;

public sealed class AdminTableHandler
{
  private readonly TableAdministrationService _service;

  public AdminTableHandler(TableAdministrationService service)
  {
    _service = service;
  }

  public async Task<CreatedAnswer<SavedTableView>> CreateAsync(Guid festivalId, SaveTableRequest request, CancellationToken cancellationToken)
  {
    return await _service.CreateAsync(festivalId, request, cancellationToken).Then(table => new SavedTableView(table.Id));
  }

  public async Task<CreatedAnswer<CopiedTablesView>> CopyAsync(Guid festivalId, CopyTablesRequest request, CancellationToken cancellationToken)
  {
    return await _service.CopyAsync(festivalId, request, cancellationToken).Then(tables => new CopiedTablesView(tables.Select(table => table.Id).ToList()));
  }

  public async Task<NoContentAnswer> MoveAsync(Guid festivalId, MoveTablesRequest request, CancellationToken cancellationToken)
  {
    return await _service.MoveAsync(festivalId, request, cancellationToken).Then(_ => Result.Success);
  }

  public async Task<ApiAnswer<SavedTableView>> UpdateAsync(Guid festivalId, Guid tableId, SaveTableRequest request, CancellationToken cancellationToken)
  {
    return await _service.UpdateAsync(festivalId, tableId, request, cancellationToken).Then(table => new SavedTableView(table.Id));
  }

  public async Task<ApiAnswer<SavedTableView>> HideAsync(Guid festivalId, Guid tableId, CancellationToken cancellationToken)
  {
    return await _service.HideAsync(festivalId, tableId, cancellationToken).Then(table => new SavedTableView(table.Id));
  }

  public async Task<ApiAnswer<SavedTableView>> ShowAsync(Guid festivalId, Guid tableId, CancellationToken cancellationToken)
  {
    return await _service.ShowAsync(festivalId, tableId, cancellationToken).Then(table => new SavedTableView(table.Id));
  }
}
