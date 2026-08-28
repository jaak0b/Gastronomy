using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace GastronomyApp.Desktop.Services.Windows;

[SupportedOSPlatform("windows")]
public sealed class WindowsPowerManager : IPowerManager
{
  private const uint ExecutionStateContinuous = 0x80000000;
  private const uint ExecutionStateSystemRequired = 0x00000001;

  public void PreventSleep()
  {
    SetThreadExecutionState(ExecutionStateContinuous | ExecutionStateSystemRequired);
  }

  public void AllowSleep()
  {
    SetThreadExecutionState(ExecutionStateContinuous);
  }

  [DllImport("kernel32.dll", SetLastError = true)]
  private extern static uint SetThreadExecutionState(uint executionState);
}

public sealed class NoOpPowerManager : IPowerManager
{
  public void PreventSleep()
  {
  }

  public void AllowSleep()
  {
  }
}
