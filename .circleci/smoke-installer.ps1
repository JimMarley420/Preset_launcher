# Run only on an isolated Windows CI VM, never on a developer's installed copy.
param(
    [Parameter(Mandatory = $true)][string]$PlanPath,
    [Parameter(Mandatory = $true)][string]$Architecture
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = Split-Path $PSScriptRoot -Parent
$plan = Get-Content $PlanPath -Raw | ConvertFrom-Json
$version = [string]$plan.version
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid planned version' }
$mapping = Get-Content "$root/Installer/architectures.json" -Raw | ConvertFrom-Json
$entry = $mapping.$Architecture
$setup = Join-Path $root "Installer\Output\PresetLauncher_Setup_${version}$($entry.suffix).exe"
$logs = Join-Path $root "artifacts\installer-logs\$Architecture"
New-Item -ItemType Directory -Force $logs | Out-Null
$installDir = Join-Path $env:TEMP "PresetLauncher-CI-$([guid]::NewGuid())"
$dataDir = Join-Path $env:LOCALAPPDATA 'PresetLauncher'
$dataFile = Join-Path $dataDir 'data.json'
$settingsFile = Join-Path $dataDir 'settings.json'
$policyFile = Join-Path $dataDir 'Security/update-policy.dat'
$policyLock = Join-Path $dataDir 'Security/update-policy.lock'
$shortcut = Join-Path ([Environment]::GetFolderPath('Programs')) 'PresetLauncher/PresetLauncher.lnk'
$uninstallKey = 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{C4A7B8D2-E3F5-4901-A2B3-C4D5E6F70819}_is1'
$registryPath = "HKCU:\$uninstallKey"
$installationKey = 'Software\PresetLauncher\Installation'
$architectureRegistryPath = "HKCU:\$installationKey"

if ($env:CIRCLECI -ne 'true') { throw 'Smoke test requires an isolated CircleCI VM' }
$machineKeys = [Microsoft.Win32.RegistryView]::Registry32, [Microsoft.Win32.RegistryView]::Registry64 |
    ForEach-Object {
        $hive = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::LocalMachine, $_)
        try {
            foreach ($subkey in $uninstallKey, $installationKey) {
                $key = $hive.OpenSubKey($subkey)
                if ($key) { $key.Dispose(); $true }
            }
        }
        finally { $hive.Dispose() }
    }
if ((Test-Path $dataDir) -or (Test-Path $registryPath) -or (Test-Path $architectureRegistryPath) -or
    $machineKeys -or (Test-Path $shortcut) -or
    (Test-Path "$env:LOCALAPPDATA\Programs\PresetLauncher")) {
    throw 'Existing PresetLauncher installation or data; refusing to run smoke test'
}
if (-not (Test-Path $setup -PathType Leaf)) { throw "Installer not found: $setup" }

function Invoke-Package([string]$Path, [string]$LogName, [switch]$NoClose) {
    $log = Join-Path $logs $LogName
    $arguments = '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CURRENTUSER /LANG=english /DIR="{0}" /TASKS="" /LOG="{1}"' -f $installDir, $log
    if ($NoClose) { $arguments += ' /NOCLOSEAPPLICATIONS /NORESTARTAPPLICATIONS' }
    $process = Start-Process -FilePath $Path -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru
    try { return $process.ExitCode }
    finally { $process.Dispose() }
}

if ($Architecture -eq 'arm64' -and [Runtime.InteropServices.RuntimeInformation]::OSArchitecture -ne 'Arm64') {
    $code = Invoke-Package $setup 'incompatible.log'
    $log = Get-Content (Join-Path $logs 'incompatible.log') -Raw
    if ($code -ne 1 -or $log -notmatch 'This program does not support the version of Windows your computer is running\.' -or
        (Test-Path $installDir) -or (Test-Path $registryPath) -or (Test-Path $architectureRegistryPath)) {
        throw 'ARM64 setup failed to reject an incompatible Windows host at initialization'
    }
    Write-Host 'ARM64 setup rejected this host. Native execution requires Windows ARM64.'
    return
}

