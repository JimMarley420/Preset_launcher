# Development notes

The desktop application uses C#, .NET 10 LTS, WinUI 3 and Windows App SDK 2.7. It references CommunityToolkit.Mvvm and Newtonsoft.Json. The installer uses Inno Setup 6.

## Build from source

Use Windows with the .NET 10 LTS SDK (10.0.404 or a later 10.0.4xx patch) and Visual Studio 2026 with WinUI / Windows App SDK development tools. From PowerShell:

```powershell
git clone https://github.com/JimMarley420/Preset_launcher.git
cd Preset_launcher
dotnet build .\PresetLauncherWinUI3\PresetLauncherWinUI3.csproj -c Release -p:Platform=x64
```

Use Inno Setup 6 to build a self-contained installer on Windows. The x64 default and `-Version` option are available on every local build.

```powershell
.\Installer\build.ps1 -Architecture x64
.\Installer\build.ps1 -Architecture x86
.\Installer\build.ps1 -Architecture arm64
.\Installer\build.ps1 -Architecture all
```

The mapping in `Installer/architectures.json` supplies the MSBuild platform, RID, output suffix, Inno compatibility and install mode, and expected PE machine. The script isolates restore and build intermediates by target and checks the app host, .NET runtime and WinUI binaries. Use `-UsePinnedCompiler` for CircleCI’s SHA-256-verified 6.7.1 compiler. The installers are written to `Installer/Output/`. Local builds default to version `0.0.0` and do not check for production updates. CircleCI calculates published versions automatically. See [release operations](RELEASING.md) for versioning and publication.

## Source layout

| Path | Responsibility |
| --- | --- |
| `PresetLauncherWinUI3/Models/` | Presets, executable entries, settings and launch history. |
| `PresetLauncherWinUI3/Services/` | Process launching, persistence, localization, themes, password verification and updates. |
| `PresetLauncherWinUI3/Views/` | Desktop pages, preset editor and native dialogs. |
| `Installer/` | Publishing and installer packaging. |

