# Development notes

The desktop application uses C#, .NET 10 LTS, WinUI 3 and Windows App SDK 2.7. It references CommunityToolkit.Mvvm and Newtonsoft.Json. The installer uses Inno Setup 6.

## Source layout

| Path | Responsibility |
| --- | --- |
| `PresetLauncherWinUI3/Models/` | Presets, executable entries, settings and launch history. |
| `PresetLauncherWinUI3/Services/` | Process launching, persistence, localization, themes, password verification and updates. |
| `PresetLauncherWinUI3/Views/Pages/` | Presets, history, statistics and settings. |
| `PresetLauncherWinUI3/Views/Dialogs/` | Preset editor. |
| `Installer/` | Publishing and installer packaging. |

Open `PresetLauncherWinUI3/PresetLauncherWinUI3.sln` in Visual Studio 2026 with the WinUI workload and select x64. Build and run the application locally. For packaging, follow the [root README](../README.md).

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
- UpdateService reads the latest stable GitHub release and validates the policy, asset size and SHA-256 digest. UpdateCoordinator owns startup and manual update dialogs. Known mandatory updates remain enforced offline via the stored minimum version. See [release operations](RELEASING.md).

## Continuous integration

CircleCI runs five Windows x64 jobs and a Linux planning job on branch pushes. A Linux PR validation job checks titles and labels, and a Linux publication job runs only on main:

| Job | Responsibility |
| --- | --- |
| validate-pr-title | Validate the Conventional Commit PR title and required maintainer update label. |
| plan-release | Run release planning tests and calculate the version and minimum supported version. |
| dependency-audit | Restore and audit application, publish and test dependencies; populate the NuGet cache. |
| build-windows | Publish the self-contained Release application to the workflow workspace. |
| test-windows | Build and execute the MSTest suite; retain JUnit, TRX and coverage reports. Runs independently of the publish job after the audit. |
| package-installer | Wait for both build and tests; compile the existing Inno Setup script from the published files without republishing. |
| smoke-installer | Install and uninstall the exact packaged executable in a fresh Windows VM; verify user data preservation and stage the installer with a SHA-256 digest. On PR branches and for main builds that plan a release, also upgrade from the previous public installer. |
| publish-release | Publish the verified installer and checksum to GitHub only on main when a release is planned. |

Only a package that passes all checks is staged as a verified CircleCI artifact. Workspaces carry build outputs and the installer between jobs in the same workflow; the NuGet cache carries dependencies between runs. The test job compiles the test project independently of the self-contained publish. Separate VMs increase startup and workspace overhead compared with a single job, but expose failures independently and allow build and tests to run concurrently. Publication requires the restricted CircleCI context, as described in [RELEASING.md](RELEASING.md). The old update server is not changed by CI.

Install the [.NET 10 LTS SDK](https://dotnet.microsoft.com/download/dotnet/10.0) and run `dotnet --version` from the repository root. `global.json` requires 10.0.404 or a later patch in the 10.0.4xx feature band. CircleCI uses the exact pin and installs it from Microsoft when the Windows Server 2022 image ships an older patch. CircleCI also pins Inno Setup 6.7.1.

The x64 Release installer bundles both the .NET and Windows App SDK runtimes. It uses ReadyToRun and disables trimming to preserve reflection-based serialization and WinUI behavior. The Windows API target remains build 19041 and the installer minimum remains build 17763. The production packaging path uses Inno Setup.

Run the non-UI tests locally on Windows:

```powershell
dotnet test tests/PresetLauncher.Tests/PresetLauncher.Tests.csproj -c Release -r win-x64 -p:Platform=x64 --settings tests/ci.runsettings --collect "Code Coverage;Format=Cobertura"
```

Tests exercise real application classes with temporary files, not mocked copies. They do not launch or kill external applications, contact GitHub, or automate WinUI controls. Updater tests use a deterministic HTTP handler to exercise the real verification code. The installer smoke test waits for the installed WinUI application to open its main window. It refuses to run outside CircleCI or against an existing installation. The smoke test also checks upgrading from the latest public release on PR branches and when a new version is planned. UI interactions, actual application launching, live updater download/install and older Windows versions still require separate validation.

CircleCI setup: connect this GitHub repository and select .circleci/config.yml. Enable branch-push triggers (including main), avoid duplicate push/PR triggers, and enable auto-cancel of redundant non-default-branch workflows. Once the first Windows run succeeds, add the observed job status checks (or the complete workflow check) to GitHub branch protection. Keep commit status updates enabled for individual job status on pull requests; the CircleCI Checks app reports the workflow status separately. Keep secrets unavailable to fork builds. Use the Tests and Artifacts tabs to review results, coverage and installer logs. The Linux open-source credit allowance does not cover Windows builds; monitor Windows credits and artifact storage.

References: [Windows execution](https://circleci.com/docs/guides/execute/using-windows/), [dependency caching](https://circleci.com/docs/guides/optimize/caching/), [test results](https://circleci.com/docs/guides/test/collect-test-data/), [auto-cancel](https://circleci.com/docs/guides/orchestrate/skip-build/), [WinUI testing](https://learn.microsoft.com/en-us/windows/apps/develop/testing/).
