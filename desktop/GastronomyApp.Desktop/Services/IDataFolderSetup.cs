namespace GastronomyApp.Desktop.Services;

public interface IDataFolderSetup
{
  public string DataDirectoryPath { get; }

  public bool Exists();

  public bool CurrentUserCanWrite();

  public void CreateWithUsersModifyGrant();

  public void GrantUsersModifyOnExisting();
}
