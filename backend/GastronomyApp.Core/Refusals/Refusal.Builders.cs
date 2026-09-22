using ErrorOr;

namespace GastronomyApp.Core.Refusals;

public static partial class Refusal
{
  private static Error BadRequest(string messageKey, string reason)
  {
    return Error.Custom(RefusalType.BadRequest, messageKey, reason);
  }

  private static Error BadRequest(string messageKey, string reason, Dictionary<string, object> metadata)
  {
    return Error.Custom(RefusalType.BadRequest, messageKey, reason, metadata);
  }

  private static Error Unauthorized(string messageKey, string reason)
  {
    return Error.Custom(RefusalType.Unauthorized, messageKey, reason);
  }

  private static Error Unauthorized(string messageKey, string reason, Dictionary<string, object> metadata)
  {
    return Error.Custom(RefusalType.Unauthorized, messageKey, reason, metadata);
  }

  private static Error NotFound(string messageKey, string reason)
  {
    return Error.Custom(RefusalType.NotFound, messageKey, reason);
  }

  private static Error NotFound(string messageKey, string reason, Dictionary<string, object> metadata)
  {
    return Error.Custom(RefusalType.NotFound, messageKey, reason, metadata);
  }

  private static Error Conflict(string messageKey, string reason)
  {
    return Error.Custom(RefusalType.Conflict, messageKey, reason);
  }

  private static Error Conflict(string messageKey, string reason, Dictionary<string, object> metadata)
  {
    return Error.Custom(RefusalType.Conflict, messageKey, reason, metadata);
  }

  private static Error Gone(string messageKey, string reason)
  {
    return Error.Custom(RefusalType.Gone, messageKey, reason);
  }

  private static Error Gone(string messageKey, string reason, Dictionary<string, object> metadata)
  {
    return Error.Custom(RefusalType.Gone, messageKey, reason, metadata);
  }

  private static Error UnprocessableEntity(string messageKey, string reason)
  {
    return Error.Custom(RefusalType.UnprocessableEntity, messageKey, reason);
  }

  private static Error UnprocessableEntity(string messageKey, string reason, Dictionary<string, object> metadata)
  {
    return Error.Custom(RefusalType.UnprocessableEntity, messageKey, reason, metadata);
  }

  private static Error ServiceUnavailable(string messageKey, string reason, Dictionary<string, object> metadata)
  {
    return Error.Custom(RefusalType.ServiceUnavailable, messageKey, reason, metadata);
  }
}
