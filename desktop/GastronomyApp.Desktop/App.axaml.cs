﻿﻿using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using GastronomyApp.Core.Services;
using GastronomyApp.Desktop.Services;
using GastronomyApp.Desktop.ViewModels;
using GastronomyApp.Desktop.Views;

namespace GastronomyApp.Desktop;

public class App : Application
{
  private readonly Never _never = new();
  private AppBootstrapper? _bootstrapper;
  private DesktopComposition? _composition;
  private MainWindow? _mainWindow;
  private MainWindowViewModel? _mainWindowViewModel;
  private QuitConfirmViewModel? _quitConfirmViewModel;
  private TrayIcon? _trayIcon;

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
    _composition = new();
    _bootstrapper = new(_composition.SingleInstance,
                       _composition.CreateMainWindowViewModel,
                       BringMainWindowToFront,
                       work => Dispatcher.UIThread.Post(work));

    var outcome = _bootstrapper.Start();

    if (outcome == BootstrapOutcome.ExitImmediately)
    {
      ExitOnceTheDispatcherRuns(lifetime);

      return;
    }

    lifetime.ShutdownMode = ShutdownMode.OnExplicitShutdown;
    lifetime.ShutdownRequested += OnShutdownRequested;

    _mainWindowViewModel = _bootstrapper.MainWindowViewModel!;
    _quitConfirmViewModel = _composition.CreateQuitConfirmViewModel(_mainWindowViewModel,
                                                                  () => lifetime.Shutdown());

    _mainWindowViewModel.AdminPagesRequested += OpenAdminPages;
    _mainWindowViewModel.DataFolderRequested += OpenDataFolder;
    _mainWindowViewModel.RepairRequested += RepairSetup;
    _mainWindowViewModel.QuitRequested += AskWhetherToQuit;
    _mainWindowViewModel.FailureDetailRequested += ShowFailureDetail;

    _mainWindow = new() { DataContext = _mainWindowViewModel };

    if (ServesTheOrderPages(outcome))
    {
      _mainWindow.Opened += OnMainWindowOpened;
    }

    lifetime.MainWindow = _mainWindow;

    CreateTrayIcon();
  }

  internal void ExitOnceTheDispatcherRuns(IClassicDesktopStyleApplicationLifetime lifetime)
  {
    // Avalonia shuts its dispatcher down immediately when Shutdown runs before the main loop,
    // and then the loop itself fails to start.
    Dispatcher.UIThread.Post(() => lifetime.Shutdown());
  }

  private bool ServesTheOrderPages(BootstrapOutcome outcome)
  {
    return outcome switch
           {
             BootstrapOutcome.ProceedToWindow => true,
             BootstrapOutcome.ProceedToWindowWithoutServer => false,
             BootstrapOutcome.ExitImmediately => false,
             _ => _never.OfType<bool>(outcome)
           };
  }

  private void CreateTrayIcon()
  {
    if (_mainWindowViewModel is null)
    {
      return;
    }

    NativeMenu menu = new();
    menu.Items.Add(new NativeMenuItem
                   {
                     Header = _mainWindowViewModel.AdminButtonLabel,
                     Command = _mainWindowViewModel.OpenAdminPagesCommand
                   });
    menu.Items.Add(new NativeMenuItem
                   {
                     Header = _mainWindowViewModel.QuitButtonLabel,
                     Command = _mainWindowViewModel.RequestQuitCommand
                   });

    _trayIcon = new()
               {
                 ToolTipText = _mainWindowViewModel.MinimisedText,
                 IsVisible = true,
                 Menu = menu
               };

    _trayIcon.Clicked += OnTrayIconClicked;
    TrayIcon.SetIcons(this, [_trayIcon]);
  }

  private void OnTrayIconClicked(object? sender, EventArgs eventArgs)
  {
    BringMainWindowToFront();
  }

  private void BringMainWindowToFront()
  {
    _mainWindow?.BringToFront();
  }

  private async void OnMainWindowOpened(object? sender, EventArgs eventArgs)
  {
    if (_mainWindow is not null)
    {
      _mainWindow.Opened -= OnMainWindowOpened;
    }

    await RunFirstRunThenStartAsync();
  }

  private async Task RunFirstRunThenStartAsync()
  {
    if (_composition is null || _mainWindowViewModel is null || _mainWindow is null)
    {
      return;
    }

    var firstRun = _composition.CreateFirstRunViewModel();
    firstRun.Evaluate();

    if (firstRun.IsSetupOffered)
    {
      FirstRunDialog dialog = new() { DataContext = firstRun };
      var accepted = await dialog.ShowDialog<bool>(_mainWindow);

      if (accepted)
      {
        await firstRun.RunSetupAsync();
      }
      else
      {
        firstRun.Decline();
      }

      if (firstRun.SetupFailed)
      {
        _mainWindowViewModel.ShowSetupFailed();
      }
      else if (firstRun.DeclinedText is not null)
      {
        _mainWindowViewModel.ShowSetupDeclined();
      }
    }

    await _mainWindowViewModel.StartAsync();
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
    if (_composition is null || _mainWindowViewModel is null)
    {
      return;
    }

    var outcome = await _composition.ElevatedSetupLauncher.RunElevatedSetupAsync();

    _mainWindowViewModel.ShowRepairOutcome(outcome);
  }

  private void OpenDataFolder()
  {
    if (_composition is null)
    {
      return;
    }

    Process.Start(new ProcessStartInfo
                  {
                    FileName = _composition.DataDirectoryPath,
                    UseShellExecute = true
                  });
  }

  private void ShowFailureDetail()
  {
    if (_mainWindowViewModel is null || _mainWindow is null)
    {
      return;
    }

    FailureDetailDialog dialog = new() { DataContext = _mainWindowViewModel };
    dialog.ShowDialog(_mainWindow);
  }

  private async void AskWhetherToQuit()
  {
    if (_quitConfirmViewModel is null || _mainWindow is null)
    {
      return;
    }

    _quitConfirmViewModel.RequestQuit();

    QuitConfirmDialog dialog = new() { DataContext = _quitConfirmViewModel };
    var confirmed = await dialog.ShowDialog<bool>(_mainWindow);

    if (confirmed)
    {
      await _quitConfirmViewModel.ConfirmAsync();

      return;
    }

    _quitConfirmViewModel.Cancel();
  }

  private void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs eventArgs)
  {
    _trayIcon?.Dispose();
    _bootstrapper?.Release();
  }
}
