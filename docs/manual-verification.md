# Manual verification on a real Windows machine

The desktop application's Windows integrations sit behind tested port interfaces, but the operating
system half of each one can only be proven by hand. Work through this list once on a real machine
before the first demonstration. Every item names what success looks like.

## First run and setup

1. **Clean first run.** Launch the executable on a machine that has never run it. Exactly one UAC
   prompt appears. Afterwards `%ProgramData%\GastronomyApp` exists and its Properties > Security shows
   `Users` with Modify rights, and Windows Defender Firewall > Inbound Rules contains a rule named for
   the program: TCP, private profile, local subnet.
2. **Setup run twice.** Run the elevated setup again on a machine that already has the rule. The rule
   is updated in place and is never absent between commands.
3. **Decline the UAC prompt.** The window still opens, shows the declined-setup text, and the server
   still starts.
4. **Decline by closing the dialog.** Close the one-time setup dialog with its X instead of the
   button. The declined text appears on the main window, not only in the dialog, and the server still
   starts.

## Single instance and lifecycle

5. **Second launch while the first runs.** No second window appears, the first window comes to the
   front and takes focus, and the second process exits within about two seconds.
6. **Second launch during startup.** Launch a second instance immediately after the first, before its
   window has painted. The second still exits quietly, with no crash dialog, even if it could not
   signal the first.
7. **Close button.** Clicking the X minimises. A phone on the same WiFi keeps getting answers while
   the window is minimised.
8. **Tray icon.** Right-click shows exactly two items, open admin and quit, and both work.
   Left-clicking the icon restores the window.
9. **Quit.** The quit button shows the confirmation. Keeping it running changes nothing. Confirming
   stops the server (a phone request now fails) and exits the process. Afterwards `powercfg /requests`
   shows no remaining SYSTEM request for the executable.

## Power and network

10. **Sleep suppression.** With the server running, `powercfg /requests` lists a SYSTEM request for
    the executable. After quitting, the request is gone and the laptop sleeps normally.
11. **No network.** Disable every network adapter and start. The window shows the no-network error.
12. **Several networks.** Connect to two networks. The network selector appears in the settings and
    choosing an entry changes the address and the QR code on the window; with one network the
    selector stays hidden.
13. **QR code.** Scan the code on the window with a phone on the same WiFi. The site opens at the
    address shown in large type.

## Failure states

14. **Port in use.** Occupy the configured port with another program, then start. The window names
    the port in the error and does not crash.
15. **Unwritable data folder.** Revoke your own Modify rights on the data folder. The window shows the
    repair text; the repair action in the settings restores write access after one UAC prompt.
    Declining that prompt shows the declined text and opens the Windows firewall settings page.
16. **Wrong folder named.** Point the data folder setting at a path the current user cannot write to
    and restart. The repair text names that path, not `%ProgramData%\GastronomyApp`.
17. **Generic start failure.** Make the server fail to start for a reason that is neither the port
    nor folder permissions (a corrupt `gastronomy.db` is the easiest to stage). The window shows the
    could-not-be-started text, names no folder path, and the process stays alive with no crash
    dialog.

## Settings locks

18. **Locked during a session.** With an active event session, the data folder, port and bind address
    fields are disabled, each with its explaining text. With no session active they are editable
    again. Changing the data folder shows the restart note and moves no files.

## Hardware, once printers exist

These stay open until a real TM-T20IV is on a desk, per the spec's open questions: the `GS ( H`
process id echo on real firmware, the `GS ( k` QR command support, and whether five minutes is the
right give-up window against a real evening.
