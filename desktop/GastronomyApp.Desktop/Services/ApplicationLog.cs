using Serilog;
using Serilog.Events;

namespace GastronomyApp.Desktop.Services;

public sealed class ApplicationLog
{
  private const string LogFolderName = "logs";
  private const string LogFileName = "gastronomy-.log";
  private const int RetainedFiles = 14;

  public string Start(string dataDirectory)
  {
    var folder = Path.Combine(dataDirectory, LogFolderName);
    Directory.CreateDirectory(folder);

    var path = Path.Combine(folder, LogFileName);

    var logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
                .WriteTo.File(path,
                              rollingInterval: RollingInterval.Day,
                              retainedFileCountLimit: RetainedFiles,
                              shared: true)
                .CreateLogger();

    Log.Logger = logger;

    return folder;
  }

  public void Stop()
  {
    Log.CloseAndFlush();
  }
}
