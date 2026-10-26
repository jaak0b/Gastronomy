using GastronomyApp.Contracts.Enrolment;
using GastronomyApp.Contracts.Events;
using GastronomyApp.Contracts.Session;
using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Results;
using Mapster;

namespace GastronomyApp.Core.Mapping;

public sealed class EnrolmentMapping : IRegister
{
  public void Register(TypeAdapterConfig config)
  {
    ArgumentNullException.ThrowIfNull(config);

    config.NewConfig<IssuedEnrolmentInvitation, InvitationView>()
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

    config.NewConfig<StaffMember, SessionView>()
          .Map(view => view.DeviceId, staffMember => staffMember.Device!.Id)
          .Map(view => view.Language, staffMember => staffMember.Device!.Language)
          .Map(view => view.StaffMember, staffMember => staffMember)
          .Map(view => view.Station, staffMember => (Station?)null);

    config.NewConfig<Station, SessionView>().Map(view => view.DeviceId, station => station.Device!.Id).Map(view => view.Language, station => station.Device!.Language).Map(view => view.StaffMember, station => (StaffMember?)null).Map(view => view.Station, station => station);
  }
}
