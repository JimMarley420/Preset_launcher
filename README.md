<h1 align="center">PresetLauncher</h1>

<p align="center">Open your Windows apps together in one click.</p>

<p align="center">
  <strong>English</strong> · <a href="README.fr.md">Français</a>
</p>

<p align="center">
  <a href="https://github.com/JimMarley420/Preset_launcher/releases/latest"><img alt="Release" src="https://img.shields.io/github/v/release/JimMarley420/Preset_launcher?style=flat-square"></a>
  <a href="https://github.com/JimMarley420/Preset_launcher/releases"><img alt="Downloads" src="https://img.shields.io/github/downloads/JimMarley420/Preset_launcher/total?style=flat-square"></a>
  <a href="LICENSE"><img alt="MIT License" src="https://img.shields.io/badge/license-MIT-lightgrey?style=flat-square"></a>
</p>

<p align="center">
  <strong><a href="https://github.com/JimMarley420/Preset_launcher/releases/latest">Download for Windows</a></strong> ·
  <a href="https://github.com/JimMarley420/Preset_launcher/releases">Releases</a> ·
  <a href="https://github.com/JimMarley420/Preset_launcher/issues">Issues and suggestions</a>
</p>

<p align="center">
  <img src="https://github.com/user-attachments/assets/d874f101-7f59-45f0-b0a0-7ce8702f2735" alt="PresetLauncher application" width="880">
</p>

## About

PresetLauncher groups programs into presets. Create a preset for work, gaming or any routine that involves opening the same apps.

* Start every program in a preset or just the ones that aren't running.
* Add apps, shortcuts and scripts, with individual launch delays and optional administrator privileges.
* Import presets from JSON, filter launch history and back up application data.
* Choose between light and dark themes, in English or French.

## Installation

Download the installer for your processor from the [latest release](https://github.com/JimMarley420/Preset_launcher/releases/latest).

| Windows processor | Installer |
| --- | --- |
| x64 | `PresetLauncher_Setup_<version>.exe` |
| 32-bit x86 | `PresetLauncher_Setup_<version>_x86.exe` |
| ARM64 | `PresetLauncher_Setup_<version>_arm64.exe` |

Check **Settings → System → About → System type**. Use a Microsoft-supported Windows version. ARM64 packages are cross-compiled and inspected on x64 CI. Native ARM64 installation and startup still require validation on ARM64 hardware. Existing installations update in their current architecture, including under emulation. To switch architectures, uninstall the current copy first. Your presets, history, statistics, settings and protected update policy survive uninstallation.

If the legacy updater in version 1.2.6 or earlier cannot find the latest release, install it manually.

## Documentation

[User guide](docs/USAGE.md) · [Build from source](docs/DEVELOPMENT.md#build-from-source) · [Contributing](CONTRIBUTING.md) · [Security](SECURITY.md)

Built with C#, .NET and WinUI 3. Distributed under the [MIT license](LICENSE).
