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

Manual tests use disposable presets and programs, isolated from production data. For changes to launching, check a valid executable, a missing executable, a program that is already running, arguments and delays. For persistence, restart the app and confirm the expected data survives. Test JSON import with disposable data. For UI changes, check both languages, light and dark themes, and window resizing. For installer changes, test installation and uninstallation on a disposable Windows environment.

Pull request verification records include the Windows version, build configuration, executed checks and observed results.

## Runtime behavior

- App.xaml.cs initializes settings, localization, theme and default DataService storage, then opens the main window and checks for updates.
- LauncherService sorts entries by Order. Full launch applies delays and records history. Missing-only launch takes one process snapshot and matches paths or filenames, ignores delays and does not record history or increment the preset launch counter. Both modes update per-program usage on successful start requests.
- ClosePresetProcessesAsync requests window closure, waits 500 ms and may kill the process. Processes without a main window can be killed immediately. Use disposable programs for testing.
- History retains up to 100 entries by default. Full launches record success and failure counts, and clearing history keeps program usage. Statistics rank up to ten executable paths by accepted start requests.
- Settings shows the actual DataService path. Saved legacy DataPath values remain readable and have never pointed the startup service to another file.
- Restore accepts an object with `Format: "PresetLauncher.AppData"`, `Version: 1` and `Data` containing `Presets`, `History` and `UsageStats`. Older raw AppData files with the same collections are accepted. The active store retains the raw format. Restore keeps a copy of the previous file and uses a same-directory temporary file and replacement. A failed load keeps the original file available for recovery.
- The editor saves editable fields onto an isolated copy based on the current preset. Cleanup rechecks the reviewed paths. Reset uses AppSettings defaults and preserves the legacy minimum version and the separate protected Security directory.
- UpdateService reads the latest stable GitHub release and validates the policy, asset size and SHA-256 digest. UpdateCoordinator owns startup and manual update dialogs. Known mandatory updates remain enforced offline via the stored minimum version. See [release operations](RELEASING.md).

## GitHub label automation

The Cloudflare Worker in `tools/auto-labeler/` refreshes open PRs on `opened`, `reopened` and `synchronize` deliveries. The repository maintainer can also comment `/label-this` on an open issue or PR. Each refresh reads the current GitHub labels and, for a PR, its changed files. Refreshes add missing labels and preserve all existing labels, including manually assigned labels and labels for paths later removed from a PR. Priority, status, resolution and update policy remain under manual control.

A title edit leaves the existing type label in place. On the next refresh, the current Conventional Commit title supplies the type if the PR has no existing type other than `type: security`. To reclassify a renamed PR, remove its old type label in GitHub and comment `/label-this`. In the current catalog, `perf` maps to `type: enhancement`, and `ci`, `build`, `test`, `chore`, `style` and `revert` map to `type: maintenance`.

The existing catalog includes `area: ci`, `area: documentation`, `area: platform` and `area: tests`. `area: launcher` covers application execution, and `area: processes` covers process detection and termination. A file that handles both can receive both labels. `area: dependencies` covers package manifests, lockfiles, NuGet configuration and SDK pins. `type: security` flags implementation paths for password and update verification, release signing and update policy, SDK integrity and webhook authentication. It can appear alongside the Conventional Commit type.

The repository webhook uses the Worker's HTTPS `/github` endpoint, `application/json`, SSL verification, and a shared secret. It subscribes to pull requests and issue comments. The Worker stores `GITHUB_TOKEN` and `WEBHOOK_SECRET` as secrets; the token requires pull request file read and issue label write access. [GitHub webhook configuration](https://docs.github.com/en/webhooks/using-webhooks/creating-webhooks)

