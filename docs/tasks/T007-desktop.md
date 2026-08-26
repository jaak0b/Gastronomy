# T007: Desktop host

## 1. Objective

Build `GastronomyApp.Desktop` into the Avalonia window described in spec sections 10.1 to 10.3 and
10.5/10.6: it starts and stops the T005 web application in-process, shows the address with a QR code,
running state, one attention indicator, phones-set-up count, and the admin/settings/quit buttons; it
minimises on close and quits only through a confirmed action; it enforces single instance; it keeps
the laptop awake while running; it creates and repairs the data folder and the firewall rule through
an elevated setup step; and every user-visible string in the window is resolved from the `desktop.*`
resx table. This task is test-first: every ViewModel behaviour is proven red-then-green before the
implementation exists. Definition of done: `dotnet test` for `GastronomyApp.Desktop.Tests` passes,
`dotnet build` for the desktop project tree is warning-free, and the manual verification list in
section 6 is run by the owner on a real Windows machine.

## 2. Assumed from earlier tasks

T005 (the `GastronomyApp.Api` composition root) is not read by this document; its exact shape is
assumed as follows, and the consistency review reconciles any mismatch:

- `GastronomyApp.Api` exposes a static composition entry point:

  ```csharp
  namespace GastronomyApp.Api;

  public static class ApiHost
  {
      public static WebApplication Build(ApiHostOptions options);
  }

  public sealed record ApiHostOptions(
      string DataDirectory,
      int Port,
      string BindAddress);
  ```

  `Build` performs every step section 10.1's table assigns to the Api library (REST endpoints, SignalR
  hub, static frontend, EF Core/SQLite wiring against `DataDirectory`, composition root) and returns an
  unstarted `WebApplication`. It does not call `Run`, `Start`, or `RunAsync`; the desktop host owns the
  application's lifetime by calling `StartAsync`/`StopAsync` on the returned instance. `ApiHostOptions`
  carries exactly the three settings section 10.1's table says cannot live in a web page (port, bind
  address, data folder), since "which network to display" is a desktop-only display concern that never
  reaches the Api and does not affect what the server binds to.

- Session-active query used to gate the data folder field (section 10.1, "disabled while a session is
  active"). Assumed as a method on the same `WebApplication` built by `ApiHost.Build`, resolved from its
  `Services`, rather than a raw HTTP call back into the server the desktop itself is hosting:

  ```csharp
  namespace GastronomyApp.Core.Sessions;

  public interface ISessionStateQuery
  {
      Task<bool> IsSessionActiveAsync(CancellationToken cancellationToken = default);
  }
  ```

  The desktop resolves `app.Services.GetRequiredService<ISessionStateQuery>()` once the `WebApplication`
  is running, and calls it when the settings window opens and before it enables the data folder field.
  If `ISessionStateQuery` turns out not to exist under that name in T005, the consistency review renames
  the desktop's call site; the shape (an async boolean query resolved from the hosted application's
  `IServiceProvider`) is the part this task depends on and asks T005 to confirm.

- Port-in-use, folder-not-writable and no-network detection happen on the desktop side, before or
  around the `StartAsync` call, not as exceptions defined by T005. `StartAsync` is assumed to throw
  `IOException` (or a subclass, e.g. `System.Net.Sockets.SocketException` wrapped by Kestrel) when the
  configured port is already bound; this task's `IHostLauncher` port (section 4) catches that and maps
  it to `desktop.error.portInUse` rather than assuming a typed exception from the Api library.

## 3. Repository layout after completion

