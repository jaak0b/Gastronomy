using GastronomyApp.Core.ReadModels;
using GastronomyApp.Infrastructure.QueryRows;
using Mapster;

namespace GastronomyApp.Infrastructure.Projections;

public sealed class StaffMemberProjection : IRegister
{
  public void Register(TypeAdapterConfig config)
  {
    ArgumentNullException.ThrowIfNull(config);

    config.NewConfig<AdministeredStaffMemberRow, AdministeredStaffMember>()
          .Map(administered => administered.StaffMemberId, row => row.StaffMember.Id)
          .Map(administered => administered.Name, row => row.StaffMember.Name)
          .Map(administered => administered.IsActive, row => row.StaffMember.IsActive)
          .Map(administered => administered.HasDevice, row => row.StaffMember.DeviceId != null);
  }
}
