using GastronomyApp.Core.Entities;
using GastronomyApp.Core.Ports;
using Microsoft.EntityFrameworkCore;

namespace GastronomyApp.Infrastructure.Repositories;

public sealed class DeviceRepository : IDeviceRepository
{
  private readonly GastronomyAppDbContext _dbContext;

  public DeviceRepository(GastronomyAppDbContext dbContext)
  {
    _dbContext = dbContext;
  }

  public async Task<Device?> FindByIdAsync(Guid deviceId, CancellationToken cancellationToken)
  {
    return await _dbContext.Devices
                           .FirstOrDefaultAsync(device => device.Id == deviceId, cancellationToken);
  }

  public async Task SaveChangesAsync(CancellationToken cancellationToken)
  {
    await _dbContext.SaveChangesAsync(cancellationToken);
  }
}
