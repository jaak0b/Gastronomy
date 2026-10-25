namespace GastronomyApp.Core.Entities;

public interface IFestivalScoped : IEntity
{
  public Guid FestivalId { get; }
}
