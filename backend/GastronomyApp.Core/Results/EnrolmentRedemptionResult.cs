using GastronomyApp.Contracts.Enums;
using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Results;

public sealed record EnrolmentRedemptionResult(EnrolmentRedemptionOutcome Outcome, DeviceOwnerKind? OwnerKind, Device? Device, StaffMember? StaffMember, Station? Station, string? PlaintextToken, Guid? InvitationId);
