using GastronomyApp.Contracts.Admin.Staff;
using GastronomyApp.Contracts.Enrolment;
using GastronomyApp.Contracts.Events;
using GastronomyApp.Contracts.Session;
using GastronomyApp.Contracts.Stations;
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
          .Map(view => view.StaffMember, issued => AsStaffMember(issued.Owner))
          .Map(view => view.Station, issued => AsStation(issued.Owner));

    config.NewConfig<EnrolmentRedemptionResult, RedeemedEnrolmentView>()
          .Map(view => view.DeviceId, redemption => redemption.Owner.Device!.Id)
          .Map(view => view.DeviceToken, redemption => redemption.PlaintextToken)
          .Map(view => view.StaffMember, redemption => AsStaffMember(redemption.Owner))
          .Map(view => view.Station, redemption => AsStation(redemption.Owner))
          .Map(view => view.Language, redemption => redemption.Owner.Device!.Language);

    config.NewConfig<IDeviceOwner, EnrolmentCompletedEvent>()
          .Map(payload => payload.StaffMemberId, owner => IdOfStaffMember(owner))
          .Map(payload => payload.StationId, owner => IdOfStation(owner))
          .Map(payload => payload.OwnerName, owner => owner.Name)
          .Map(payload => payload.DeviceId, owner => owner.Device!.Id);

    config.NewConfig<IDeviceOwner, SessionView>().Ignore(view => view.DeviceId).Ignore(view => view.Language).Map(view => view.StaffMember, owner => AsStaffMember(owner)).Map(view => view.Station, owner => AsStation(owner));
  }

  private Guid? IdOfStaffMember(IDeviceOwner? owner)
  {
    return AsStaffMember(owner)?.Id;
  }

  private Guid? IdOfStation(IDeviceOwner? owner)
  {
    return AsStation(owner)?.Id;
  }

  private StaffMemberView? AsStaffMember(IDeviceOwner? owner)
  {
    if (owner is not StaffMember staffMember)
      return null;

    return new(staffMember.Id, staffMember.Name);
  }

  private StationSummaryView? AsStation(IDeviceOwner? owner)
  {
    if (owner is not Station station)
      return null;

    return new(station.Id, station.Name);
  }
}
