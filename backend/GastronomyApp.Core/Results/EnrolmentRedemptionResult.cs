using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Results;

public sealed record EnrolmentRedemptionResult(IDeviceOwner Owner, string PlaintextToken);
