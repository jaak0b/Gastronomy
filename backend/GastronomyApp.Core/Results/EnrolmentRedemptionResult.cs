using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Results;

public sealed record EnrolmentRedemptionResult(EnrolmentRedemptionOutcome Outcome, EnrolmentInvitation? Invitation, IDeviceOwner? Owner, string? PlaintextToken);
