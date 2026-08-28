using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace GastronomyApp.Desktop.Services.Windows;

public sealed class DataFolderSetup : IDataFolderSetup
{
  private readonly string dataDirectoryPath;

  public DataFolderSetup(string dataDirectoryPath)
  {
    this.dataDirectoryPath = dataDirectoryPath;
  }

  public string DataDirectoryPath => dataDirectoryPath;

  public bool Exists()
  {
    return Directory.Exists(dataDirectoryPath);
  }

  public bool CurrentUserCanWrite()
  {
    if (!Exists())
    {
      return false;
    }

    string probe = Path.Combine(dataDirectoryPath, $"write-probe-{Guid.NewGuid():N}.tmp");

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
    Directory.CreateDirectory(dataDirectoryPath);
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
    DirectoryInfo directory = new(dataDirectoryPath);
    DirectorySecurity security = directory.GetAccessControl();

    security.AddAccessRule(new FileSystemAccessRule(
        new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
        FileSystemRights.Modify,
        InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
        PropagationFlags.None,
        AccessControlType.Allow));

    directory.SetAccessControl(security);
  }
}
