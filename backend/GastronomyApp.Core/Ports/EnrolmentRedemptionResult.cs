using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Enums;

namespace GastronomyApp.Core.Ports;

public sealed record EnrolmentRedemptionResult(
  EnrolmentRedemptionOutcome Outcome,
  DeviceOwnerKind? OwnerKind,
  Device? Device,
  StaffMember? StaffMember,
  Station? Station,
  string? PlaintextToken,
  Guid? InvitationId);
