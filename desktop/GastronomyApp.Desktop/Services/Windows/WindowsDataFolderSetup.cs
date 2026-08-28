using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace GastronomyApp.Desktop.Services.Windows;

public sealed class DataFolderSetup : IDataFolderSetup
{

  public DataFolderSetup(string dataDirectoryPath)
  {
    this.DataDirectoryPath = dataDirectoryPath;
  }

  public string DataDirectoryPath { get; }

  public bool Exists()
  {
    return Directory.Exists(DataDirectoryPath);
  }

  public bool CurrentUserCanWrite()
  {
    if (!Exists())
    {
      return false;
    }

    var probe = Path.Combine(DataDirectoryPath, $"write-probe-{Guid.NewGuid():N}.tmp");

    try
    {
      File.WriteAllText(probe, string.Empty);
      File.Delete(probe);

      return true;
    }
    catch (UnauthorizedAccessException)
    {
      return false;
    }
    catch (IOException)
    {
      return false;
    }
  }

  public void CreateWithUsersModifyGrant()
  {
    Directory.CreateDirectory(DataDirectoryPath);
    GrantUsersModifyOnExisting();
  }

  public void GrantUsersModifyOnExisting()
  {
    if (!OperatingSystem.IsWindows())
    {
      return;
    }

    GrantOnWindows();
  }

  [SupportedOSPlatform("windows")]
  private void GrantOnWindows()
  {
    DirectoryInfo directory = new(DataDirectoryPath);
    var security = directory.GetAccessControl();

    security.AddAccessRule(new(new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
                               FileSystemRights.Modify,
                               InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                               PropagationFlags.None,
                               AccessControlType.Allow));

    directory.SetAccessControl(security);
  }
}
