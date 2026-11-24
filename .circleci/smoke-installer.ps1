# Run only on an isolated Windows CI VM, never on a developer's installed copy.
param([Parameter(Mandatory = $true)][ValidatePattern('^\d+\.\d+\.\d+
Set-StrictMode -Version Latest

$root = Split-Path $PSScriptRoot -Parent
$setup = Join-Path $root "Installer\Output\PresetLauncher_Setup_$version.exe"
$logs = Join-Path $root 'artifacts\installer-logs'
New-Item -ItemType Directory -Force $logs | Out-Null
$installDir = Join-Path $env:TEMP "PresetLauncher-CI-$([guid]::NewGuid())"
$dataDir = Join-Path $env:LOCALAPPDATA 'PresetLauncher'
$marker = Join-Path $dataDir 'ci-preservation.txt'
$uninstallKey = 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{C4A7B8D2-E3F5-4901-A2B3-C4D5E6F70819}_is1'
$registryPath = "HKCU:\$uninstallKey"

if ($env:CIRCLECI -ne 'true') { throw 'Smoke test requires an isolated CircleCI VM' }
if ((Test-Path $dataDir) -or (Test-Path $registryPath) -or (Test-Path "HKLM:\$uninstallKey") -or (Test-Path "$env:LOCALAPPDATA\Programs\PresetLauncher")) {
    throw 'Existing PresetLauncher installation or data; refusing to run smoke test'
}
if (-not (Test-Path $setup -PathType Leaf)) { throw "Installer not found: $setup" }

New-Item -ItemType Directory $dataDir | Out-Null
$token = [guid]::NewGuid().ToString()
Set-Content -Path $marker -Value $token -Encoding ASCII -NoNewline
try {
    $install = Start-Process -FilePath $setup -ArgumentList "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CURRENTUSER /DIR=`"$installDir`" /TASKS=`"`" /LOG=`"$logs\install.log`"" -Wait -PassThru
    if ($install.ExitCode -ne 0) { throw "Setup failed: $($install.ExitCode)" }
    $exe = Join-Path $installDir 'PresetLauncherWinUI3.exe'
    $dll = Join-Path $installDir 'PresetLauncherWinUI3.dll'
    foreach ($file in @($exe, $dll, (Join-Path $installDir 'coreclr.dll'), (Join-Path $installDir 'Microsoft.UI.Xaml.dll'))) {
        if (-not (Test-Path $file -PathType Leaf)) { throw "Missing installed file: $file" }
    }
    if ((Get-Item $dll).VersionInfo.FileVersion -ne "$version.0") { throw 'Installed assembly version does not match installer version' }
    if ((Get-ItemProperty -Path $registryPath).DisplayVersion -ne $version) { throw 'Unexpected installed registry version' }
    $uninstaller = Join-Path $installDir 'unins000.exe'
    if (-not (Test-Path $uninstaller)) { throw 'Uninstaller missing' }
    $remove = Start-Process -FilePath $uninstaller -ArgumentList "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /LOG=`"$logs\uninstall.log`"" -Wait -PassThru
    if ($remove.ExitCode -ne 0) { throw "Uninstall failed: $($remove.ExitCode)" }
    if ((Test-Path $exe) -or (Test-Path $dll) -or (Test-Path $registryPath)) { throw 'Uninstall left application files or registry entry' }
    if (-not (Test-Path $marker) -or (Get-Content $marker -Raw) -ne $token) { throw 'Uninstall modified user data' }
    Write-Host 'Installer smoke test passed.'
}
finally {
    if (Test-Path $marker) { Remove-Item $marker -Force }
    if ((Test-Path $dataDir) -and -not (Get-ChildItem $dataDir -Force)) { Remove-Item $dataDir }
}
)][string]$Version)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = Split-Path $PSScriptRoot -Parent
$version = (Select-String -Path "$root\Installer\PresetLauncher.iss" -Pattern '#define AppVersion "([^"]+)"').Matches[0].Groups[1].Value
$setup = Join-Path $root "Installer\Output\PresetLauncher_Setup_$version.exe"
$logs = Join-Path $root 'artifacts\installer-logs'
New-Item -ItemType Directory -Force $logs | Out-Null
$installDir = Join-Path $env:TEMP "PresetLauncher-CI-$([guid]::NewGuid())"
$dataDir = Join-Path $env:LOCALAPPDATA 'PresetLauncher'
$marker = Join-Path $dataDir 'ci-preservation.txt'
$uninstallKey = 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{C4A7B8D2-E3F5-4901-A2B3-C4D5E6F70819}_is1'
$registryPath = "HKCU:\$uninstallKey"

if ($env:CIRCLECI -ne 'true') { throw 'Smoke test requires an isolated CircleCI VM' }
if ((Test-Path $dataDir) -or (Test-Path $registryPath) -or (Test-Path "HKLM:\$uninstallKey") -or (Test-Path "$env:LOCALAPPDATA\Programs\PresetLauncher")) {
    throw 'Existing PresetLauncher installation or data; refusing to run smoke test'
}
if (-not (Test-Path $setup -PathType Leaf)) { throw "Installer not found: $setup" }

New-Item -ItemType Directory $dataDir | Out-Null
$token = [guid]::NewGuid().ToString()
Set-Content -Path $marker -Value $token -Encoding ASCII -NoNewline
try {
    $install = Start-Process -FilePath $setup -ArgumentList "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CURRENTUSER /DIR=`"$installDir`" /TASKS=`"`" /LOG=`"$logs\install.log`"" -Wait -PassThru
    if ($install.ExitCode -ne 0) { throw "Setup failed: $($install.ExitCode)" }
    $exe = Join-Path $installDir 'PresetLauncherWinUI3.exe'
    $dll = Join-Path $installDir 'PresetLauncherWinUI3.dll'
    foreach ($file in @($exe, $dll, (Join-Path $installDir 'coreclr.dll'), (Join-Path $installDir 'Microsoft.UI.Xaml.dll'))) {
        if (-not (Test-Path $file -PathType Leaf)) { throw "Missing installed file: $file" }
    }
    if ((Get-Item $dll).VersionInfo.FileVersion -ne "$version.0") { throw 'Installed assembly version does not match installer version' }
    if ((Get-ItemProperty -Path $registryPath).DisplayVersion -ne $version) { throw 'Unexpected installed registry version' }
    $uninstaller = Join-Path $installDir 'unins000.exe'
    if (-not (Test-Path $uninstaller)) { throw 'Uninstaller missing' }
    $remove = Start-Process -FilePath $uninstaller -ArgumentList "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /LOG=`"$logs\uninstall.log`"" -Wait -PassThru
    if ($remove.ExitCode -ne 0) { throw "Uninstall failed: $($remove.ExitCode)" }
    if ((Test-Path $exe) -or (Test-Path $dll) -or (Test-Path $registryPath)) { throw 'Uninstall left application files or registry entry' }
    if (-not (Test-Path $marker) -or (Get-Content $marker -Raw) -ne $token) { throw 'Uninstall modified user data' }
    Write-Host 'Installer smoke test passed.'
}
finally {
    if (Test-Path $marker) { Remove-Item $marker -Force }
    if ((Test-Path $dataDir) -and -not (Get-ChildItem $dataDir -Force)) { Remove-Item $dataDir }
}
