# CLAUDE.md (desktop)

Rules for the Avalonia desktop application. The repository root `CLAUDE.md` binds here too; this file
adds to it and never contradicts it. What the window shows and never shows, how the port is chosen
and how the program updates itself are in the `features` page `desktop-window.md`.

## Hard rules

1. **Clicking the close button never stops the server.** It minimises. Quitting happens only through
   an explicit quit action that asks for confirmation first. This rule is the reason the desktop
   application exists.
2. **Single instance.** A second launch brings the existing window to the front and exits. Two
   instances would mean two servers and phones talking to whichever won the port.
3. **The application keeps the laptop awake while the server runs** and releases that when stopped.
   On Windows this is `SetThreadExecutionState` (or the power request API); on other platforms it is
   a no-op with a logged note, never a crash.
4. **It runs as a normal user.** Never require elevation to start. The two tasks that need elevation
   (the inbound firewall rule, making the data folder writable for all users) happen once, in a
   single elevated first-run step. Declined elevation degrades to instructions, never to a broken
   state.
5. **Data lives in `%ProgramData%\GastronomyApp\`**, and the application explicitly grants Users
   modify rights on the folder it creates there. Without that grant the volunteer who did not set the
   system up can read the database and not write to it, which fails orders for a reason nobody can
   guess.
6. **Errors appear on the window in plain language**, in German and English: port in use, data folder
   not writable, no network found. A log line is not a substitute for telling the operator.
7. **Everything is logged to a rolling file** in `%ProgramData%\GastronomyApp\logs\`, written by
   Serilog and shared by the window and the server it hosts. Device tokens are credentials and never
   appear in it. Ports, addresses and folder paths do, because they are what a phone call is about.

8. **Localization:** every user-visible string in the window exists in German and English, resolved
   through the same resx mechanism as the backend (`Strings.de.resx` / `Strings.en.resx` or a
   desktop-specific pair). No string literals in axaml or code-behind.
9. **MVVM.** ViewModels hold the state and commands; code-behind only where Avalonia forces it (see
   gotchas). No static methods or properties beyond `AvaloniaProperty.Register` and framework
   metadata.

10. **The window carries the same colour meanings as the web app.** The palette lives in
    `Themes/Colors.axaml` and the accent in `App.axaml`, never as a raw hex in a view: neutral
    `Primary` for the action that moves work forward, red `Danger` for anything destructive, green
    `StatusRunning` for a healthy server, red `StatusDown` for a failure, amber `StatusWarning` for a
    warning, grey `StatusStarting` while the answer is still out. Green, amber and red are status
    only, never a button. When the palette changes it changes here and in `frontend/src/theme.ts`
    together, because `frontend/CLAUDE.md` rule 6 is the same list.

## Avalonia gotchas (hard-won, do not relearn them)

- **Dynamic `MenuItem` submenus:** build them in code-behind (`CollectionChanged` handler producing a
  hand-built `List<MenuItem>`). XAML `ItemsSource` binding does not render submenus reliably.
- **`Button.Flyout` content declared in XAML never receives input:** the popup renders and bindings
  resolve (headless tests pass!), but real clicks die with `(PresentationSource) PlatformImpl is
  null, couldn't handle input`. Build flyout content in code-behind (`new Flyout()` plus content
  controls).
- **`IsVisible` on a null sub-path evaluates `true`** when the object is null. Always add
  `FallbackValue=False`.
- **Never replace an `ObservableCollection` instance.** Mutate in place (`Clear()` then `Add()`).
  Menus and flyouts re-bind unreliably to a replaced collection.
- **Compiled bindings:** set `AvaloniaUseCompiledBindingsByDefault=true`; every `DataTemplate` then
  needs `x:DataType`.
- **`TrayIcon` exists in Avalonia natively** (declared in `App.axaml` or code). No extra package.

## Testing

NUnit + FakeItEasy, matching the backend. ViewModel logic is tested headlessly with
`Avalonia.Headless`; anything that talks to Windows (power state, ACLs, firewall) goes behind a port
interface so ViewModel tests never touch the OS. The elevated first-run step is verified manually on
a real machine and its logic (deciding what is missing) is unit tested behind the same ports.
