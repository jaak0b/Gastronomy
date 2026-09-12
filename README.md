# GastronomyApp

[![Build & test](https://img.shields.io/github/actions/workflow/status/jaak0b/Gastronomy/build.yml?label=build%20%26%20test)](https://github.com/jaak0b/Gastronomy/actions/workflows/build.yml)
[![Latest release](https://img.shields.io/github/v/release/jaak0b/Gastronomy?label=release&sort=semver)](https://github.com/jaak0b/Gastronomy/releases/latest)
[![License: MIT](https://img.shields.io/github/license/jaak0b/Gastronomy)](LICENSE)
[![Built with Avalonia / .NET](https://img.shields.io/badge/built%20with-Avalonia%20%2F%20.NET-512BD4)](https://avaloniaui.net)

A self-hosted ordering system for volunteer fire department festivals. It replaces the paper order slip.

A waiter takes the order at the table on their own phone: items, quantities, table name, and the running total as an adding aid. The laptop in the tent runs the whole system, including the database and the web app. Each production location has a tablet that shows its part of the order, where staff mark items as waiting, being prepared or ready. A ready item shows the table name, so whoever passes the station takes it out. Open tables can be settled later from the phone, with the amount the guest actually paid.

The site runs on the local WiFi without internet. Phones and tablets enrol by scanning a QR code from the laptop, so there is nothing to install on them and no usernames or passwords. No money moves through the app: it shows prices to help add up and records what a table has paid.

## Installer

Windows installers are published on the [releases page](https://github.com/jaak0b/Gastronomy/releases). Download `GastronomyApp-win-Setup.exe` and run it. The installed program checks GitHub for a new version, downloads it in the background, and installs it when the program quits, so a running festival is never interrupted.

## Build and test

You need the .NET 10 SDK and Node.js.

```powershell
npm install --prefix frontend
npm run build --prefix frontend
dotnet build GastronomyApp.slnx
dotnet run --project desktop/GastronomyApp.Desktop
```

Run everything with `pwsh ./build.ps1 --target Test`, or the two suites separately:

```powershell
dotnet test GastronomyApp.slnx
npm test --prefix frontend -- --run
```

Build the installer with `pwsh ./build.ps1 --target Pack`; the output lands in `artifacts/velopack`. Publishing a release is `pwsh ./build.ps1 --target Release` with a `GH_TOKEN` set.

## Layout

| Folder | Contents |
|---|---|
| `backend/` | .NET 10 class libraries: domain, EF Core SQLite, REST and SignalR API |
| `desktop/` | The only executable: an Avalonia window that hosts the API in-process |
| `frontend/` | Vue 3 and TypeScript app for phones, station tablets and the admin screens |

## License

Released under the [MIT License](LICENSE).
