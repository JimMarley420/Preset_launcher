# Development notes

The desktop application uses C#, .NET 8, WinUI 3 and Windows App SDK 1.8. It references CommunityToolkit.Mvvm and Newtonsoft.Json. The installer uses Inno Setup 6.

## Source layout

| Path | Responsibility |
| --- | --- |
| `PresetLauncherWinUI3/Models/` | Presets, executable entries, settings and launch history. |
| `PresetLauncherWinUI3/Services/` | Process launching, persistence, localization, themes, password verification and updates. |
| `PresetLauncherWinUI3/Views/Pages/` | Presets, history, statistics and settings. |
| `PresetLauncherWinUI3/Views/Dialogs/` | Preset editor. |
| `Installer/` | Publishing and installer packaging. |

Open `PresetLauncherWinUI3/PresetLauncherWinUI3.sln` in Visual Studio and select x64. Build and run the application locally. For packaging, follow the [root README](../README.md).

## Manual checks

Use test presets containing harmless programs, not your production data. For changes to launching, check a valid executable, a missing executable, a program that is already running, arguments and delays. For persistence, restart the app and confirm the expected data survives; test import/export with disposable JSON. For UI changes, check both languages, light and dark themes, and window resizing. For installer changes, test installation and uninstallation on a disposable Windows environment.

Record the Windows version, configuration and results in the pull request. Do not claim a check passed unless you ran it.
