namespace GastronomyApp.Core.Entities;

public interface ISortable : IEntity
{
  public int SortOrder { get; set; }
}
