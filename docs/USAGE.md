# User guide

[Français](USAGE.fr.md) · [PresetLauncher](../README.md)

## Getting started

Install the [latest release](https://github.com/JimMarley420/Preset_launcher/releases/latest) on Windows x64. The installer declares Windows 10 build 17763 as its minimum. The project targets build 19041 and compatibility with older builds has not been verified. The installer includes the .NET and Windows App SDK runtimes.

The navigation pane starts collapsed. The icons open Presets, History, Statistics and Settings. Use the menu button to show their labels.

In Presets, select **New preset** or a row’s **Edit** button. Enter a name and add programs from files or running processes. Use the arrows to change launch order. The gear beside each program opens its delay and administrator options. Missing or inaccessible paths stay in the list. **Cancel** or **Back** discards changes after confirmation if the draft has changed. Windows determines how to open scripts and shortcuts.

Search presets by name or description. **Import preset** adds one JSON preset with a new identity. Check paths, arguments and administrator options in presets you import before launching them.

## Launching programs

Choose a mode from **Launch**. **Confirm before launch** in Settings controls whether the selected mode asks for confirmation.

| Action | What happens |
| --- | --- |
| Launch all | Sends start requests in preset order, applies delays and records the run in history. |
| Launch missing only | Skips programs already detected by executable path or filename. This mode skips delays and does not record a run in history. |
| Close processes | Requests closure of matching processes and may forcibly terminate them after confirmation. This can affect programs started outside PresetLauncher. Save your work first. |

Windows accepting a start request does not mean the program has finished loading. Both launch modes count accepted requests in per-program usage statistics.

## History, statistics and appearance

History shows full launches, their duration and results. Filter by **All**, **Successful**, **Failed** or **Today**. Failed includes runs with both accepted and failed requests. Today follows the local calendar. **Clear history** keeps presets and usage statistics.

Statistics show the number of presets, recorded launches for current presets, distinct program paths with recorded usage and the ten most-used program paths.

Settings offer English and French, Light, Dark and System themes, and None, Mica or Acrylic backdrops. Material availability depends on Windows and its accessibility settings.

## Data management

**Data location** shows the active file, normally `%LocalAppData%\PresetLauncher\data.json`. Backups are selected independently.

- **Export data** saves an unencrypted versioned JSON backup of presets, executable configurations, launch history and usage statistics. It can contain executable paths, arguments, launch details and preset password hashes. Keep the file private. The backup excludes installed programs, application preferences and update-policy state.
- **Import data** accepts version 1 backups and earlier raw application-data JSON files up to 32 MiB. Review the summary before replacing the current data. Restore keeps stored identities, checks paths on the current computer and saves the previous data file beside the active file. Missing programs can be reviewed after restore.
- **Review references** previews executable files that could not be found, grouped by preset. Reconnect any unavailable drives or networks before confirming. Cleanup removes the reviewed references that are still unavailable and keeps presets, history, statistics and executable files.
- **Reset application** deletes presets, launch history and usage statistics and restores default preferences. Installed programs, external executable files and the protected update floor are kept. Export a backup first if you may need the data later.

Preset duplication, individual preset export, argument editing and password locking have no user-interface controls. Imported JSON presets can include arguments that Windows receives when launched.

## Updates

PresetLauncher checks GitHub Releases at startup and from Settings. It verifies the signed policy, installer size and SHA-256 before opening the installer.

- Optional updates can be postponed or skipped.
- Recommended updates can be postponed for 24 hours.
- Required updates must be installed to continue using the application.

Known minimum supported versions remain enforced offline. Recommended reminders are checked at startup. Versions 1.2.6 and earlier use the old update server. Install the latest release manually if no update is offered. See [Security](../SECURITY.md).
