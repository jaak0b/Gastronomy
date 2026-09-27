using GastronomyApp.Contracts.Admin.Staff;
using GastronomyApp.Core.Entities;
using Mapster;

namespace GastronomyApp.Api.Mapping;

public sealed class AdminStaffMembersMapping : IMappingRegistration
{
  public void Register(TypeAdapterConfig config)
  {
    ArgumentNullException.ThrowIfNull(config);

    config.NewConfig<StaffMember, StaffMemberView>();

    config.NewConfig<StaffMember, AdminStaffMemberView>()
          .Map(view => view.StaffMemberId, staffMember => staffMember.Id)
          .Map(view => view.HasDevice, staffMember => staffMember.DeviceId != null);
  }
}