A refresh with one file page makes up to four GitHub API requests. Deliveries with no missing labels skip the catalog read and label write. GitHub expects a response within 10 seconds. If a large PR or slow API causes a delivery to fail, redeliver it or use `/label-this`. [GitHub webhook delivery guidance](https://docs.github.com/en/webhooks/using-webhooks/best-practices-for-using-webhooks) · [Cloudflare Workers limits](https://developers.cloudflare.com/workers/platform/limits/)

The CircleCI `plan-release` job checks Worker syntax and runs the regression suite. Run `node --check tools/auto-labeler/worker.mjs` and `node --test tools/auto-labeler/worker.test.mjs` from the repository root to verify locally.

## Continuous integration

CircleCI retains the nine named required checks. A lightweight setup job selects the executor, and Linux checks PR metadata, tests the Python release contracts and plans validation using the actual PR head and base SHA. Eligible non-application, non-releasing changes do not allocate a Windows VM. All other changes retain the existing validation lanes. When Windows runs, the Medium VM restores and audits the x64 test graph, runs the full x64 regression suite and retains JUnit, TRX and Cobertura. Linux gates verify genuine Windows evidence for the same commit, version and lane, or explicitly report that Windows validation is not applicable after the Linux scope check.

| Lane | Windows work |
| --- | --- |
| Non-releasing documentation, website, issue/PR template and allowlisted label Worker changes | No Windows VM when the diff is proven eligible; Linux release-policy and JavaScript checks still run. |
| Ordinary application PRs | Audited x64 test restore and x64 compile/regression. |
| Architecture-sensitive PRs | x64 regression, three isolated publish restores, three PE checks and installers, x64 and WOW64 x86 smoke, and ARM64-setup rejection on x64. |
| Main commits outside the Linux-only lane, including every planned release | Full validation. A planned production release also verifies a historical installer for each architecture previously released and stages the complete release for Linux publication. |

The Linux-only lane requires a regular-file-only diff against the PR's current main base or the main commit's sole parent, plus a non-releasing release plan. Mixed, stale, stacked, unknown or uncertain changes retain the existing Windows lane. Renames count both paths. Installer, updater, startup, interop, project, dependency, publish, and CI changes take the full lane. New and renamed C# or XAML files, unknown paths, ambiguous change records, and a mismatched base/head also take the full lane. Deleted paths keep their previous classification. Known platform API changes are checked in existing application sources. Ordinary edits with no platform indicators keep the x64 lane, whose regression compile satisfies the build check. Full-lane reports distinguish PE inspection, native x64 startup and WOW64 x86 startup. Use a disposable Windows ARM64 environment for native application smoke and for x64 emulation verification. Use suitable 32-bit Windows hardware or a VM for native x86 verification.

The x64 host uses the SDK pinned in `global.json`. The SDK archive is checked against Microsoft's SHA-512 before extraction. Exact input NuGet and SDK caches are restored once and saved only on main with seven-day retention. NuGet audit still queries the advisory feeds. Full validation restores the publish targets separately with a shared package cache. Trimming and ReadyToRun remain disabled. Release packaging uses solid `lzma2/fast`. Publish directories stay on the worker. The workspace retains one copy of the installers, checksums and evidence. Downstream jobs may download the same workspace layer independently. Only reports and diagnostics are stored as CircleCI artifacts.

Run the non-UI tests locally on Windows:

```powershell
dotnet test tests/PresetLauncher.Tests/PresetLauncher.Tests.csproj -c Release -r win-x64 -p:Platform=x64 --settings tests/ci.runsettings --collect "Code Coverage;Format=Cobertura"
```

The regression suite reads the exact installer fixtures through production data and settings services. The Python publisher signs a fixed cross-language fixture with a dedicated test key, and MSTest passes its release JSON through both production C# verifiers. Regenerate it with `python .circleci/test_sign_update.py` and OpenSSL on PATH. The checked-in signatures stay fixed between regenerations.

The installed application must expose enabled preset controls in Windows UI Automation, including the edit control for the fixture preset. The check exercises the existing startup gate and fails on premature exit, recovery mode or a readiness timeout. The smoke script refuses to run outside CircleCI or against existing user data. It checks the validated fixture bytes and protected policy files across installation, uninstallation and upgrades from a verified matching public predecessor. Live updater installation and older Windows versions require separate validation.

The intended trigger policy is one push pipeline for main and one validation pipeline per PR revision, with per-job commit statuses and no release secrets available to forks. Cancellation of superseded branch pipelines must exclude main publication.

### Compute and storage

The Linux-only lane allocates no Windows executor. Application validation uses one Windows Medium VM per pipeline. CircleCI reports compute consumption by resource class; cache transfers, workspace transfers and artifact storage are accounted for separately.

The five evidence gates attach the same upstream workspace. Installer assets are retained in the workspace for publication; GitHub Releases is the permanent distribution location.

[CircleCI credit usage](https://circleci.com/docs/guides/plans-pricing/credits/) · [Workspace behavior](https://circleci.com/docs/guides/orchestrate/workspaces/)
