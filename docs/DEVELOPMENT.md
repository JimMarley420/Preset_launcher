# Development notes

The desktop application uses C#, .NET 10 LTS, WinUI 3 and Windows App SDK 2.7. It references CommunityToolkit.Mvvm and Newtonsoft.Json. The installer uses Inno Setup 6.

## Build from source

Use Windows with the .NET 10 LTS SDK (10.0.404 or a later 10.0.4xx patch) and Visual Studio 2026 with WinUI / Windows App SDK development tools. From PowerShell:

```powershell
git clone https://github.com/JimMarley420/Preset_launcher.git
cd Preset_launcher
dotnet build .\PresetLauncherWinUI3\PresetLauncherWinUI3.csproj -c Release -p:Platform=x64
```

To produce the self-contained x64 installer, install Inno Setup 6 and run:

```powershell
.\Installer\build.ps1
```

The installer is written to `Installer/Output/`. Local builds default to version `0.0.0` and do not check for production updates. CircleCI calculates published versions automatically. See [release operations](RELEASING.md) for versioning and publication.

## Source layout

| Path | Responsibility |
| --- | --- |
| `PresetLauncherWinUI3/Models/` | Presets, executable entries, settings and launch history. |
| `PresetLauncherWinUI3/Services/` | Process launching, persistence, localization, themes, password verification and updates. |
| `PresetLauncherWinUI3/Views/Pages/` | Presets, history, statistics and settings. |
| `PresetLauncherWinUI3/Views/Dialogs/` | Preset editor. |
| `Installer/` | Publishing and installer packaging. |

