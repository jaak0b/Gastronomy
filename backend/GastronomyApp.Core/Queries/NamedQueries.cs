using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Queries;

public static class NamedQueries
{
  public static IQueryable<TEntity> Namesakes<TEntity>(this IQueryable<TEntity> rows, string name, Guid? excludedId) where TEntity : class, INamed
  {
    return rows.Where(row => row.Name == name && row.Id != excludedId);
  }
}
