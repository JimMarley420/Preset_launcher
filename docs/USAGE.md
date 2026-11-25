# User guide

[Français](USAGE.fr.md) · [PresetLauncher](../README.md)

## Getting started

Install the [latest release](https://github.com/JimMarley420/Preset_launcher/releases/latest) on Windows x64. The installer declares Windows 10 build 17763 as its minimum. The project targets build 19041 and compatibility with older builds has not been verified. Both .NET and Windows App SDK runtimes are included.

Create a preset, give it a name and add programs using the file picker or the list of running processes. Each entry can have a delay and request administrator privileges. Windows determines how to open scripts and shortcuts. Presets can be searched by name or description, edited, imported from JSON or deleted.

## Launching programs

| Action | What happens |
| --- | --- |
| Launch All | Starts programs in preset order, applies delays and records the launch. |
| Launch Missing Only | Skips programs already detected by executable path or filename. No delays or history entry. |
| Close processes | Requests closure of matching processes and may forcibly terminate them after confirmation. |

Save your work before closing processes. This also affects matching programs started outside PresetLauncher.

## History and appearance

History records full launches, their duration and successful start requests. A successful request does not mean the app has finished loading. Statistics show preset counts, recorded launches and the ten most used program paths.

Settings include English and French, light, dark and system themes, Mica or Acrylic on supported systems, cleanup of invalid entries and data reset.

## Updates

The app checks GitHub Releases at startup and from Settings. It verifies installer size and SHA-256 before opening the installer.

* Optional updates can be postponed or skipped.
* Recommended updates can be postponed for 24 hours, but cannot be skipped permanently.
* Required updates must be installed to continue using the app.

Known minimum supported versions remain enforced when offline. The reminder for recommended updates is checked at startup, not while the app is left running. See [Security](../SECURITY.md) for verification details.

Versions 1.2.6 and earlier use the old update server. If the update is not offered, install the latest release manually. Existing presets and preferences are retained.

## Backing up your data

By default, data is stored in `%LocalAppData%\PresetLauncher\`.

| File | Contents |
| --- | --- |
| `data.json` | Presets, launch history and usage statistics |
| `settings.json` | Application preferences |

Uninstalling preserves these files. Close the app before copying both files to a safe location or restoring them on another PC. Back up before resetting data. The Browse control in Settings does not relocate active storage and should not be relied on for backups.

Check executable paths, arguments and administrator settings in any imported preset before running it.

## Current limitations

The history filters and launch confirmation preference do not currently affect behavior. Preset duplication, individual export, argument editing and password locking are not available as working UI actions. Imported JSON presets can include arguments that are passed to Windows.
