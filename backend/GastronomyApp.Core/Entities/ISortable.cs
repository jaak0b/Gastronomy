namespace GastronomyApp.Core.Entities;

public interface ISortable
{
  public Guid Id { get; }

  public int SortOrder { get; set; }
}
