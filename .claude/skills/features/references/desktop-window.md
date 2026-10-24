# Desktop window

`GastronomyApp.Desktop` is the only executable of the product. It hosts the web application in its own
process and puts one small Avalonia window in front of it, so an operator with little technical
ability never sees a console, never edits a configuration file, and can never end the evening by
accident. The operator opens the admin page from the window, reads the status bar to see whether the
server is healthy, and quits through a confirmation. The window is a launcher and never a second
admin UI.

## Where the code lives

Everything is under `desktop/GastronomyApp.Desktop/`:

- `App.axaml` and `App.axaml.cs` start the program: the single instance check, the tray icon (its menu
  opens the admin page or asks to quit), `ShutdownMode.OnExplicitShutdown`, the first run dialog and
  the quit confirmation.
- `Hosting/AppBootstrapper.cs` (a second launch brings the first window to the front and exits),
  `Hosting/HostLauncher.cs` (builds and starts `GastronomyAppApiApplication` in this process and
  answers a `HostLaunchResult`: started, port in use, data folder not writable, no network, start
  failed) and `Hosting/DesktopComposition.cs`.
- `ViewModels/MainWindowViewModel.cs` holds the state: the port, `CurrentStatus` (starting, running,
  warning, down) and `AdminUrl`. `QuitConfirmViewModel.cs` holds the quit.
- `Views/MainWindow.axaml` and its code behind, and the dialogs `QuitConfirmDialog`, `FirstRunDialog`,
  `FailureDetailDialog` and `UpdateConfirmDialog` on `DialogTitleBar`.
- `Themes/Colors.axaml` (the palette) and `Themes/Skin.axaml` (geometry and control styles). `App.axaml`
  merges the first and includes the second after `FluentTheme`.
- Texts: `Localization/Strings.de.resx`, `Strings.en.resx` and `Strings.resx` through
  `DesktopTextProvider`. Also here: `Updates/` (the installer; `UpdateInstallGate` asks
  `FestivalSchedule` whether an update may be installed), `Platform/Windows/`, `Setup/` and `Settings/`.

## Invariants

- Closing the window never stops the server. `MainWindow.OnClosingMinimisesInstead` cancels a close
  the user made and minimises, and the tray icon brings the window back. Only the quit button (or the
  tray menu entry) ends the program: it opens `QuitConfirmDialog`, and `QuitConfirmViewModel.ConfirmAsync`
  stops the server and then asks the application to exit. Cancelling leaves the server running, and a
  stop that fails keeps the program open and says so in the status bar.
- The window shows the title bar (title, version, language picker, and the update icon only in an
  installed copy), the buttons for the admin page, the data folder, the repair of the setup and quit,
  and the status bar. It shows no address, no QR code and no counts. The admin button opens
  `http://localhost:{port}/admin` on loopback (`MainWindowViewModel.AdminUrl`).
- The status bar is the status light. It is a strip at the bottom filled with the colour of
  `CurrentStatus`: grey while the server has not answered, green when it runs, amber for a warning and
  red when something is down. It carries text only when there is a notice or an error to show, and a
  button for the technical detail of a failed start. `StatusLevelConverter` turns the status into the
  classes `running`, `warning` and `down` that `MainWindow.axaml` styles.
- The admin interface is the web page. The window never grows a second admin UI: no item editing, no
  station management, no order views, no address, no QR code, no counts and no status readouts. The
  web admin has to exist for the phones anyway, and two admin surfaces would both have to be kept
  true.
- There is no settings window, because nothing is left to configure: the port is chosen
  automatically, the server answers on every network interface, and the data folder is fixed.
  Opening the data folder and the elevated "Repair the setup" are buttons on the main window.

## The port

The port is never typed by a human. On first start the program asks Windows for a free port and
writes it down. Every later start tries the port it wrote down; if that one is taken it asks for
another, writes that down, and tells the operator that everybody has to set their phone up again,
which is the truth because a phone's device token is scoped to the origin and a new port is a new
origin. It retries only on "address already in use", at most ten times; any other bind failure stops
at once and is shown.

## Updates

The program updates itself from the public GitHub releases of its repository through Velopack. It
never reads prereleases and needs no access token.

On every start it checks at most once an hour. The moment of the last attempt is written down, so a
failed check counts and the next start waits an hour before trying again. A found update is
downloaded in the background at once.

