using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Queries;

public static class ActivatableQueries
{
  public static IQueryable<TEntity> Active<TEntity>(this IQueryable<TEntity> rows) where TEntity : class, IActivatable
  {
    return rows.Where(row => row.IsActive);
  }
}