```
desktop/
  GastronomyApp.Desktop/
    GastronomyApp.Desktop.csproj          (edited: resx generator, SkiaSharp/QR package reference)
    App.axaml / App.axaml.cs               (edited: TrayIcon, single-instance bootstrap)
    Program.cs                             (edited: setup-argument branch, mutex/pipe bootstrap)
    app.manifest                            (edited: requestedExecutionLevel stays "asInvoker")
    Strings.de.resx
    Strings.en.resx
    Strings.Designer.cs                     (generated)
    Views/
      MainWindow.axaml / .axaml.cs
      SettingsWindow.axaml / .axaml.cs
      QuitConfirmDialog.axaml / .axaml.cs
      FirstRunDialog.axaml / .axaml.cs
    ViewModels/
      MainWindowViewModel.cs
      SettingsWindowViewModel.cs
      QuitConfirmViewModel.cs
      FirstRunViewModel.cs
      ViewModelBase.cs
    Services/
      HostLauncher.cs                       (IHostLauncher impl, owns ApiHost.Build + WebApplication lifetime)
      QrCodeGenerator.cs                     (IQrCodeGenerator impl)
      NetworkAddressProvider.cs              (INetworkAddressProvider impl)
      SingleInstanceCoordinator.cs           (ISingleInstance impl: mutex + named pipe)
      Windows/
        WindowsPowerManager.cs               (IPowerManager impl, SetThreadExecutionState)
        WindowsFirewallSetup.cs              (IFirewallSetup impl, netsh advfirewall)
        WindowsDataFolderSetup.cs            (IDataFolderSetup impl, FileSystemAccessRule grant)
        NoOpPowerManager.cs                  (non-Windows fallback)
      SettingsStore.cs                       (reads/writes settings.json, layers over appsettings.json)
  GastronomyApp.Desktop.Tests/
    GastronomyApp.Desktop.Tests.csproj      (edited: FakeItEasy, Avalonia.Headless.NUnit references)
    ViewModels/
      MainWindowViewModelTests.cs
      SettingsWindowViewModelTests.cs
      QuitConfirmViewModelTests.cs
      FirstRunViewModelTests.cs
    Smoke/
      MainWindowSmokeTests.cs                 (Avalonia.Headless window-loads test)
docs/
  tasks/
    T007-desktop.md                          (this file)
```

## 4. Ports (full signatures)

All ports live in `GastronomyApp.Desktop.Services` (not `GastronomyApp.Core`: they describe desktop
machine concerns, not domain concepts, and T005/Core do not need to know they exist). Windows
implementations are thin adapters over Win32/`netsh`/`System.Threading.Mutex`; they are excluded from
unit tests per `desktop\CLAUDE.md` and verified manually (section 6).

```csharp
namespace GastronomyApp.Desktop.Services;

public interface IHostLauncher
{
    Task<HostLaunchResult> StartAsync(ApiHostOptions options, CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    bool IsRunning { get; }
}

public abstract record HostLaunchResult
{
    public sealed record Started(WebApplication Application) : HostLaunchResult;
    public sealed record PortInUse(int Port) : HostLaunchResult;
    public sealed record DataFolderNotWritable(string Path) : HostLaunchResult;
    public sealed record NoNetworkAvailable : HostLaunchResult;
}

public interface IPowerManager
{
    void PreventSleep();
    void AllowSleep();
}

public interface IFirewallSetup
{
    bool IsRuleConfigured();
    void EnsureRuleConfigured();
}

public interface IDataFolderSetup
{
    string DataDirectoryPath { get; }
    bool Exists();
    bool CurrentUserCanWrite();
    void CreateWithUsersModifyGrant();
    void GrantUsersModifyOnExisting();
}

public interface ISingleInstance
{
    SingleInstanceOutcome AcquireOrSignalExisting();
    event Action? ActivationRequested;
    void Release();
}

public enum SingleInstanceOutcome
{
    AcquiredPrimary,
    SignaledExistingAndShouldExit,
}

public interface IQrCodeGenerator
{
    IReadOnlyList<bool[]> GenerateMatrix(string content);
}

public interface INetworkAddressProvider
{
    IReadOnlyList<NetworkAddressOption> GetAvailableAddresses();
}

public sealed record NetworkAddressOption(string InterfaceName, string IPAddress);

public interface ISettingsStore
{
    DesktopSettings Load();
    void Save(DesktopSettings settings);
}

public sealed record DesktopSettings(
    int Port,
    string BindAddress,
    string DataDirectory,
    string? SelectedNetworkInterface);
```

Elevated relaunch (the setup argument process, section 10.3) is exposed as its own port so
`FirstRunViewModel` can be tested without spawning a real process:

```csharp
public interface IElevatedSetupLauncher
{
    Task<ElevatedSetupOutcome> RunElevatedSetupAsync(CancellationToken cancellationToken = default);
}

public enum ElevatedSetupOutcome
{
    Completed,
    ElevationDeclined,
}
```

