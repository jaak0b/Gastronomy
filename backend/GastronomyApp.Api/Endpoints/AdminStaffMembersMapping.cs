using GastronomyApp.Api.Contracts;
using GastronomyApp.Core.ReadModels;
using Mapster;

namespace GastronomyApp.Api.Endpoints;

public sealed class AdminStaffMembersMapping : IRegister
{
  public void Register(TypeAdapterConfig config)
  {
    ArgumentNullException.ThrowIfNull(config);

    config.NewConfig<AdministeredStaffMember, AdminStaffMemberView>();
  }
}
