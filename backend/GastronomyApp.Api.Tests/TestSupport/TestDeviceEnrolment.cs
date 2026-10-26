using GastronomyApp.Core.Ports;
using GastronomyApp.Core.Services;
using GastronomyApp.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace GastronomyApp.Api.Tests.TestSupport;

public sealed record EnrolledDevice(Guid DeviceId, string PlaintextToken);

public sealed class TestDeviceEnrolment
{
  private readonly IServiceProvider _services;

  public TestDeviceEnrolment(IServiceProvider services)
  {
    _services = services;
  }

  public async Task<EnrolledDevice> EnrolStaffMemberAsync(Guid staffMemberId)
  {
    using var scope = _services.CreateScope();
    scope.ServiceProvider.GetRequiredService<AfterCommitActions>().StartCollecting();

    return await RedeemAsync(scope.ServiceProvider, staffMemberId, null);
  }

  public async Task<EnrolledDevice> EnrolStationAsync(Guid stationId)
  {
    using var scope = _services.CreateScope();
    scope.ServiceProvider.GetRequiredService<AfterCommitActions>().StartCollecting();

    return await RedeemAsync(scope.ServiceProvider, null, stationId);
  }

  public async Task<string> CreateInvitationCodeAsync(Guid? staffMemberId)
  {
    using var scope = _services.CreateScope();
    scope.ServiceProvider.GetRequiredService<AfterCommitActions>().StartCollecting();

    return await CreateInvitationCodeAsync(scope.ServiceProvider, staffMemberId, null);
  }

  private async Task<EnrolledDevice> RedeemAsync(IServiceProvider scopedServices, Guid? staffMemberId, Guid? stationId)
  {
    var code = await CreateInvitationCodeAsync(scopedServices, staffMemberId, stationId);
    var redeemed = await scopedServices.GetRequiredService<EnrolmentService>().RedeemAsync(new() { Code = code }, "de", CancellationToken.None);

    return new(redeemed.Value.DeviceId, redeemed.Value.DeviceToken);
  }

  private async Task<string> CreateInvitationCodeAsync(IServiceProvider scopedServices, Guid? staffMemberId, Guid? stationId)
  {
    var created = await scopedServices.GetRequiredService<EnrolmentService>().CreateInvitationAsync(staffMemberId, stationId, CancellationToken.None);

    if (created.IsError)
      throw new InvalidOperationException($"The test invitation was refused: {created.FirstError.Description}");

    return scopedServices.GetRequiredService<IOutstandingInvitationCache>().Read()!.QRCodeValue;
  }
}