`WindowsElevatedSetupLauncher` relaunches `Process.GetCurrentProcess().MainModule.FileName` with
`--setup`, `UseShellExecute = true`, `Verb = "runas"`, and awaits exit; `Program.cs`'s `--setup` branch
calls `IFirewallSetup.EnsureRuleConfigured()` and `IDataFolderSetup.CreateWithUsersModifyGrant()` /
`GrantUsersModifyOnExisting()` (whichever applies), opens no window, takes no mutex, and exits.

## 5. Ordered steps

1. **Resx table.** Add `Strings.de.resx` / `Strings.en.resx` with every `desktop.*` key from spec
   10.1 verbatim (including the `{url}`, `{port}`, `{path}`, `{count}` placeholders and the two-key
   `desktop.phones.one` / `desktop.phones.many` split). No other resx pair.

2. **Port interfaces first.** Add every interface in section 4 to `GastronomyApp.Desktop/Services`
   with no implementation bodies beyond `throw new NotImplementedException()`, so ViewModels can be
   written against them immediately.

3. **`MainWindowViewModel` tests, red first.** In `GastronomyApp.Desktop.Tests/ViewModels`, write
   NUnit + FakeItEasy tests against fakes of `IHostLauncher`, `IPowerManager`, `IQrCodeGenerator`,
   `INetworkAddressProvider`, `ISessionStateQuery`-backed status, covering:
   - Start transitions `Status` from `Stopped` to `Running`, calls `IPowerManager.PreventSleep()`.
   - `HostLaunchResult.PortInUse` sets `ErrorMessageKey = "desktop.error.portInUse"` with the port
     formatted in, and `Status` stays `Stopped`.
   - `HostLaunchResult.DataFolderNotWritable` sets `desktop.error.dataFolderRepair`.
   - `HostLaunchResult.NoNetworkAvailable` sets `desktop.error.noNetwork`.
   - Stop transitions `Running` to `Stopped`, calls `IPowerManager.AllowSleep()`.
   - `AttentionState` exposes exactly one of `None` / `Some` (never both, never neither) mapped to
     `desktop.attention.none` / `desktop.attention.some`.
   - Phone count zero maps to `desktop.phones.none`; one to `desktop.phones.one`; N>1 to
     `desktop.phones.many` with `{count}` bound.
   - QR content is the address as returned by `INetworkAddressProvider`/settings, unchanged by the
     ViewModel (matrix generation is delegated, never recomputed).

   Run the suite, paste the red output (every test fails or does not compile because
   `MainWindowViewModel` does not exist yet), then implement `MainWindowViewModel` to green.

