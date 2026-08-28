using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using GastronomyApp.Desktop.Services;
using GastronomyApp.Desktop.ViewModels;
using GastronomyApp.Desktop.Views;

namespace GastronomyApp.Desktop;

public class App : Application
{
  private AppBootstrapper? bootstrapper;
  private DesktopComposition? composition;
  private MainWindow? mainWindow;
  private MainWindowViewModel? mainWindowViewModel;
  private QuitConfirmViewModel? quitConfirmViewModel;
  private TrayIcon? trayIcon;

  override public void Initialize()
  {
    AvaloniaXamlLoader.Load(this);
  }

  override public void OnFrameworkInitializationCompleted()
  {
    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
    {
      StartDesktop(lifetime);
    }

    base.OnFrameworkInitializationCompleted();
  }

  private void StartDesktop(IClassicDesktopStyleApplicationLifetime lifetime)
  {
    composition = new();
    bootstrapper = new(composition.SingleInstance,
                       composition.CreateMainWindowViewModel,
                       BringMainWindowToFront,
                       work => Dispatcher.UIThread.Post(work));

    if (bootstrapper.Start() == BootstrapOutcome.ExitImmediately)
    {
      lifetime.Shutdown();

      return;
    }

    lifetime.ShutdownMode = ShutdownMode.OnExplicitShutdown;
    lifetime.ShutdownRequested += OnShutdownRequested;

    mainWindowViewModel = bootstrapper.MainWindowViewModel!;
    quitConfirmViewModel = composition.CreateQuitConfirmViewModel(mainWindowViewModel,
                                                                  () => lifetime.Shutdown());

    mainWindowViewModel.AdminPagesRequested += OpenAdminPages;
    mainWindowViewModel.DataFolderRequested += OpenDataFolder;
    mainWindowViewModel.RepairRequested += RepairSetup;
    mainWindowViewModel.QuitRequested += AskWhetherToQuit;

    mainWindow = new() { DataContext = mainWindowViewModel };
    mainWindow.Opened += OnMainWindowOpened;
    lifetime.MainWindow = mainWindow;

    CreateTrayIcon();
  }

  private void CreateTrayIcon()
  {
    if (mainWindowViewModel is null)
    {
      return;
    }

    NativeMenu menu = new();
    menu.Items.Add(new NativeMenuItem
                   {
                     Header = mainWindowViewModel.AdminButtonLabel,
                     Command = mainWindowViewModel.OpenAdminPagesCommand
                   });
    menu.Items.Add(new NativeMenuItem
                   {
                     Header = mainWindowViewModel.QuitButtonLabel,
                     Command = mainWindowViewModel.RequestQuitCommand
                   });

    trayIcon = new()
               {
                 ToolTipText = mainWindowViewModel.MinimisedText,
                 IsVisible = true,
                 Menu = menu
               };

    trayIcon.Clicked += OnTrayIconClicked;
    TrayIcon.SetIcons(this, [trayIcon]);
  }

  private void OnTrayIconClicked(object? sender, EventArgs eventArgs)
  {
    BringMainWindowToFront();
  }

  private void BringMainWindowToFront()
  {
    mainWindow?.BringToFront();
  }

  private async void OnMainWindowOpened(object? sender, EventArgs eventArgs)
  {
    if (mainWindow is not null)
    {
      mainWindow.Opened -= OnMainWindowOpened;
    }

    await RunFirstRunThenStartAsync();
  }

  private async Task RunFirstRunThenStartAsync()
  {
    if (composition is null || mainWindowViewModel is null || mainWindow is null)
    {
      return;
    }

    var firstRun = composition.CreateFirstRunViewModel();
    firstRun.Evaluate();

    if (firstRun.IsSetupOffered)
    {
      FirstRunDialog dialog = new() { DataContext = firstRun };
      var accepted = await dialog.ShowDialog<bool>(mainWindow);

      if (accepted)
      {
        await firstRun.RunSetupAsync();
      }
      else
      {
        firstRun.Decline();
      }

      mainWindowViewModel.NoticeText = firstRun.DeclinedText;
    }

    await mainWindowViewModel.StartAsync();
  }

  private void OpenAdminPages(string adminUrl)
  {
    Process.Start(new ProcessStartInfo
                  {
                    FileName = adminUrl,
                    UseShellExecute = true
                  });
  }

  private async void RepairSetup()
  {
    if (composition is null || mainWindowViewModel is null)
    {
      return;
    }

    var outcome = await composition.ElevatedSetupLauncher.RunElevatedSetupAsync();

    mainWindowViewModel.ShowRepairOutcome(outcome);
  }

  private void OpenDataFolder()
  {
    if (composition is null)
    {
      return;
    }

    Process.Start(new ProcessStartInfo
                  {
                    FileName = composition.DataDirectoryPath,
                    UseShellExecute = true
                  });
  }

  private async void AskWhetherToQuit()
  {
    if (quitConfirmViewModel is null || mainWindow is null)
    {
      return;
    }

    quitConfirmViewModel.RequestQuit();

    QuitConfirmDialog dialog = new() { DataContext = quitConfirmViewModel };
    var confirmed = await dialog.ShowDialog<bool>(mainWindow);

    if (confirmed)
    {
      await quitConfirmViewModel.ConfirmAsync();

      return;
    }

    quitConfirmViewModel.Cancel();
  }

  private void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs eventArgs)
  {
    trayIcon?.Dispose();
    bootstrapper?.Release();
  }
}
