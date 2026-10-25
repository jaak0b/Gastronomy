namespace GastronomyApp.Core.Entities;

public interface INamed
{
  public Guid Id { get; }

  public string Name { get; }
}