4. **`QuitConfirmViewModel` tests, red first.** `RequestQuit()` opens the confirm state; `Cancel()`
   returns to running with no side effect; `Confirm()` calls `IHostLauncher.StopAsync` then signals the
   application to exit. Assert the dialog never closes the app without `Confirm()` having been called
   (guards spec 10.1's "quit only through the quit button, which asks for confirmation first").

5. **`SettingsWindowViewModel` tests, red first**, covering:
   - Port/bind-address fields are editable when `ISessionStateQuery.IsSessionActiveAsync()` is false
     and no phone is enrolled; both refused (bound to `IsEnabled = false`,
     `desktop.settings.addressLocked` shown) once a session has accepted an order, per spec 10.1's "How
     costly, and when refused" subsection. Model this as a constructor-supplied
     `bool anyOrderAcceptedThisSession` flag until T005 exposes the concrete query; wire the real query
     in step 9.
   - Data folder field `IsEnabled` is false whenever `ISessionStateQuery.IsSessionActiveAsync()`
     returns true, with `desktop.settings.dataFolderLocked` shown; true otherwise. A changed value never
     moves files and shows `desktop.settings.dataFolderRestart` (no move, restart required).
   - `RepairSetup()` calls `IElevatedSetupLauncher.RunElevatedSetupAsync()`; `ElevationDeclined` shows
     `desktop.settings.repairDeclined` and opens Windows firewall settings via a delegated
     `Action openFirewallSettings` (a plain `Action`, not a new port: it is a single `Process.Start`
     call with no state to fake beyond "was it invoked", so the test asserts against a substituted
     delegate rather than adding another interface for one call).
   - Network selector lists `INetworkAddressProvider.GetAvailableAddresses()`, hidden entirely (per
     spec: "only relevant when the laptop is on more than one network") when the list has zero or one
     entries.

6. **`FirstRunViewModel` tests, red first, the detection matrix.** Given fakes of `IFirewallSetup` and
   `IDataFolderSetup`, cover the four cells: both configured (no dialog shown), only firewall missing,
   only data folder missing/unwritable, both missing. Each missing case shows
   `desktop.firstRun.title`/`desktop.firstRun.body` and offers `IElevatedSetupLauncher`; declining
   shows `desktop.firstRun.declined` and the ViewModel still reports `ReadyToStart = true` (spec 10.3:
   "the program starts normally" on decline).

7. **`ISingleInstance` behaviour test.** Fake the port directly (no real mutex/pipe in this test): given
   `AcquireOrSignalExisting()` returns `SignaledExistingAndShouldExit`, `Program.cs`'s composition logic
   (tested via a small `AppBootstrapper` class extracted for testability) must not construct
   `MainWindowViewModel` or start `IHostLauncher`, and must exit before any window is shown. Given
   `AcquiredPrimary`, bootstrapping proceeds normally and `ActivationRequested` (raised when a second
   launch signals over the pipe) brings the existing window to front, never opens a second window.

8. **Avalonia.Headless smoke test.** One test per window (`MainWindow`, `SettingsWindow`) that boots
   the headless Avalonia app, sets the `DataContext` to a ViewModel built from fakes, and asserts the
   window loads without exception and its title text resolves through the resx table (not a hardcoded
   string). This is presence-and-wiring coverage only; it does not replace the ViewModel tests above.

9. **Wire the real ports.** Implement `HostLauncher` (calls `ApiHost.Build`, then `StartAsync`/
   `StopAsync` on the returned `WebApplication`, catching bind/write failures per section 2's
   assumption and mapping them to `HostLaunchResult` cases), `QrCodeGenerator` (see constraint below on
   which encoder), `NetworkAddressProvider` (`NetworkInterface.GetAllNetworkInterfaces()`), and the
   Windows implementations of `IPowerManager`, `IFirewallSetup`, `IDataFolderSetup`,
   `IElevatedSetupLauncher`, `ISingleInstance`. Replace the constructor flags used as stand-ins in steps
   3 and 5 with the real `ISessionStateQuery` resolved from the running `WebApplication.Services`, per
   the assumption in section 2. Add `NoOpPowerManager` for non-Windows and select it at startup via
   `OperatingSystem.IsWindows()`.

10. **Build the views.** `MainWindow.axaml`/`SettingsWindow.axaml` bind to the ViewModels with compiled
    bindings (`x:DataType` on every `DataTemplate`); QR matrix renders as a `Canvas` of `Rectangle`
    cells built in code-behind from `IQrCodeGenerator.GenerateMatrix`, not as an `ItemsSource`-bound
    `ItemsControl` grid (keeps rendering fast for a fixed-size matrix redrawn once per address change).
    `TrayIcon` is declared in `App.axaml` with two menu items only: "Open the admin pages" and "Quit",
    both routed to the same commands `MainWindowViewModel` exposes; no independent tray logic. The quit
    confirmation dialog and first-run dialog are built as separate `Window`s shown via
    `ShowDialog<T>`, never as XAML-declared `Flyout` content (desktop CLAUDE.md gotcha).

11. **`Program.cs` bootstrap.** Parse `args` for `--setup` first, before touching Avalonia, and branch:
    `--setup` runs the elevated-step logic from section 4 and returns without building the Avalonia
    app; otherwise call `ISingleInstance.AcquireOrSignalExisting()` before `BuildAvaloniaApp().Start(...)`,
    exiting immediately on `SignaledExistingAndShouldExit`.

## 6. Constraints

- MVVM per `desktop\CLAUDE.md` rule 8: every one of the behaviours in steps 3 to 7 lives in a
  ViewModel behind a port; no static state beyond `AvaloniaProperty.Register` and framework metadata.
- Compiled bindings on: `AvaloniaUseCompiledBindingsByDefault=true` already set by T001; every
  `DataTemplate` added here carries `x:DataType`.
- No `Button.Flyout` content declared in XAML; build flyout/dialog content in code-behind
  (`desktop\CLAUDE.md` gotcha: XAML flyout content renders but does not receive real input).
- Any `ObservableCollection` (phone list on the network selector, tray menu items) is mutated in
  place (`Clear()` + `Add()`), never replaced with a new instance.
- Every `IsVisible` binding on a nullable sub-path (attention link target, error banner) carries
  `FallbackValue=False`.
- No string literals in `.axaml` or code-behind beyond what compiled bindings require. The product
  name/window title exception T001 recorded ends with this task: `desktop.windowTitle` replaces it.
  Every other user-visible string (button labels, error text, dialog titles/bodies, tooltips) is a
  resx-bound resource, in German and English, added in step 1.
- QR encoder: per spec 10.1 ("A QR encoder is a version 1 requirement, unconditionally... The window
  and the card use the one encoder, in one place"), this task builds the one client-side QR matrix
  generator both the window and the future station-card renderer (section 5.5, a later task) will
  share. The spec does not name a library; it only fixes that there is exactly one encoder shared
  everywhere it is needed, and that open question 11 governs only how the symbol later reaches a
  printed slip (firmware `GS ( k` vs. a raster of this encoder's output), not whether the encoder
  exists. This task places the encoder behind `IQrCodeGenerator` precisely so that choice (a
  hand-rolled ISO/IEC 18004 encoder vs. a small vendored library) is swappable without touching any
  caller; `QrCodeGenerator` picks a small dependency-free managed QR encoder (no native interop, so it
  runs identically headless in tests) and returns a boolean matrix rather than a rendered image, so
  both the Avalonia `Canvas` renderer here and a future raster-to-ESC/POS renderer consume the same
  shape.
- `appsettings.json` beside the executable is read-only shipped defaults (scheme, port, bind address,
  log level per spec 10.7); `ISettingsStore`/`SettingsStore` layers `settings.json` from the data
  folder over it and never writes back to `appsettings.json`.
- Data folder change is refused while a session is active per the assumption in section 2; the
  ViewModel never bypasses that by writing directly to `DesktopSettings.DataDirectory` while the field
  is disabled.
- `HostLauncher` never calls `WebApplication.Run`/`RunAsync`; it owns start/stop explicitly so the
  quit flow can await a clean `StopAsync` before the process exits.
- Zero build warnings across `GastronomyApp.Desktop` and `GastronomyApp.Desktop.Tests`.

## 7. Verification

- `dotnet test GastronomyApp.slnx --filter FullyQualifiedName~GastronomyApp.Desktop.Tests` green,
  output quoted, covering every ViewModel test in steps 3 to 8.
- `dotnet build GastronomyApp.slnx` zero warnings.
- Each ViewModel test file's red-then-green pair is quoted in the change: the failing run before the
  implementation existed, and the passing run after.

**Manual verification list (Windows, run by the owner after this task lands; not part of automated
CI, listed here per `desktop\CLAUDE.md`'s "Windows-API implementations are excluded from unit tests
and verified manually"):**

1. First run on a clean machine: one UAC prompt appears, data folder is created under
   `%ProgramData%\GastronomyApp`, `Users` group has Modify rights on it (check folder Properties >
   Security), a firewall rule for the executable exists afterward (Windows Defender Firewall with
   Advanced Security > Inbound Rules).
2. Declining the UAC prompt: window still opens, `desktop.firstRun.declined` text is shown, program
   runs normally with server started.
3. Second launch while the first is running: no second window appears, the first window comes to the
   front, second process exits promptly.
4. Close button (X) minimises the window; server keeps answering requests from a phone on the same
   WiFi while minimised.
5. Quit button shows the confirmation dialog; "Keep it running" cancels with no state change; "Quit"
   stops the server (verify a phone request fails afterward) and exits the process.
6. Laptop does not sleep (via power settings / `powercfg /requests`) while the server is running, and
   sleeps normally again a short time after quitting.
7. Start with the configured port already occupied by another process: window shows
   `desktop.error.portInUse` with the port number, no crash.
8. Point the data folder at a location the current user cannot write to (or revoke Modify rights
   manually): window shows `desktop.error.dataFolderRepair`, "Repair the setup" from Settings restores
   write access.
9. Disconnect all network adapters: window shows `desktop.error.noNetwork`.
10. QR code on the window, scanned with a phone on the same WiFi, opens the site at the address shown
    in large type.
11. Settings: port/bind address fields become read-only (with `desktop.settings.addressLocked`) once an
    order has been placed in the current session, editable again after restarting into a fresh session;
    data folder field is disabled while a session is active with `desktop.settings.dataFolderLocked`
    shown, editable when no session is active.
12. Tray icon: minimizing to tray, right-click shows "Open the admin pages" and "Quit" only; both work.

## 8. Out of scope

- Auto-start with Windows.
- Installer or packaging, single-file publish tuning.
- Non-Windows implementations of `IPowerManager`, `IFirewallSetup`, `IDataFolderSetup`,
  `IElevatedSetupLauncher` beyond the `NoOpPowerManager` no-op stub and equivalent guards that skip
  Windows-only steps on other platforms.
- mDNS or any address-discovery mechanism beyond the QR code and manual entry.
- The station-card QR rendering (`GET /api/admin/locations/{id}/station-card`, spec 5.5) and open
  question 11's printer-side decision: this task only builds the shared encoder behind
  `IQrCodeGenerator` that the future card renderer will also depend on.
- Admin page content of any kind: the window opens a browser to the admin URL and nothing more.

## 9. Assumptions

Restated from section 2, plus the smaller ones made while writing the steps above:

1. `GastronomyApp.Api` exposes `ApiHost.Build(ApiHostOptions)` returning an unstarted
   `WebApplication`, with `ApiHostOptions(string DataDirectory, int Port, string BindAddress)`; the
   desktop owns `StartAsync`/`StopAsync`.
2. Session-active state is queryable as `ISessionStateQuery.IsSessionActiveAsync()`, resolved from the
   running `WebApplication.Services`, and this is also what the settings window asks before enabling
   the data folder field and before deciding whether to lock the port/bind-address fields.
3. `StartAsync` surfaces a port-already-bound failure in a way `IHostLauncher` can distinguish (an
   `IOException`/`SocketException`) rather than the Api library defining its own typed result for this;
   `HostLauncher` is the seam that turns whatever T005 throws into `HostLaunchResult`.
4. The QR encoder is a small managed, dependency-free library choice made inside this task (behind
   `IQrCodeGenerator`) because the spec fixes that exactly one encoder exists and is shared, but leaves
   the concrete library/algorithm open.
5. `Program.cs`'s composition logic is extracted into a small `AppBootstrapper` class so single-instance
   short-circuiting is unit-testable without booting real Avalonia.
6. "Which network to display" (spec 10.1's settings table) is a desktop-local display preference
   (`DesktopSettings.SelectedNetworkInterface`), not part of `ApiHostOptions`, since it affects only
   what address the window and QR code show, not what the server binds to (`BindAddress` already
   covers the bind side, typically `0.0.0.0` or a specific interface IP entered separately).

## 10. Ambiguities and chosen readings

- Spec 10.1 lists the settings-window fields as four ("Port", "Bind address", "Data folder", "Which
  network's address is shown") without giving the last one a dedicated resx key beyond
  `desktop.settings.network`/`desktop.settings.networkHelp`, which already exist in the table; no new
  strings were invented for it.
- The spec does not name the mutex/pipe identifiers. Read as: pick a fixed, product-scoped name (e.g.
  `Global\GastronomyApp.Desktop.SingleInstance`) at implementation time; not specified further here
  since it carries no behavioural ambiguity, only a literal string choice.
- "Repair the setup" (button) and first-run dialog run the same elevated step (spec 10.3: "It runs the
  same elevated step, with the same two idempotent actions"); read as one shared
  `IElevatedSetupLauncher.RunElevatedSetupAsync()` call site used from both `FirstRunViewModel` and
  `SettingsWindowViewModel`, not two separate ports.
- Tray icon behaviour beyond "minimal version the spec describes": spec 10.1 does not mandate a tray
  icon explicitly, only that `desktop\CLAUDE.md` notes `TrayIcon` exists natively. Read as: include it
  showing running/stopped via icon or tooltip text and the two menu actions listed in step 10, since a
  window that can only be restored from the taskbar (no tray habit) is a reasonable but strictly
  smaller reading; the taskbar-only reading remains acceptable if the consistency review prefers
  dropping the tray icon entirely, since the spec text does not require one.
