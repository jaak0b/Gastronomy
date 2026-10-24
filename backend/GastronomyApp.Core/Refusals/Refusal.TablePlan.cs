using ErrorOr;
using GastronomyApp.Contracts.Validation;

namespace GastronomyApp.Core.Refusals;

public static partial class Refusal
{
  public static class TablePlan
  {
    public static Error FestivalNotFound(Guid festivalId)
    {
      return NotFound("FestivalNotFound", $"The festival {festivalId} whose table plan was asked for or was to change does not exist.");
    }

    public static Error AreaNotFound(Guid festivalId, Guid areaId)
    {
      return NotFound("AreaNotFound", $"The area {areaId} does not exist at the festival {festivalId}.");
    }

    public static Error TableNotFound(Guid festivalId, Guid tableId)
    {
      return NotFound("TableNotFound", $"The table {tableId} does not exist at the festival {festivalId}.");
    }

    public static Error StationLinkNotFound(Guid festivalId, Guid stationId)
    {
      return NotFound("StationLinkNotFound", $"The station {stationId} does not take part in the festival {festivalId}, so it has no place on that festival's plan.");
    }

    public static Error StationShapeNotFound(Guid festivalId, Guid stationId)
    {
      return NotFound("StationShapeNotFound", $"The station {stationId} is not placed on the plan of the festival {festivalId}, so there was nothing to remove.");
    }

    public static Error AreaOutsideTheFestival(Guid festivalId, Guid areaId)
    {
      return BadRequest(RefusalMessageKeys.AdminActionFailed,
                        $"The area {areaId} named in the request does not belong to the festival {festivalId}. The plan editor offers only that festival's areas, so this call did not come from that screen.",
                        new Dictionary<string, object> { [MetadataKeys.ProblemCode] = ProblemCodes.ValidationFailed });
    }

    public static Error AreaNameTaken(Guid festivalId, string name)
    {
      return Conflict("errors.admin.festivals.areaNameTaken", $"Another area of the festival {festivalId}, shown or hidden, already carries the name {name}, compared without regard to case.", new Dictionary<string, object> { [MetadataKeys.ProblemCode] = "AreaNameTaken" });
    }

    public static Error TableNameTaken(Guid festivalId, string name)
    {
      return Conflict("errors.admin.festivals.tableNameTaken", $"Another table of the festival {festivalId}, shown or hidden, already carries the name {name}, compared without regard to case.", new Dictionary<string, object> { [MetadataKeys.ProblemCode] = "TableNameTaken" });
    }

    public static Error TableWithItemsToHandOutOrSettleStaysShown(Guid festivalId, Guid tableId)
    {
      return Conflict("errors.admin.festivals.tableWithItemsToHandOutOrSettleStaysShown",
                      $"The festival {festivalId} is running and its table {tableId} has an item that is not yet handed out or not yet settled, so the table cannot be hidden.",
                      new Dictionary<string, object> { [MetadataKeys.ProblemCode] = "TableWithItemsToHandOutOrSettleStaysShown" });
    }

    public static Error AreaWithItemsToHandOutOrSettleStaysShown(Guid festivalId, Guid areaId)
    {
      return Conflict("errors.admin.festivals.areaWithItemsToHandOutOrSettleStaysShown",
                      $"The festival {festivalId} is running and a table in its area {areaId} has an item that is not yet handed out or not yet settled, so the area cannot be hidden.",
                      new Dictionary<string, object> { [MetadataKeys.ProblemCode] = "AreaWithItemsToHandOutOrSettleStaysShown" });
    }
  }
}
