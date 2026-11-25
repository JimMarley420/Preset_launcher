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
$shortcut = Join-Path ([Environment]::GetFolderPath('Programs')) 'PresetLauncher/PresetLauncher.lnk'
$uninstallKey = 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{C4A7B8D2-E3F5-4901-A2B3-C4D5E6F70819}_is1'
$registryPath = "HKCU:\$uninstallKey"

if ($env:CIRCLECI -ne 'true') { throw 'Smoke test requires an isolated CircleCI VM' }
$machineKeys = [Microsoft.Win32.RegistryView]::Registry32, [Microsoft.Win32.RegistryView]::Registry64 |
    ForEach-Object {
        $hive = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::LocalMachine, $_)
        try {
            $key = $hive.OpenSubKey($uninstallKey)
            if ($key) { $key.Dispose(); $true }
        }
        finally { $hive.Dispose() }
    }
if ((Test-Path $dataDir) -or (Test-Path $registryPath) -or $machineKeys -or (Test-Path $shortcut) -or
    (Test-Path "$env:LOCALAPPDATA\Programs\PresetLauncher")) {
    throw 'Existing PresetLauncher installation or data; refusing to run smoke test'
}
if (-not (Test-Path $setup -PathType Leaf)) { throw "Installer not found: $setup" }

function Invoke-Package([string]$Path, [string]$LogName) {
    $log = Join-Path $logs $LogName
    $process = Start-Process -FilePath $Path -ArgumentList ('/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CURRENTUSER /DIR="{0}" /TASKS="" /LOG="{1}"' -f $installDir, $log) -WindowStyle Hidden -Wait -PassThru
    try { return $process.ExitCode }
    finally { $process.Dispose() }
}

if ($Architecture -eq 'arm64' -and [Runtime.InteropServices.RuntimeInformation]::OSArchitecture -ne 'Arm64') {
    if ((Invoke-Package $setup 'incompatible.log') -ne 1 -or (Test-Path $installDir) -or (Test-Path $registryPath)) {
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
    if (-not (Test-Path (Join-Path $installDir 'PresetLauncherWinUI3.exe') -PathType Leaf)) { throw 'Installed executable missing' }
    if (-not (Test-Path $shortcut -PathType Leaf)) { throw 'Start menu shortcut missing' }
    if ((Get-ItemProperty -Path $registryPath).DisplayVersion -ne $ExpectedVersion) { throw 'Unexpected installed registry version' }
}

function Assert-CurrentPackage {
    & "$root/Installer/assert-payload.ps1" -Directory $installDir -Machine $entry.machine
    if ((Get-ItemProperty -Path $registryPath).PresetLauncherArchitecture -cne $Architecture) {
        throw 'Incorrect architecture registration'
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

function Assert-ApplicationStarts([string]$Phase) {
    $exe = Join-Path $installDir 'PresetLauncherWinUI3.exe'
    Write-Host "Starting application ($Phase)"
    $timer = [Diagnostics.Stopwatch]::StartNew()
    $process = Start-Process -FilePath $exe -WorkingDirectory $installDir -PassThru
    try {
        do {
            $process.Refresh()
            if ($process.HasExited) { throw "Application exited during startup: $($process.ExitCode)" }
            if ($process.MainWindowHandle -ne [IntPtr]::Zero -and (Test-Path $policyFile -PathType Leaf)) {
                Write-Host ("Startup ({0}): {1:F2}s" -f $Phase, $timer.Elapsed.TotalSeconds)
                return
            }
            Start-Sleep -Milliseconds 250
        } while ($timer.Elapsed.TotalSeconds -lt 30)
        throw 'Application window or protected policy store did not initialize within 30 seconds'
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
}

function Remove-Package([string]$LogName) {
    $uninstaller = Join-Path $installDir 'unins000.exe'
    $log = Join-Path $logs $LogName
    if (-not (Test-Path $uninstaller)) { throw 'Uninstaller missing' }
    Write-Host "Running $LogName"
    $process = Start-Process -FilePath $uninstaller -ArgumentList ('/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /LOG="{0}"' -f $log) -WindowStyle Hidden -Wait -PassThru
    if ($process.ExitCode -ne 0) { throw "Uninstall failed: $($process.ExitCode)" }
    if ((Test-Path (Join-Path $installDir 'PresetLauncherWinUI3.exe')) -or
        (Test-Path (Join-Path $installDir 'PresetLauncherWinUI3.dll')) -or (Test-Path $registryPath) -or (Test-Path $shortcut)) {
        throw 'Uninstall left application files or registry entry'
    }
    Assert-UserData
}

New-Item -ItemType Directory $dataDir | Out-Null
'{"Presets":[{"Id":"ci-fixture","Name":"Preserve me","Executables":[]}],"History":[],"UsageStats":{}}' | Set-Content $dataFile -Encoding UTF8
'{"Language":"en","Theme":"dark"}' | Set-Content $settingsFile -Encoding UTF8
$dataHash = (Get-FileHash $dataFile -Algorithm SHA256).Hash
$settingsHash = (Get-FileHash $settingsFile -Algorithm SHA256).Hash
$previousSetup = Join-Path $env:TEMP "PresetLauncher-previous-$([guid]::NewGuid()).exe"
try {
    Install-Package $setup $version 'install.log'
    Assert-CurrentPackage
    Assert-UserData
    Assert-ApplicationStarts fresh
    if ($Architecture -eq 'x64') {
        $conflict = Join-Path $root "Installer/Output/PresetLauncher_Setup_${version}$($mapping.x86.suffix).exe"
        if ((Invoke-Package $conflict 'migration-conflict.log') -ne 7) {
            throw 'Expected setup to reject an in-place architecture switch'
        }
        Assert-CurrentPackage
    }
    $policyHash = (Get-FileHash $policyFile -Algorithm SHA256).Hash
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
    if ((Get-FileHash $policyFile -Algorithm SHA256).Hash -ne $policyHash) { throw 'Protected policy did not survive installation changes' }
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