function Install-Package([string]$Path, [string]$ExpectedVersion, [string]$LogName) {
    Write-Host "Running $LogName"
    $timer = [Diagnostics.Stopwatch]::StartNew()
    $code = Invoke-Package $Path $LogName
    Write-Host ("{0}: {1:F2}s" -f $LogName, $timer.Elapsed.TotalSeconds)
    if ($code -ne 0) { throw "Setup failed: $code" }
    $exe = Join-Path $installDir 'PresetLauncherWinUI3.exe'
    if (-not (Test-Path $exe -PathType Leaf)) { throw 'Installed executable missing' }
    if (Get-Process PresetLauncherWinUI3 -ErrorAction SilentlyContinue | Where-Object Path -eq $exe) {
        throw 'Unattended setup unexpectedly launched PresetLauncher'
    }
    if (-not (Test-Path $shortcut -PathType Leaf)) { throw 'Start menu shortcut missing' }
    if ((Get-ItemProperty -Path $registryPath).DisplayVersion -ne $ExpectedVersion) { throw 'Unexpected installed registry version' }
}

function Assert-CurrentPackage {
    & "$root/Installer/assert-payload.ps1" -Directory $installDir -Machine $entry.machine
    if (-not (Test-Path -LiteralPath $architectureRegistryPath)) {
        throw "Missing installation architecture registration: $Architecture"
    }
    $registered = Get-ItemPropertyValue -LiteralPath $architectureRegistryPath -Name PresetLauncherArchitecture
    if ($registered -cne $Architecture) {
        throw "Incorrect architecture registration: expected $Architecture, found $registered"
    }
    $dll = Join-Path $installDir 'PresetLauncherWinUI3.dll'
    foreach ($file in @($dll, (Join-Path $installDir 'coreclr.dll'), (Join-Path $installDir 'Microsoft.UI.Xaml.dll'))) {
        if (-not (Test-Path $file -PathType Leaf)) { throw "Missing installed file: $file" }
    }
    $runtime = (Get-Content (Join-Path $installDir 'PresetLauncherWinUI3.runtimeconfig.json') -Raw | ConvertFrom-Json).runtimeOptions
    if ($runtime.tfm -ne 'net10.0' -or -not ($runtime.includedFrameworks | Where-Object {
        $_.name -eq 'Microsoft.NETCore.App' -and $_.version -match '^10\.0\.'
    })) { throw 'Expected a bundled .NET 10 runtime' }
    $exe = Join-Path $installDir 'PresetLauncherWinUI3.exe'
    if ((Get-Item $exe).VersionInfo.FileVersion -ne "$version.0") { throw 'Installed executable version does not match installer version' }
    if ((Get-Item $dll).VersionInfo.FileVersion -ne "$version.0") { throw 'Installed assembly version does not match installer version' }
}

# These existing controls are enabled only once the startup and mandatory-update
# gates release input. The edit control also proves the app loaded the fixture.
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
$newPreset = [Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::NameProperty, 'New preset')
$editPreset = [Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::NameProperty, 'Edit Preserve me')
$recovery = [Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::NameProperty,
    'Application data could not be read. The original file was kept. Restore a backup in Settings.')

function Assert-StartupBlockedBySetup {
    $exe = Join-Path $installDir 'PresetLauncherWinUI3.exe'
    $lockPath = Join-Path $installDir '.presetlauncher-install.lock'
    if (-not (Test-Path -LiteralPath $lockPath -PathType Leaf)) { throw 'Setup did not create its installation lock' }
    $exclusive = [IO.File]::Open($lockPath, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::None)
    try {
        $process = Start-Process -FilePath $exe -WorkingDirectory $installDir -PassThru
        try {
            if (-not $process.WaitForExit(15000)) { throw 'Application did not exit while setup held the installation lock' }
            if ($process.ExitCode -ne 1618) { throw "Unexpected blocked startup exit code: $($process.ExitCode)" }
        }
        finally {
            if (-not $process.HasExited) { Stop-Process -InputObject $process -Force }
            $process.WaitForExit()
            $process.Dispose()
        }
    }
    finally { $exclusive.Dispose() }
    Write-Host 'Startup correctly rejected an exclusive installation lock.'
}

