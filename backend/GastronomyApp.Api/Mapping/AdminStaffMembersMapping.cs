using GastronomyApp.Contracts;
using GastronomyApp.Core.ReadModels;
using Mapster;

namespace GastronomyApp.Api.Mapping;

public sealed class AdminStaffMembersMapping : IRegister
{
  public void Register(TypeAdapterConfig config)
  {
    ArgumentNullException.ThrowIfNull(config);

    config.NewConfig<AdministeredStaffMember, AdminStaffMemberView>();
  }
}
