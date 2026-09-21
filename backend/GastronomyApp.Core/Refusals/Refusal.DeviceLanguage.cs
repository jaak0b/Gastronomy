using ErrorOr;

namespace GastronomyApp.Core.Refusals;

public static partial class Refusal
{
  public static class DeviceLanguage
  {
    public static Error UnsupportedLanguage(string? language)
    {
      return BadRequest("session.unsupportedLanguage",
                        $"The device asked for the language {language}, and this application speaks German and English only.",
                        new Dictionary<string, object>
                        {
                          [MetadataKeys.ProblemCode] = ProblemCodes.ValidationFailed
                        });
    }

    public static Error DeviceNotFound(Guid deviceId)
    {
      return Unauthorized("DeviceNotFound",
                          $"The device {deviceId} does not exist, so its session is refused.");
    }
  }
}
