using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Results;

public sealed record EnrolmentRedemptionResult(EnrolmentInvitation Invitation, IDeviceOwner Owner, string PlaintextToken);
