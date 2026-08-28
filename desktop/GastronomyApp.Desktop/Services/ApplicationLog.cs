using Serilog;
using Serilog.Core;

namespace GastronomyApp.Desktop.Services;

public sealed class ApplicationLog
{
    private const string LogFolderName = "logs";
    private const string LogFileName = "gastronomy-.log";
    private const int RetainedFiles = 14;

    public string Start(string dataDirectory)
    {
        string folder = Path.Combine(dataDirectory, LogFolderName);
        Directory.CreateDirectory(folder);

        string path = Path.Combine(folder, LogFileName);

        Logger logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning)
            .WriteTo.File(
                path,
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