function Assert-ApplicationStarts([string]$Phase) {
    $exe = Join-Path $installDir 'PresetLauncherWinUI3.exe'
    Write-Host "Starting application ($Phase)"
    $timer = [Diagnostics.Stopwatch]::StartNew()
    $process = Start-Process -FilePath $exe -WorkingDirectory $installDir -PassThru
    try {
        do {
            $process.Refresh()
            if ($process.HasExited) { throw "Application exited during startup: $($process.ExitCode)" }
            if ($process.MainWindowHandle -ne [IntPtr]::Zero) {
                try {
                    $window = [Windows.Automation.AutomationElement]::FromHandle($process.MainWindowHandle)
                    $scope = [Windows.Automation.TreeScope]::Descendants
                    if ($window.FindFirst($scope, $recovery)) { throw 'Application entered data recovery mode' }
                    $create = $window.FindFirst($scope, $newPreset)
                    $edit = $window.FindFirst($scope, $editPreset)
                    if ($create -and $edit -and $create.Current.IsEnabled -and $edit.Current.IsEnabled -and
                        (Test-Path $policyFile -PathType Leaf) -and (Test-Path $policyLock -PathType Leaf)) {
                        Write-Host ("Startup ({0}): {1:F2}s" -f $Phase, $timer.Elapsed.TotalSeconds)
                        if ($Phase -eq 'fresh') {
                            $before = (Get-FileHash -LiteralPath (Join-Path $installDir 'PresetLauncherWinUI3.dll') -Algorithm SHA256).Hash
                            $code = Invoke-Package $setup 'busy-installation.log' -NoClose
                            $log = Get-Content (Join-Path $logs 'busy-installation.log') -Raw
                            if ($code -ne 7 -or $log -notmatch 'Unable to acquire application installation lock \(Windows error 32\)') {
                                throw "Setup did not reject an active launcher at its installation lock: $code"
                            }
                            if ($process.HasExited -or (Get-FileHash -LiteralPath (Join-Path $installDir 'PresetLauncherWinUI3.dll') -Algorithm SHA256).Hash -ne $before) {
                                throw 'Rejected setup closed the launcher or changed application files'
                            }
                            Write-Host 'Setup correctly rejected an active launcher.'
                        }
                        return
                    }
                }
                catch [Windows.Automation.ElementNotAvailableException] {
                    # WinUI can replace its automation elements during initial layout.
                }
            }
            if ($process.WaitForExit(250)) { throw "Application exited during startup: $($process.ExitCode)" }
        } while ($timer.Elapsed.TotalSeconds -lt 30)
        throw 'Enabled preset controls and protected policy were not available within 30 seconds'
    }
    finally {
        if (-not $process.HasExited) { Stop-Process -InputObject $process -Force }
        $process.WaitForExit()
        $process.Dispose()
    }
}

function Assert-UserData {
    if ((Get-FileHash $dataFile -Algorithm SHA256).Hash -ne $dataHash -or
        (Get-FileHash $settingsFile -Algorithm SHA256).Hash -ne $settingsHash) {
        throw 'Installer modified user data'
    }
    if ($policyHash -and ((Get-FileHash $policyFile -Algorithm SHA256).Hash -ne $policyHash -or
        (Get-FileHash $policyLock -Algorithm SHA256).Hash -ne $policyLockHash)) {
        throw 'Installer modified protected update policy or its initialization marker'
    }
}

function Remove-Package([string]$LogName) {
    $uninstaller = Join-Path $installDir 'unins000.exe'
    $log = Join-Path $logs $LogName
    if (-not (Test-Path $uninstaller)) { throw 'Uninstaller missing' }
    Write-Host "Running $LogName"
    $process = Start-Process -FilePath $uninstaller -ArgumentList ('/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /LOG="{0}"' -f $log) -WindowStyle Hidden -Wait -PassThru
    if ($process.ExitCode -ne 0) { throw "Uninstall failed: $($process.ExitCode)" }
    if ((Test-Path (Join-Path $installDir 'PresetLauncherWinUI3.exe')) -or
        (Test-Path (Join-Path $installDir 'PresetLauncherWinUI3.dll')) -or (Test-Path $registryPath) -or
        (Test-Path $architectureRegistryPath) -or (Test-Path $shortcut)) {
        throw 'Uninstall left application files or registry entry'
    }
    Assert-UserData
}

