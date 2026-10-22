# Format the C# files you touched

Backend rule 10 requires every C# file an agent created or edited to go through the ReSharper
cleanup in the same turn. Nobody formats by hand.

## When the `resharper` MCP server is connected

Run `mcp__resharper__format_file` in `cleanup` mode on each file.

## Otherwise: JetBrains' command-line tools

They are pinned in `.config/dotnet-tools.json` (`jetbrains.resharper.globaltools`), so
`dotnet tool restore` installs them on a new machine. Run from the repository root, with every file
in one call:

```powershell
dotnet jb cleanupcode --profile="Built-in: Reformat Code" --no-build --no-updates --include="<path1>;<path2>" GastronomyApp.slnx
```

- Paths are relative to the repository root and separated by `;`.
- `--no-build` is required. Without it the tool builds the whole solution, desktop included, which
  fails while the desktop app is running.
- The profile only reformats; it never rewrites code.
- Target the solution file. A project file loads an empty project and the tool reports that it
  found nothing to clean up.
- One run takes about 16 seconds, so pass all files at once.

## Where the settings live

The command-line tool reads only `.editorconfig`, never Rider's own settings. A formatting setting
that exists only in an IDE makes the two disagree, so every formatting setting, including the
`resharper_*` keys, lives in `.editorconfig`.
