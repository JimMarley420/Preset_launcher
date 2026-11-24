# Run only on an isolated Windows CI VM, never on a developer's installed copy.
param([Parameter(Mandatory = $true)][string]$PlanPath)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = Split-Path $PSScriptRoot -Parent
$plan = Get-Content $PlanPath -Raw | ConvertFrom-Json
$version = [string]$plan.version
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid planned version' }
$setup = Join-Path $root "Installer\Output\PresetLauncher_Setup_$version.exe"
$logs = Join-Path $root 'artifacts\installer-logs'
New-Item -ItemType Directory -Force $logs | Out-Null
$installDir = Join-Path $env:TEMP "PresetLauncher-CI-$([guid]::NewGuid())"
$dataDir = Join-Path $env:LOCALAPPDATA 'PresetLauncher'
$dataFile = Join-Path $dataDir 'data.json'
$settingsFile = Join-Path $dataDir 'settings.json'
$uninstallKey = 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{C4A7B8D2-E3F5-4901-A2B3-C4D5E6F70819}_is1'
$registryPath = "HKCU:\$uninstallKey"

if ($env:CIRCLECI -ne 'true') { throw 'Smoke test requires an isolated CircleCI VM' }
if ((Test-Path $dataDir) -or (Test-Path $registryPath) -or (Test-Path "HKLM:\$uninstallKey") -or (Test-Path "$env:LOCALAPPDATA\Programs\PresetLauncher")) {
    throw 'Existing PresetLauncher installation or data; refusing to run smoke test'
}
if (-not (Test-Path $setup -PathType Leaf)) { throw "Installer not found: $setup" }

function Install-Package([string]$Path, [string]$ExpectedVersion, [string]$LogName) {
    $log = Join-Path $logs $LogName
    Write-Host "Running $LogName"
    $timer = [Diagnostics.Stopwatch]::StartNew()
    $process = Start-Process -FilePath $Path -ArgumentList ('/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CURRENTUSER /DIR="{0}" /TASKS="" /LOG="{1}"' -f $installDir, $log) -WindowStyle Hidden -Wait -PassThru
    Write-Host ("{0}: {1:F2}s" -f $LogName, $timer.Elapsed.TotalSeconds)
    if ($process.ExitCode -ne 0) { throw "Setup failed: $($process.ExitCode)" }
    if (-not (Test-Path (Join-Path $installDir 'PresetLauncherWinUI3.exe') -PathType Leaf)) { throw 'Installed executable missing' }
    if ((Get-ItemProperty -Path $registryPath).DisplayVersion -ne $ExpectedVersion) { throw 'Unexpected installed registry version' }
}

function Assert-CurrentPackage {
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
            if ($process.MainWindowHandle -ne [IntPtr]::Zero) {
                Write-Host ("Startup ({0}): {1:F2}s" -f $Phase, $timer.Elapsed.TotalSeconds)
                return
            }
            Start-Sleep -Milliseconds 250
        } while ($timer.Elapsed.TotalSeconds -lt 30)
        throw 'Application main window did not initialize within 30 seconds'
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
        (Test-Path (Join-Path $installDir 'PresetLauncherWinUI3.dll')) -or (Test-Path $registryPath)) {
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
    Remove-Package 'uninstall.log'

    if ($plan.PSObject.Properties['previous_installer']) {
        $previous = ([string]$plan.previous).TrimStart('v')
        $asset = $plan.previous_installer
        $expectedUrl = "https://github.com/JimMarley420/Preset_launcher/releases/download/$($plan.previous)/PresetLauncher_Setup_$previous.exe"
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
    if (Test-Path $dataFile) { Remove-Item $dataFile -Force }
    if (Test-Path $settingsFile) { Remove-Item $settingsFile -Force }
    if ((Test-Path $dataDir) -and -not (Get-ChildItem $dataDir -Force)) { Remove-Item $dataDir }
}
