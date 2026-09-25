namespace GastronomyApp.Core.Announcements;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class RaisesAttribute : Attribute
{
  public RaisesAttribute(params HubEvent[] events)
  {
    Events = events;
  }

  public IReadOnlyList<HubEvent> Events { get; }
}