Installation happens on quit, inside the confirmed quit flow: the downloaded update is applied when
no festival is running and none starts within the next 24 hours, hidden festivals included. If a
festival is near, the download stays on disk and a later quit can still install it. A quit while the
server is not running postpones as well.

The download icon in the title bar of an installed copy checks and downloads on demand, ignoring the
hourly limit and the festival window, and asks before installing. "Update now" installs at once and
restarts the program. "Not now" closes the question and changes nothing, and a downloaded update
still installs on quit when the gate allows it. The program never restarts on its own.

## Styling

- The palette schema and the control styles were ported from the owner's other application,
  Collectary, so a token has the same name in both.
- `Themes/Colors.axaml` holds a `Color` token and a matching brush for every entry: `PrimaryColor` and
  `PrimaryBrush`, `DangerColor` and `DangerBrush`, `StatusRunningColor` and `StatusRunningBrush`, and
  so on. The tokens are the surfaces (`Background`, `Surface`, `SurfaceAlt`), `Primary` with `Hover`,
  `Pressed` and `Foreground`, the text colours (`TextPrimary`, `TextSecondary`, `TextDisabled`), the
  lines (`Border`, `BorderStrong`, `FocusRing`, `ControlHover`), `Danger` with the same three
  variants, and `StatusRunning`, `StatusWarning`, `StatusDown`, `StatusStarting` and `StatusForeground`.
- `Themes/Skin.axaml` holds the geometry tokens (`ControlCornerRadius`, `ControlBorderThickness`,
  `ControlPadding`, `ControlHeight`, `ControlFontSize`) and the styles for `Window`, `TextBlock`,
  `Button`, `ComboBox` and `ComboBoxItem`, plus two button classes: `Button.accent` for the action that
  moves work forward (the admin button and the continue button of the first run dialog) and
  `Button.danger` for the quit confirmation.
- Colours are always set through palette tokens (`{DynamicResource <Name>Brush}`), never as a literal
  hex in a view. The hex values live in `Colors.axaml`, and `App.axaml` sets Fluent's
  `SystemAccentColor` family to the same value as `PrimaryColor`.
- The window carries the colour meanings of the web app. The accent is the neutral dark `Primary`
  (`#1F2937`, white text), not a red: red is `Danger` (`#C94F4F`), on the quit confirmation button and
  the DEBUG badge. The status bar has its own success green, `StatusRunning` (`#2E8B57`), and the
  status tokens are used nowhere else.
- The title bars (`TitleBar` in `MainWindow.axaml` and `DialogTitleBar.axaml`) have a border on the
  bottom edge only (`BorderThickness="0,0,0,1"`). The status bar has no border.
- Deliberately not ported are Collectary's alternate palettes and its runtime theme switching. The
  window has one light palette: `App.axaml` fixes `RequestedThemeVariant="Light"`, and no code switches
  it.
- Fluent paints a button's chrome on the template's content presenter, so every hover and pressed
  setter targets `/template/ ContentPresenter#PART_ContentPresenter`, for the plain button, for
  `.accent`, for `.danger` and for the update icon button. A `ComboBox` is the exception: its chrome is
  `/template/ Border#Background`.

## Tests

`desktop/GastronomyApp.Desktop.Tests` (NUnit, FakeItEasy, `Avalonia.Headless`):

- `Smoke/MainWindowSmokeTest`: the status bar in grey, green, amber and red with and without text, the
  buttons keeping their height under a long message, the label colours of the plain buttons and the
  accent button (white on `#1F2937`), the version, the title bar, the update icon and its spinner.
- `Smoke/QuitConfirmDialogSmokeTest` (the hover and pressed colours of the danger button, the outcome,
  Escape), `FirstRunDialogSmokeTest`, `FailureDetailDialogSmokeTest` and `UpdateConfirmDialogSmokeTest`.
  `TestSupport/RenderedColourReader.cs` reads the painted colour from `PART_ContentPresenter`.
- `ViewModels/MainWindowViewModelTest` (start, port, status levels, language, the admin address on
  loopback), `QuitConfirmViewModelTest` (nothing stops before the confirmation, a cancel leaves the
  server running, the server stops before the exit) and `FirstRunViewModelTest`.
- `Hosting/HostLauncherTest`, `Hosting/AppBootstrapperTest` and `AppTest`.
- Nothing tests that closing the window minimises it.
