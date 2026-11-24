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

## Runtime behavior

- App.xaml.cs initializes settings, localization, theme and default DataService storage, then opens the main window and checks for updates.
- LauncherService sorts entries by Order. Full launch applies delays and records history; missing-only launch takes one process snapshot and matches paths or filenames, ignores delays and does not record history or increment the preset launch counter. Both modes update per-program usage on successful start requests.
- ClosePresetProcessesAsync requests window closure, waits 500 ms and may kill the process; processes without a main window can be killed immediately. Use disposable programs for testing.
- History retains up to 100 entries by default. Its filter handler only refreshes the unfiltered list. Statistics rank up to ten executable paths; percentages are relative to the most-used path, not shares of all launches.
- DataService uses the default LocalAppData file at startup, regardless of the saved DataPath value. Settings Browse combines DataPath with data.json even though the default already includes the filename; it does not change storage. Cleanup removes entries marked invalid, not temporary files. Reset clears presets, history, usage counts and settings.
- Preset.Clone, DataService.ExportPreset and PasswordService are helpers without current UI actions. ConfirmBeforeLaunch is persisted but the launch handler always asks for confirmation. Do not advertise these as working features.
- UpdateService reads version.txt and downloads the corresponding setup executable from the configured HTTPS host. The app does not explicitly validate a signature or checksum before executing it.
