using GastronomyApp.Contracts.Enrolment;
using GastronomyApp.Contracts.Events;
using GastronomyApp.Contracts.Session;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Results;
using Mapster;

namespace GastronomyApp.Api.Mapping;

public sealed class EnrolmentMapping : IMappingRegistration
{
  public void Register(TypeAdapterConfig config)
  {
    ArgumentNullException.ThrowIfNull(config);

    config.NewConfig<IssuedEnrolmentInvitation, InvitationView>()
          .Ignore(view => view.QRUrl)
          .Ignore(view => view.AvailableAddresses)
          .Map(view => view.InvitationId, issued => issued.Invitation.Id)
          .Map(view => view.ExpiresAtUtc, issued => issued.Invitation.ExpiresAtUtc)
          .Map(view => view.StaffMember, issued => issued.Owner as StaffMember)
          .Map(view => view.Station, issued => issued.Owner as Station);

    config.NewConfig<EnrolmentRedemptionResult, RedeemedEnrolmentView>()
          .Map(view => view.DeviceId, redemption => redemption.Owner.Device!.Id)
          .Map(view => view.DeviceToken, redemption => redemption.PlaintextToken)
          .Map(view => view.StaffMember, redemption => redemption.Owner as StaffMember)
          .Map(view => view.Station, redemption => redemption.Owner as Station)
          .Map(view => view.Language, redemption => redemption.Owner.Device!.Language);

    config.NewConfig<IDeviceOwner, EnrolmentCompletedEvent>()
          .Map(payload => payload.StaffMemberId, owner => (Guid?)((StaffMember)owner).Id, owner => owner is StaffMember)
          .Map(payload => payload.StationId, owner => (Guid?)((Station)owner).Id, owner => owner is Station)
          .Map(payload => payload.OwnerName, owner => owner.Name)
          .Map(payload => payload.DeviceId, owner => owner.Device!.Id);

    config.NewConfig<IDeviceOwner, SessionView>()
          .Ignore(view => view.DeviceId)
          .Ignore(view => view.Language)
          .Map(view => view.StaffMember, owner => owner as StaffMember)
          .Map(view => view.Station, owner => owner as Station);
  }
}
