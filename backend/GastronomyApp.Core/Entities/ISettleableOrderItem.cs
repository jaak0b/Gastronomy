namespace GastronomyApp.Core.Entities;

public interface ISettleableOrderItem
{
  public int UnitPriceCents { get; }

  public DateTime? SettledAtUtc { get; }
}
