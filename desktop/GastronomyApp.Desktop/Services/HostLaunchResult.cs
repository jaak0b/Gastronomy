using Microsoft.AspNetCore.Builder;

namespace GastronomyApp.Desktop.Services;

public abstract record HostLaunchResult
{
  public sealed record Started(WebApplication Application) : HostLaunchResult;

  public sealed record PortInUse(int Port) : HostLaunchResult;

  public sealed record DataFolderNotWritable(string Path) : HostLaunchResult;

  public sealed record NoNetworkAvailable : HostLaunchResult;

  public sealed record StartFailed(Exception Failure) : HostLaunchResult;
}