Open `PresetLauncherWinUI3/PresetLauncherWinUI3.sln` in Visual Studio 2026 with the WinUI workload and select x64. Build and run the application locally. For packaging, see [Build from source](#build-from-source).

## Manual checks

Use test presets containing harmless programs, not your production data. For changes to launching, check a valid executable, a missing executable, a program that is already running, arguments and delays. For persistence, restart the app and confirm the expected data survives. Test JSON import with disposable data. For UI changes, check both languages, light and dark themes, and window resizing. For installer changes, test installation and uninstallation on a disposable Windows environment.

Record the Windows version, configuration and results in the pull request. Do not claim a check passed unless you ran it.

## Runtime behavior

- App.xaml.cs initializes settings, localization, theme and default DataService storage, then opens the main window and checks for updates.
- LauncherService sorts entries by Order. Full launch applies delays and records history. Missing-only launch takes one process snapshot and matches paths or filenames, ignores delays and does not record history or increment the preset launch counter. Both modes update per-program usage on successful start requests.
- ClosePresetProcessesAsync requests window closure, waits 500 ms and may kill the process. Processes without a main window can be killed immediately. Use disposable programs for testing.
- History retains up to 100 entries by default. Its filter handler only refreshes the unfiltered list. Statistics rank up to ten executable paths. Percentages are relative to the most-used path, not shares of all launches.
- DataService uses the default LocalAppData file at startup, regardless of the saved DataPath value. Settings Browse combines DataPath with data.json even though the default already includes the filename. It does not change storage. Cleanup removes entries marked invalid, not temporary files. Reset clears presets, history, usage counts and settings.
- Preset.Clone, DataService.ExportPreset and PasswordService are helpers without current UI actions. ConfirmBeforeLaunch is persisted but the launch handler always asks for confirmation. Do not advertise these as working features.
- UpdateService reads the latest stable GitHub release and validates the policy, asset size and SHA-256 digest. UpdateCoordinator owns startup and manual update dialogs. Known mandatory updates remain enforced offline via the stored minimum version. See [release operations](RELEASING.md).

## Continuous integration

CircleCI runs the same named checks for each validated PR. The release plan selects the installer lane from the diff against the PR's actual base, including both paths of a rename. Changes to the installer, updater, app startup and update settings, SDK and NuGet declarations, publishing profiles, build scripts and CircleCI configuration require the full lane. Main always runs the full lane.

| Job | Responsibility |
| --- | --- |
| validate-pr-title | Validate the Conventional Commit title and stacked PR eligibility. |
| validate-update-label | Validate the update policy label independently. |
| plan-release | Test release planning, calculate version and minimum supported version, and select the validation lane. |
| dependency-audit | Restore and audit the application and test graph on Windows. |
| test-windows | Compile the app and test projects, run MSTest and retain JUnit, TRX and application coverage in both lanes. |
| build-windows | In the full lane, audit and publish the self-contained x64 graph, compile the Inno Setup installer on the same VM, and persist the installer and checksum. In the fast lane, the test job supplies the application compilation check. |
| package-installer | Verify the full-lane installer and checksum after build, audit and tests succeed. Log the lane decision on fast PRs. |
| smoke-installer | In the full lane, verify the checksum, exercise fresh install, startup and uninstall, and verify staged artifact integrity. Full-lane PRs and planned production releases also upgrade from the previous public installer. |
| publish-release | Publish the smoke-tested installer and checksum on main when a release is planned. |

The dependency audit runs in parallel with release planning and the test path. NuGet caches are keyed by branch and the tracked restore inputs, including SDK, projects, props, targets, lock files and NuGet configuration. PRs can read a matching main cache. The serialized audit job saves caches only on main with seven-day retention. Restore still queries the vulnerability feeds and fails on the configured audit errors. The workflow transfers the release plan, the compressed installer and checksum, and the smoke-verified checksum. Published binaries stay on the VM that compiles the installer.

Install the [.NET 10 LTS SDK](https://dotnet.microsoft.com/download/dotnet/10.0) and run `dotnet --version` from the repository root. `global.json` requires 10.0.404 or a later patch in the 10.0.4xx feature band. CircleCI uses the exact pin and verifies the x64 host path and resolved SDK. If the image lacks the pin, it verifies the SHA-512 of the Microsoft SDK archive before extraction, whether it came from cache or Microsoft. Update `.circleci/dotnet-sdk.sha512` from Microsoft release metadata when changing the pin. The audit job also owns the SDK archive cache. A cache miss downloads the Microsoft archive and runs the same checksum and SDK version checks. For timing comparisons, use the individual cache, SDK preparation and audited restore steps in CircleCI. CircleCI verifies the official Inno Setup 6.7.1 download by SHA-256 and installs the compiler in portable mode.

The x64 Release installer bundles both the .NET and Windows App SDK runtimes. ReadyToRun is disabled based on the packaging benchmark. Trimming stays disabled to preserve reflection-based serialization and WinUI behavior. The Windows API target remains build 19041 and the installer minimum remains build 17763. The production packaging path uses Inno Setup with solid `lzma2/fast` compression. The review benchmark measured ReadyToRun and the Inno compression levels separately and the final full lane verifies the selected combination, including startup and upgrade.

Run the non-UI tests locally on Windows:

```powershell
dotnet test tests/PresetLauncher.Tests/PresetLauncher.Tests.csproj -c Release -r win-x64 -p:Platform=x64 --settings tests/ci.runsettings --collect "Code Coverage;Format=Cobertura"
```

Tests exercise real application classes with temporary files, not mocked copies. They do not launch or kill external applications, contact GitHub, or automate WinUI controls. Updater tests use a deterministic HTTP handler to exercise the real verification code. The installer smoke test waits for the installed WinUI application to open its main window. It refuses to run outside CircleCI or against an existing installation. The smoke test also checks upgrading from the latest public release on full-lane PRs and when a new version is planned. UI interactions, actual application launching, live updater download/install and older Windows versions still require separate validation.

CircleCI setup: connect this GitHub repository and select .circleci/config.yml. Enable branch-push triggers (including main), avoid duplicate push/PR triggers, and enable auto-cancel of redundant non-default-branch workflows. Once the first Windows run succeeds, add the observed job status checks (or the complete workflow check) to GitHub branch protection. Keep commit status updates enabled for individual job status on pull requests. The CircleCI Checks app reports the workflow status separately. Keep secrets unavailable to fork builds. Use the Tests and Artifacts tabs to review results, coverage and installer logs. The Linux open-source credit allowance does not cover Windows builds. Monitor Windows credits and artifact storage.

References: [Windows execution](https://circleci.com/docs/guides/execute/using-windows/), [dependency caching](https://circleci.com/docs/guides/optimize/caching/), [test results](https://circleci.com/docs/guides/test/collect-test-data/), [auto-cancel](https://circleci.com/docs/guides/orchestrate/skip-build/), [WinUI testing](https://learn.microsoft.com/en-us/windows/apps/develop/testing/).
