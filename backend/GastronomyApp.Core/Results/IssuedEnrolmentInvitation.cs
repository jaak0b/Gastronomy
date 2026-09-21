using GastronomyApp.Core.Entities;

namespace GastronomyApp.Core.Results;

public sealed record IssuedEnrolmentInvitation(EnrolmentInvitation Invitation, string QRCodeValue, IDeviceOwner? Owner);