New-Item -ItemType Directory $dataDir | Out-Null
# DataServiceTests loads these exact files through the production services.
$dataFixture = Join-Path $PSScriptRoot 'fixtures/smoke-data.json'
$settingsFixture = Join-Path $PSScriptRoot 'fixtures/smoke-settings.json'
Copy-Item -LiteralPath $dataFixture -Destination $dataFile
Copy-Item -LiteralPath $settingsFixture -Destination $settingsFile
$dataHash = (Get-FileHash $dataFixture -Algorithm SHA256).Hash
$settingsHash = (Get-FileHash $settingsFixture -Algorithm SHA256).Hash
$policyHash = $null
$policyLockHash = $null
$previousSetup = Join-Path $env:TEMP "PresetLauncher-previous-$([guid]::NewGuid()).exe"
try {
    Install-Package $setup $version 'install.log'
    Assert-CurrentPackage
    Assert-UserData
    Assert-StartupBlockedBySetup
    Assert-ApplicationStarts fresh
    $policyHash = (Get-FileHash $policyFile -Algorithm SHA256).Hash
    $policyLockHash = (Get-FileHash $policyLock -Algorithm SHA256).Hash
    if ($Architecture -in 'x64', 'x86') {
        $other = if ($Architecture -eq 'x64') { 'x86' } else { 'x64' }
        $conflict = Join-Path $root "Installer/Output/PresetLauncher_Setup_${version}$($mapping.$other.suffix).exe"
        if ((Invoke-Package $conflict 'migration-conflict.log') -ne 7) {
            throw 'Expected setup to reject an in-place architecture switch'
        }
        Assert-CurrentPackage
    }
    Remove-Package 'uninstall.log'

    if ($plan.PSObject.Properties['previous_installers'] -and $plan.previous_installers.PSObject.Properties[$Architecture]) {
        $previous = ([string]$plan.previous).TrimStart('v')
        $asset = $plan.previous_installers.$Architecture
        $expectedUrl = "https://github.com/JimMarley420/Preset_launcher/releases/download/$($plan.previous)/PresetLauncher_Setup_${previous}$($entry.suffix).exe"
        if ($previous -notmatch '^\d+\.\d+\.\d+$' -or $asset.url -cne $expectedUrl -or
            $asset.sha256 -notmatch '^[0-9a-f]{64}$' -or $asset.size -le 0 -or $asset.size -gt 1GB) {
            throw 'Invalid previous installer plan'
        }
        Write-Host "Downloading verified baseline installer $previous"
        Invoke-WebRequest -Uri $expectedUrl -UseBasicParsing -OutFile $previousSetup -TimeoutSec 300
        if ((Get-Item $previousSetup).Length -ne $asset.size -or
            (Get-FileHash $previousSetup -Algorithm SHA256).Hash -ine $asset.sha256) {
            throw 'Previous installer verification failed'
        }
        Install-Package $previousSetup $previous 'upgrade-from.log'
        Assert-UserData
        Install-Package $setup $version 'upgrade-to.log'
        Assert-CurrentPackage
        Assert-UserData
        Assert-ApplicationStarts upgrade
        Remove-Package 'upgrade-uninstall.log'
    }
    Write-Host 'Installer smoke test passed.'
}
finally {
    if (Test-Path $previousSetup) { Remove-Item $previousSetup -Force }
    $resolved = (Resolve-Path -LiteralPath $dataDir).Path
    if ($resolved -ne (Join-Path (Resolve-Path -LiteralPath $env:LOCALAPPDATA).Path 'PresetLauncher') -or
        (Get-Item -LiteralPath $dataDir).Attributes.HasFlag([IO.FileAttributes]::ReparsePoint)) {
        throw 'Unsafe smoke data directory'
    }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
