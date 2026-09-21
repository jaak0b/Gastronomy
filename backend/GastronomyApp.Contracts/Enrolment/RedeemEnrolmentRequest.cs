using GastronomyApp.Contracts.Validation;

namespace GastronomyApp.Contracts.Enrolment;

public sealed record RedeemEnrolmentRequest
{
  [RequiredText(ErrorMessage = RefusalMessageKeys.EnrolmentCodeMissing)]
  public string? Code { get; init; }

  public string? Name { get; init; }

  public string? UserAgent { get; init; }

  public string? PreviousDeviceToken { get; init; }
}