Open `PresetLauncherWinUI3/PresetLauncherWinUI3.sln` in Visual Studio 2026 with the WinUI workload and select x64. Build and run the application locally. For packaging, see [Build from source](#build-from-source).

## Manual checks

Use test presets containing harmless programs, not your production data. For changes to launching, check a valid executable, a missing executable, a program that is already running, arguments and delays. For persistence, restart the app and confirm the expected data survives. Test JSON import with disposable data. For UI changes, check both languages, light and dark themes, and window resizing. For installer changes, test installation and uninstallation on a disposable Windows environment.

Record the Windows version, configuration and results in the pull request. Do not claim a check passed unless you ran it.

## Runtime behavior

- App.xaml.cs initializes settings, localization, theme and default DataService storage, then opens the main window and checks for updates.
- LauncherService sorts entries by Order. Full launch applies delays and records history. Missing-only launch takes one process snapshot and matches paths or filenames, ignores delays and does not record history or increment the preset launch counter. Both modes update per-program usage on successful start requests.
- ClosePresetProcessesAsync requests window closure, waits 500 ms and may kill the process. Processes without a main window can be killed immediately. Use disposable programs for testing.
- History retains up to 100 entries by default. Full launches record success and failure counts, and clearing history keeps program usage. Statistics rank up to ten executable paths by accepted start requests.
- Settings shows the actual DataService path. Saved legacy DataPath values remain readable and have never pointed the startup service to another file.
- Restore accepts an object with `Format: "PresetLauncher.AppData"`, `Version: 1` and `Data` containing `Presets`, `History` and `UsageStats`. Older raw AppData files with the same collections are accepted. The active store retains the raw format. Restore keeps a copy of the previous file and uses a same-directory temporary file and replacement. A failed load keeps the original file available for recovery.
- The editor saves editable fields onto an isolated copy based on the current preset. Cleanup rechecks the reviewed paths. Reset uses AppSettings defaults and preserves the legacy minimum version and the separate protected Security directory.
- UpdateService reads the latest stable GitHub release and validates the policy, asset size and SHA-256 digest. UpdateCoordinator owns startup and manual update dialogs. Known mandatory updates remain enforced offline via the stored minimum version. See [release operations](RELEASING.md).

## Continuous integration

CircleCI retains the eight named required checks. Linux checks PR metadata, tests the Python release contracts and plans validation using the actual PR head and base SHA. A single Windows Medium VM restores and audits the x64 test graph, runs the full x64 regression suite and retains JUnit, TRX and Cobertura. Linux gates verify the Windows evidence for the same commit, version and lane.

| Lane | Windows work |
| --- | --- |
| Documentation and ordinary application PRs | Audited x64 test restore and x64 compile/regression. Documentation retains the same regression guarantee. |
| Architecture-sensitive PRs | x64 regression, three isolated publish restores, three PE checks and installers, x64 and WOW64 x86 smoke, and ARM64-setup rejection on x64. |
| Every main commit | Full validation. A planned production release also verifies a historical installer for each architecture previously released and stages the complete release for Linux publication. |

Installer, updater, startup, interop, project, dependency, publish, and CI changes take the full lane. Renames report both paths. Deleted paths keep their previous classification. An unknown path or mismatched base/head takes the full lane. The regression compile satisfies the build check on fast PRs. Full-lane reports distinguish PE inspection, native x64 startup and WOW64 x86 startup. Use a disposable Windows ARM64 environment for native application smoke and for x64 emulation verification. Use suitable 32-bit Windows hardware or a VM for native x86 verification.

The x64 host uses the SDK pinned in `global.json`. The SDK archive is checked against Microsoft's SHA-512 before extraction. Exact input NuGet and SDK caches are restored once and saved only on main with seven-day retention. NuGet audit still queries the advisory feeds. Full validation restores the publish targets separately with a shared package cache. Trimming and ReadyToRun remain disabled. Release packaging uses solid `lzma2/fast`. Publish directories stay on the worker. The workspace retains one copy of the installers, checksums and evidence. Downstream jobs may download the same workspace layer independently. Only reports and diagnostics are stored as CircleCI artifacts.

Run the non-UI tests locally on Windows:

```powershell
dotnet test tests/PresetLauncher.Tests/PresetLauncher.Tests.csproj -c Release -r win-x64 -p:Platform=x64 --settings tests/ci.runsettings --collect "Code Coverage;Format=Cobertura"
```

Tests exercise real application classes with temporary files, not mocked copies. They do not launch or kill external applications, contact GitHub, or automate WinUI controls. Updater tests use a deterministic HTTP handler to exercise the real verification code. The installer smoke test waits for the installed WinUI application to open its main window. It refuses to run outside CircleCI or against an existing installation. The smoke test also checks upgrading from the latest public release on full-lane PRs and when a new version is planned. UI interactions, actual application launching, live updater download/install and older Windows versions still require separate validation.

Configure one branch-push trigger for main and one PR validation trigger for branches where the integration supports this split. Disable overlapping push, PR and comment triggers for the same revision. Keep per-job commit statuses and fork secret isolation. If the integration supports cancellation of superseded non-default-branch workflows, verify main publication remains excluded. The repository does not require a schedule or paid infrastructure.

### Compute and storage measurement

CircleCI advertises 30,000 monthly Free credits for Windows/macOS and up to 400,000 additional eligible public Linux credits. Windows Medium is 40 credits/minute, Windows Large is 120 and Small Linux Docker is 5. Compute by summing each resource class's job durations. Track cache transfers, workspace uploads/downloads and artifacts separately in the Usage dashboard. The Windows-only theoretical ceiling is 750 Medium minutes per month before other eligible consumption. [Open-source allowance](https://circleci.com/open-source/) · [Price list](https://circleci.com/pricing/price-list/) · [Using credits](https://circleci.com/docs/guides/plans-pricing/credits/)

| Workflow | Windows jobs / SDK preparations / restore scopes / compiler preparations | Windows min | Linux min | Estimated compute credits, Windows + Linux | Wall min |
| --- | --- | ---: | ---: | ---: | ---: |
| Original x64 fast, pipeline 72 | 4 / 2 / 2 / 0 | 3.33 | 1.05 | 133 + 5 | 3.76 |
| Original x64 full PR, pipeline 67 | 4 / 3 / 3 / 1 | 7.82 | 1.22 | 313 + 6 | 6.31 |
| Original x64 release, pipeline 68 | 4 / 3 / 3 / 1 | 6.89 | 1.22 | 276 + 6 | 6.17 |
| Consolidated ordinary | 1 / 1 / 1 / 0 | NOT RUN | NOT RUN | 40 × Windows min + 5 × Linux min | NOT RUN |
| Consolidated full | 1 / 1 / 4 / 1 | NOT RUN | NOT RUN | 40 × Windows min + 5 × Linux min | NOT RUN |
| Consolidated production | 1 / 1 / 4 / 1 | NOT RUN | NOT RUN | Full lane plus Linux publication | NOT RUN |

The original figures are public job elapsed times rounded from the API. The estimates exclude storage, network and provider billing adjustments. On the warm fast baseline the two Windows jobs spent 56.1 s selecting/extracting the SDK, 35.6 s restoring caches, 7.3 s restoring and auditing dependencies, and 55.4 s running the regression command including its restore and compile. The SDK cache transferred 274 MiB and NuGet 965 MiB per hit. On the warm non-releasing main pipeline 73, x64 publish took 44.7 s including restore, Inno preparation 9.6 s, compression 22.4 s, smoke 18.1 s and workspace upload 16.9 s for 76 MiB. A release smoke with historical upgrade took 114.7 s on pipeline 68. A controlled cold/warm comparison, billed credits and organization-level storage totals were unavailable.

To reproduce, retrieve the project pipelines from `https://circleci.com/api/v2/project/gh/JimMarley420/Preset_launcher/pipeline`, then use `/api/v2/pipeline/{id}/workflow` and `/api/v2/workflow/{id}/job` for timestamps. Query `/api/v1.1/project/github/JimMarley420/Preset_launcher/{job_number}` for individual step durations in `steps[].actions[].run_time_millis`. Check the corresponding logs for cache hits or misses and transferred bytes. Compare the first naturally cold build following an input change to the next compatible warm build. Use one full PR and the next actual production release for the remaining rows. The build script logs per-target restore, publish and compression times. Consult Usage before projecting monthly capacity.

A local warm build with .NET SDK 10.0.404 and Inno Setup 6.7.3 produced 80.1 MB (x64), 55.6 MB (x86) and 77.5 MB (ARM64) installers. The three publish and PE steps took 10.7 s, 12.4 s and 12.3 s respectively. Compression took 20.0 s, 16.1 s and 18.2 s. The combined payload was 213.2 MB before workspace transport. These are workstation measurements from `.\Installer\build.ps1 -Architecture all`. Hosted execution and transfer times require a CircleCI run.

A dedicated self-hosted Windows runner is an optional future alternative. It requires maintenance, an isolated disposable execution environment and separate runner storage/egress accounting. Keep public PR builds and release secrets off personal runners. [CircleCI runner documentation](https://circleci.com/docs/runner/overview/)
