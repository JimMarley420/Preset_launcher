$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$planPath = 'artifacts/release-plan.json'
$plan = Get-Content $planPath -Raw | ConvertFrom-Json
if ($plan.sha -cne $env:CIRCLE_SHA1 -or $plan.validate_installer -isnot [bool] -or
    ($env:CIRCLE_BRANCH -eq 'main' -and -not $plan.validate_installer)) { throw 'Invalid Windows validation plan' }
if ([Runtime.InteropServices.RuntimeInformation]::OSArchitecture -ne 'X64') { throw 'Expected an x64 validation host' }

$output = 'Installer/Output'
New-Item -ItemType Directory -Force $output | Out-Null
$evidence = [ordered]@{
    sha = $plan.sha; version = $plan.version; full = $plan.validate_installer
    audit = $true; test = $true; built = @(); smoke = [ordered]@{}; installers = [ordered]@{}
}
if ($plan.validate_installer) {
    $mapping = Get-Content Installer/architectures.json -Raw | ConvertFrom-Json
    & ./Installer/build.ps1 -Architecture all -Version $plan.version -UsePinnedCompiler
    $sums = foreach ($entry in $mapping.PSObject.Properties) {
        $target = $entry.Name
        $name = "PresetLauncher_Setup_$($plan.version)$($entry.Value.suffix).exe"
        $file = Get-Item -LiteralPath (Join-Path $output $name)
        $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        $evidence.installers[$target] = @{ size = $file.Length; sha256 = $hash }
        "$hash  $name"
    }
    $sums | Set-Content (Join-Path $output 'SHA256SUMS.txt') -Encoding ASCII
    foreach ($target in $mapping.PSObject.Properties.Name) {
        & "$PSScriptRoot/smoke-installer.ps1" -Architecture $target -PlanPath $planPath
        $evidence.built += $target
        $evidence.smoke[$target] = switch ($target) {
            x64 { 'native' }
            x86 { 'wow64' }
            arm64 { 'incompatible-host-rejected' }
        }
    }
} else {
    # The regression job already compiled the x64 application via its project reference.
    $evidence.built += 'x64'
    New-Item -ItemType File (Join-Path $output '.fast-lane') | Out-Null
}
if ($plan.validate_installer) {
    $bytes = (Get-ChildItem -LiteralPath $output -File | Measure-Object Length -Sum).Sum
    Write-Host "Installer workspace payload: $bytes bytes"
}
$evidence | ConvertTo-Json -Depth 5 | Set-Content artifacts/windows-validation.json -Encoding UTF8
