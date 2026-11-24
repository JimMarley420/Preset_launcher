# PresetLauncher

Launch the Windows apps you use together, without opening each one by hand.

**English** · [Français](README.fr.md)

[![Release](https://img.shields.io/github/v/release/JimMarley420/Preset_launcher?style=flat-square)](https://github.com/JimMarley420/Preset_launcher/releases/latest) [![License: MIT](https://img.shields.io/badge/license-MIT-lightgrey?style=flat-square)](LICENSE) [![Downloads](https://img.shields.io/github/downloads/JimMarley420/Preset_launcher/total?style=flat-square)](https://github.com/JimMarley420/Preset_launcher/releases)

[![Windows](https://img.shields.io/badge/Windows-10%20%2F%2011-0078D6?style=flat-square)](https://www.microsoft.com/windows/) [![C#](https://img.shields.io/badge/C%23-239120?style=flat-square)](https://learn.microsoft.com/dotnet/csharp/) [![.NET 8](https://img.shields.io/badge/.NET-8-512BD4?style=flat-square&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/download/dotnet/8.0) [![WinUI 3](https://img.shields.io/badge/WinUI-3-0078D4?style=flat-square)](https://learn.microsoft.com/windows/apps/winui/winui3/) [![Windows App SDK](https://img.shields.io/badge/Windows%20App%20SDK-1.8-0078D4?style=flat-square)](https://learn.microsoft.com/windows/apps/windows-app-sdk/) [![Inno Setup](https://img.shields.io/badge/Inno%20Setup-6-555555?style=flat-square)](https://jrsoftware.org/isinfo.php)

[Download for Windows](https://github.com/JimMarley420/Preset_launcher/releases/latest) · [Report a problem](https://github.com/JimMarley420/Preset_launcher/issues/new/choose) · [Build from source](#build-from-source)

![PresetLauncher application window](https://github.com/user-attachments/assets/4bc92444-c889-46ad-932b-5ef402787a71)

## One preset, several apps

A preset is a named list of programs. Create one for work, another for gaming, or one for the tools you open every morning.

- Add programs using the file picker or by selecting an existing Windows process. The picker accepts executables, scripts and shortcuts; Windows decides how to open the selected file.
- Search presets by name or description, edit their contents, import a preset from JSON or delete it.
- Choose a delay and whether to request administrator privileges for each entry.

### Launch options

| Action | Behavior |
| --- | --- |
| Launch All | Starts entries in their stored order, applies each delay before launch and records the outcome. |
| Launch Missing Only | Skips programs detected by executable path or filename. This mode does not apply delays or create a history entry. |
| Close processes | After confirmation, attempts to close matching processes, then may forcibly terminate them. Save your work first; this can also close programs you started outside PresetLauncher. |

### History and settings

The History page shows full-launch timestamps, durations and successful start counts. It can be cleared. A successful start means Windows accepted the launch request, not that the application finished loading.

Statistics show preset counts, recorded full launches and the ten most-used program paths. Settings include English and French, light/dark/system themes, Mica or Acrylic where supported, cleanup of invalid executable entries, data reset and update checks.

### Current limitations

The history filter controls and the launch-confirmation preference are present but do not currently change behavior. Duplication, individual preset export, argument editing and password locking are not exposed as working controls in this version. Presets imported from JSON can contain launch arguments, which the launcher passes to Windows.

## Install

Use a Windows 10 or 11 PC compatible with the x64 installer. The installer declares Windows 10 build 17763 as its minimum; the project targets build 19041. Compatibility with older builds has not been verified.

1. Open the [latest release](https://github.com/JimMarley420/Preset_launcher/releases/latest).
2. Download `PresetLauncher_Setup_<version>.exe` and run it.
3. Open PresetLauncher, create a preset and add the executables you want to launch.

The installer includes the application runtime. The app checks for updates at startup and from Settings; installation requires confirmation. Updates are fetched from `version.multichat.cloud`, rather than directly from GitHub Releases.

## Local data

By default, PresetLauncher stores its files in `%LocalAppData%\PresetLauncher\`:

- `data.json` — presets, launch history and usage statistics.
- `settings.json` — application preferences.

The installer preserves user data on uninstall. Back up both files manually with the application closed before moving to another PC or using the reset action. The current Settings browse control attempts to copy data but does not relocate the active storage; do not rely on it as a backup. Review imported presets, particularly executable paths, arguments and administrator settings, before running them.

## Build from source

Use Windows with the .NET 8 SDK and Visual Studio 2022 with WinUI / Windows App SDK development tools. From PowerShell:

```powershell
git clone https://github.com/JimMarley420/Preset_launcher.git
cd Preset_launcher
dotnet build .\PresetLauncherWinUI3\PresetLauncherWinUI3.csproj -c Release -p:Platform=x64
```

To produce the self-contained x64 installer, install Inno Setup 6 and run:

```powershell
.\Installer\build.ps1
```

The installer is written to `Installer/Output/`. See [the development notes](docs/DEVELOPMENT.md) for the source layout and manual checks.

## Contributing

Found a reproducible bug or have a change in mind? Start with [CONTRIBUTING.md](CONTRIBUTING.md). For vulnerabilities, follow [SECURITY.md](SECURITY.md) instead of posting details publicly.

PresetLauncher is distributed under the [MIT license](LICENSE).
