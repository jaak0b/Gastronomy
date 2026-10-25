using GastronomyApp.Contracts.Admin.Staff;
using GastronomyApp.Core.Entities;
using Mapster;

namespace GastronomyApp.Core.Mapping;

public sealed class StaffMemberMapping : IRegister
{
  public void Register(TypeAdapterConfig config)
  {
    ArgumentNullException.ThrowIfNull(config);

    config.NewConfig<StaffMember, StaffMemberView>();

    config.NewConfig<StaffMember, AdminStaffMemberView>().Map(view => view.StaffMemberId, staffMember => staffMember.Id).Map(view => view.HasDevice, staffMember => staffMember.DeviceId != null);
  }
}
