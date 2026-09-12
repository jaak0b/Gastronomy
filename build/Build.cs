using System.Text;
using System.Text.Json;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Serilog;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using static Nuke.Common.Tools.Git.GitTasks;

internal class Build : NukeBuild
{
  public static int Main() => Execute<Build>(x => x.Test);

  [Parameter("Configuration to build: default is 'Debug' (local) or 'Release' (server)")]
  private readonly string Configuration = IsLocalBuild ? "Debug" : "Release";

  private AbsolutePath FrontendDirectory => RootDirectory / "frontend";
  private AbsolutePath SolutionFile => RootDirectory / "GastronomyApp.slnx";
  private AbsolutePath DesktopProject => RootDirectory / "desktop" / "GastronomyApp.Desktop" / "GastronomyApp.Desktop.csproj";
  private AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";
  private AbsolutePath DesktopPublishDirectory => ArtifactsDirectory / "desktop";
  private AbsolutePath VelopackDirectory => ArtifactsDirectory / "velopack";
  private AbsolutePath AppIcon => RootDirectory / "desktop" / "GastronomyApp.Desktop" / "Assets" / "avalonia-logo.ico";

  private const string GitHubRepoUrl = "https://github.com/jaak0b/Gastronomy";

  [Parameter("GitHub token for publishing releases. Defaults to the GH_TOKEN or GITHUB_TOKEN environment variable.")]
  private readonly string? GitHubToken = Environment.GetEnvironmentVariable("GH_TOKEN")
      ?? Environment.GetEnvironmentVariable("GITHUB_TOKEN");

  private string[] TestProjects =>
  [
    "backend/GastronomyApp.Core.Tests/GastronomyApp.Core.Tests.csproj",
    "backend/GastronomyApp.Infrastructure.Tests/GastronomyApp.Infrastructure.Tests.csproj",
    "backend/GastronomyApp.Api.Tests/GastronomyApp.Api.Tests.csproj",
    "desktop/GastronomyApp.Desktop.Tests/GastronomyApp.Desktop.Tests.csproj"
  ];

  private Target Restore => _ => _
      .Executes(() =>
      {
        DotNetRestore(s => s
            .SetProjectFile(SolutionFile));
      });

  private Target BuildFrontend => _ => _
      .Executes(() =>
      {
        var npm = ResolveNpmExecutable();
        ProcessTasks.StartProcess(npm, "ci --no-audit --no-fund", FrontendDirectory).AssertZeroExitCode();
        ProcessTasks.StartProcess(npm, "run build", FrontendDirectory).AssertZeroExitCode();
      });

  private Target Compile => _ => _
      .DependsOn(Restore, BuildFrontend)
      .Executes(() =>
      {
        DotNetBuild(s => s
            .SetProjectFile(SolutionFile)
            .SetConfiguration(Configuration)
            .EnableNoRestore());
      });

  private Target TestFrontend => _ => _
      .DependsOn(BuildFrontend)
      .Executes(() =>
      {
        ProcessTasks.StartProcess(ResolveNpmExecutable(), "test -- --run", FrontendDirectory).AssertZeroExitCode();
      });

  private Target Test => _ => _
      .DependsOn(Compile, TestFrontend)
      .Executes(() =>
      {
        DotNetTest(s => s
                .SetConfiguration(Configuration)
                .EnableNoRestore()
                .EnableNoBuild()
                .CombineWith(TestProjects, (settings, project) => settings
                    .SetProjectFile(RootDirectory / project)),
            degreeOfParallelism: TestProjects.Length,
            completeOnFailure: true);
      });

  private Target PublishDesktop => _ => _
      .Description("Publishes the Windows desktop app self-contained (win-x64) for packaging.")
      .DependsOn(BuildFrontend)
      .Executes(() =>
      {
        DesktopPublishDirectory.CreateOrCleanDirectory();
        DotNetPublish(s => s
            .SetProject(DesktopProject)
            .SetConfiguration("Release")
            .SetRuntime("win-x64")
            .SetSelfContained(true)
            .SetOutput(DesktopPublishDirectory));
      });

  private Target Pack => _ => _
      .Description("Builds the Velopack Windows installer and update feed into artifacts/velopack.")
      .DependsOn(PublishDesktop)
      .Executes(() =>
      {
        VelopackDirectory.CreateOrCleanDirectory();
        var notesFile = WriteReleaseNotes();

        Vpk($"pack"
            + $" --packId GastronomyApp"
            + $" --packTitle GastronomyApp"
            + $" --packAuthors Jakob"
            + $" --packVersion {ReleaseVersion()}"
            + $" --packDir \"{DesktopPublishDirectory}\""
            + $" --mainExe GastronomyApp.Desktop.exe"
            + $" --icon \"{AppIcon}\""
            + $" --releaseNotes \"{notesFile}\""
            + $" --outputDir \"{VelopackDirectory}\"");

        Log.Information("Velopack output: {Dir}", VelopackDirectory);
      });

