namespace GastronomyApp.Core.Refusals;

public static class RefusalType
{
  public const int BadRequest = 400;

  public const int Unauthorized = 401;

  public const int NotFound = 404;

  public const int Conflict = 409;

  public const int Gone = 410;

  public const int UnprocessableEntity = 422;

  public const int ServiceUnavailable = 503;
}
