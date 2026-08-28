namespace GastronomyApp.Api.Contracts;

public sealed record RedeemEnrolmentRequest
{
    public string? Code { get; init; }
    public string? Name { get; init; }
    public string? UserAgent { get; init; }
}

public sealed record RedeemedEnrolmentView(
    Guid DeviceId,
    string DeviceToken,
    StaffMemberView StaffMember,
    string Language);
