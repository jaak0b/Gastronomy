using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Queries;

public static class FestivalScopedQueries
{
  public static IQueryable<TEntity> WhereFestivalIs<TEntity>(this IQueryable<TEntity> rows, Guid festivalId) where TEntity : class, IFestivalScoped
  {
    return rows.Where(row => row.FestivalId == festivalId);
  }
}
