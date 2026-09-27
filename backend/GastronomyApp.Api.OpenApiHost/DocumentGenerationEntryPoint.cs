using GastronomyApp.Api;

var application = new GastronomyAppApiApplication().Build(new()
{
  DataDirectory = Path.Combine(Path.GetTempPath(), "GastronomyAppOpenApi"),
  Port = 0,
  BindAddress = "127.0.0.1"
});

application.Run();
