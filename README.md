# PresetLauncher

Launch the Windows apps you use together, without opening each one by hand.

**English** · [Français](README.fr.md)

[![Release](https://img.shields.io/github/v/release/JimMarley420/Preset_launcher?style=flat-square)](https://github.com/JimMarley420/Preset_launcher/releases/latest) [![License: MIT](https://img.shields.io/badge/license-MIT-lightgrey?style=flat-square)](LICENSE)

[Download for Windows](https://github.com/JimMarley420/Preset_launcher/releases/latest) · [Report a problem](https://github.com/JimMarley420/Preset_launcher/issues/new/choose) · [Build from source](#build-from-source)

![PresetLauncher application window](https://github.com/user-attachments/assets/4bc92444-c889-46ad-932b-5ef402787a71)

## One preset, several apps

A preset is a saved list of programs. Create one for work, another for gaming, or one for the tools you open every morning. Launch the entire list or start only the programs that are not already running.

| | What you can do |
| --- | --- |
| Presets | Create, duplicate, import and export groups of programs as JSON. |
| Launch options | Set arguments, delays and administrator privileges for individual programs. |
| Running apps | Find existing processes, launch missing programs and request that running apps close. |
| History | Review launch results, durations and usage statistics. |
| Appearance | Choose light, dark or system theme; switch between English and French. |

Presets can be password-locked in the interface. This does not encrypt the underlying JSON files or restrict access to Windows executables.

## Install

Use a Windows 10 or 11 PC compatible with the x64 installer. Windows 10 version 2004 or later is the recommended baseline.

1. Open the [latest release](https://github.com/JimMarley420/Preset_launcher/releases/latest).
2. Download `PresetLauncher_Setup_<version>.exe` and run it.
3. Open PresetLauncher, create a preset and add the executables you want to launch.

The installer includes the application runtime. In-app updates are fetched from `version.multichat.cloud`, rather than directly from GitHub Releases.

## Local data

By default, PresetLauncher stores its files in `%LocalAppData%\PresetLauncher\`:

- `data.json` — presets, launch history and usage statistics.
- `settings.json` — application preferences.

The installer preserves user data on uninstall. Back up your files before moving to another PC. Review imported presets, particularly executable paths, arguments and administrator settings, before running them.

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
