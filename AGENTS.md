# AGENTS.md

## Cursor Cloud specific instructions

### Project Overview

ICN_T2 is a .NET 8.0 WPF/WinForms Windows desktop application (Yokai Watch 2 game modding tool). It targets `net8.0-windows10.0.22621.0`. See `README.md` for feature details.

### Cross-platform Build (Linux)

This project targets Windows, but can be **restored and compiled on Linux** using the `EnableWindowsTargeting` MSBuild property:

- **Restore:** `dotnet restore ICN_T2.sln -p:EnableWindowsTargeting=true`
- **Build:** `dotnet build ICN_T2.sln -p:EnableWindowsTargeting=true`
- **Lint/Format check:** `EnableWindowsTargeting=true dotnet format ICN_T2.sln --verify-no-changes`
- **Auto-fix format:** `EnableWindowsTargeting=true dotnet format ICN_T2.sln`

The `EnableWindowsTargeting=true` flag is **required** for all dotnet commands on Linux; without it, restore/build will fail with `NETSDK1100`.

### Running the Application

The application is a WPF GUI app and **cannot be launched on Linux** — it requires Windows 10+ with .NET 8.0 Desktop Runtime. On Windows: `dotnet run --project ICN_T2/ICN_T2.csproj`.

### Tests

There are no automated test projects in this repository. Validation relies on build compilation and `dotnet format` checks.

### Key Build Notes

- The build produces `ICN_T2.dll` at `ICN_T2/bin/Debug/net8.0-windows10.0.22621.0/`.
- NU1701 warning about `WPF.UI 3.4.2.7` package compatibility is expected and harmless.
- ~335 nullable reference type warnings (CS8600/CS8618/CS8602/etc.) exist in the codebase — these are pre-existing and not build errors.
- The .NET 8.0 SDK is installed at `$HOME/.dotnet` with PATH configured in `~/.bashrc`.