  private Target Release => _ => _
      .Description("Publishes a GitHub release with the Windows installer, the update feed and commit-message notes.")
      .DependsOn(Test, Pack)
      .Requires(() => GitHubToken)
      .Executes(() =>
      {
        var version = ReleaseVersion();
        var tag = $"v{version}";

        Vpk($"upload github"
            + $" --repoUrl {GitHubRepoUrl}"
            + $" --token {GitHubToken}"
            + $" --publish"
            + $" --releaseName \"GastronomyApp {version}\""
            + $" --tag {tag}"
            + $" --outputDir \"{VelopackDirectory}\"",
            logInvocation: false);

        Log.Information("Published release {Tag}", tag);
      });

  private static string ResolveNpmExecutable()
  {
    try
    {
      return ToolPathResolver.GetPathExecutable("npm");
    }
    catch (Exception) when (EnvironmentInfo.IsWin)
    {
      Log.Warning("npm was not found on PATH; using npm.cmd");
      return ToolPathResolver.GetPathExecutable("npm.cmd");
    }
  }

  private void Vpk(string arguments, bool logInvocation = true)
  {
    var dotnet = ToolPathResolver.GetPathExecutable("dotnet");
    string command = "vpk " + arguments;
    ProcessTasks.StartProcess(dotnet, command, RootDirectory, logInvocation: logInvocation).AssertZeroExitCode();
  }

  private string ReleaseVersion()
  {
    var dotnet = ToolPathResolver.GetPathExecutable("dotnet");
    var process = ProcessTasks.StartProcess(dotnet,
        "nbgv get-version --variable SimpleVersion", RootDirectory, logOutput: false);
    process.AssertZeroExitCode();
    return process.Output
        .Where(o => o.Type == OutputType.Std)
        .Select(o => o.Text.Trim())
        .First(t => t.Length > 0);
  }

  private AbsolutePath WriteReleaseNotes()
  {
    var body = NotesFromPullRequests() ?? NotesFromCommits();

    var notesFile = ArtifactsDirectory / "release-notes.md";
    ArtifactsDirectory.CreateDirectory();
    notesFile.WriteAllText(body);
    return notesFile;
  }

  private string? NotesFromPullRequests()
  {
    if (string.IsNullOrEmpty(GitHubToken)) return null;

    try
    {
      var headSha = GitLines("rev-parse HEAD").FirstOrDefault()?.Trim();
      var previousTag = GitLines("tag --list v* --sort=-version:refname").FirstOrDefault()?.Trim();

      var arguments = new StringBuilder($"api repos/{GitHubRepoSlug}/releases/generate-notes")
          .Append($" -f tag_name=v{ReleaseVersion()}");
      if (!string.IsNullOrEmpty(headSha))
        arguments.Append($" -f target_commitish={headSha}");
      if (!string.IsNullOrEmpty(previousTag))
        arguments.Append($" -f previous_tag_name={previousTag}");

      var gh = ToolPathResolver.GetPathExecutable("gh");
      var process = ProcessTasks.StartProcess(gh, arguments.ToString(), RootDirectory, GitHubEnvironment(), logOutput: false);
      process.AssertZeroExitCode();

      var json = string.Join(Environment.NewLine,
          process.Output.Where(o => o.Type == OutputType.Std).Select(o => o.Text));
      using var document = JsonDocument.Parse(json);
      var body = document.RootElement.GetProperty("body").GetString();

      return !string.IsNullOrWhiteSpace(body) && DescribesChanges(body) ? body.Trim() : null;
    }
    catch (Exception ex)
    {
      Log.Warning(ex, "gh generate-notes failed; falling back to commit-based release notes");
      return null;
    }
  }

  private static bool DescribesChanges(string body)
  {
    return body
        .Split('\n')
        .Select(line => line.Trim())
        .Any(line => line.Length > 0
                     && !line.StartsWith("**Full Changelog**", StringComparison.OrdinalIgnoreCase));
  }

  private string NotesFromCommits()
  {
    var previousTag = GitLines("tag --list v* --sort=-version:refname").FirstOrDefault()?.Trim();
    var range = string.IsNullOrEmpty(previousTag) ? "HEAD" : $"{previousTag}..HEAD";
    var commits = GitLines($"log {range} --no-merges --pretty=format:-%x20%s")
        .Select(l => l.Trim())
        .Where(l => l.Length > 0)
        .ToList();

    return commits.Count > 0
        ? string.Join(Environment.NewLine, commits)
        : "Maintenance release.";
  }

  private IEnumerable<string> GitLines(string arguments) =>
      Git(arguments, workingDirectory: RootDirectory, logOutput: false)
          .Where(o => o.Type == OutputType.Std)
          .Select(o => o.Text);

  private string GitHubRepoSlug => new Uri(GitHubRepoUrl).AbsolutePath.Trim('/');

  private IReadOnlyDictionary<string, string> GitHubEnvironment()
  {
    var environment = Environment.GetEnvironmentVariables()
        .Cast<System.Collections.DictionaryEntry>()
        .ToDictionary(entry => (string)entry.Key, entry => (string)entry.Value!);
    environment["GH_TOKEN"] = GitHubToken ?? string.Empty;
    return environment;
  }
}
