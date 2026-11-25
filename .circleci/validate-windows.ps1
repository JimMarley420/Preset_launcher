param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('PrepareCompiler', 'Publish', 'Compile', 'Smoke', 'Evidence')][string]$Phase,
    [ValidateSet('x64', 'x86', 'arm64')][string]$Architecture
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$planPath = 'artifacts/release-plan.json'
$plan = Get-Content $planPath -Raw | ConvertFrom-Json
if ($plan.sha -cne $env:CIRCLE_SHA1 -or $plan.version -notmatch '^\d+\.\d+\.\d+$' -or
    $plan.validate_installer -isnot [bool] -or
    ($env:CIRCLE_BRANCH -eq 'main' -and -not $plan.validate_installer)) { throw 'Invalid Windows validation plan' }
if ([Runtime.InteropServices.RuntimeInformation]::OSArchitecture -ne 'X64') { throw 'Expected an x64 validation host' }
if (($Phase -in 'Publish', 'Compile', 'Smoke') -and -not $Architecture) { throw "$Phase requires an architecture" }

$output = 'Installer/Output'
if (-not $plan.validate_installer -and $Phase -ne 'Evidence') {
    Write-Host ("SKIPPED (fast lane): $Phase $Architecture".TrimEnd())
    return
}

$mapping = Get-Content Installer/architectures.json -Raw | ConvertFrom-Json
$compilerDirectory = Join-Path (Get-Location).Path 'artifacts/inno-compiler'
$phaseEvidence = 'artifacts/windows-phases'
if ($Phase -eq 'PrepareCompiler') {
    & ./Installer/build.ps1 -Phase PrepareCompiler -UsePinnedCompiler -CompilerDirectory $compilerDirectory
    return
}
if ($Phase -eq 'Evidence') {
    $evidence = [ordered]@{
        sha = $plan.sha; version = $plan.version; full = $plan.validate_installer
        audit = $true; test = $true; built = @(); smoke = [ordered]@{}; installers = [ordered]@{}
    }
    if ($plan.validate_installer) {
        $sums = foreach ($entry in $mapping.PSObject.Properties) {
            $target = $entry.Name
            $result = Get-Content "$phaseEvidence/$target.json" -Raw | ConvertFrom-Json
            if (-not $result.smoke) { throw "Missing smoke evidence for $target" }
            $evidence.built += $target
            $evidence.smoke[$target] = $result.smoke
            $evidence.installers[$target] = $result.installer
            "$($result.installer.sha256)  PresetLauncher_Setup_$($plan.version)$($entry.Value.suffix).exe"
        }
        $sums | Set-Content (Join-Path $output 'SHA256SUMS.txt') -Encoding ASCII
        $bytes = (Get-ChildItem -LiteralPath $output -File | Measure-Object Length -Sum).Sum
        Write-Host "Installer workspace payload: $bytes bytes"
    } else {
        # The regression step already compiled the x64 application via its project reference.
        $evidence.built += 'x64'
        New-Item -ItemType Directory -Force $output | Out-Null
        New-Item -ItemType File (Join-Path $output '.fast-lane') | Out-Null
    }
    $evidence | ConvertTo-Json -Depth 5 | Set-Content artifacts/windows-validation.json -Encoding UTF8
    return
}

$logs = "artifacts/installer-logs/$Architecture"
$resultPath = "$phaseEvidence/$Architecture.json"
New-Item -ItemType Directory -Force $logs | Out-Null
Start-Transcript -Path "$logs/$Phase.log" | Out-Null
try {
    switch ($Phase) {
        Publish {
            & ./Installer/build.ps1 -Architecture $Architecture -Version $plan.version -PublishOnly | Out-Host
        }
        Compile {
            & ./Installer/build.ps1 -Architecture $Architecture -Version $plan.version -Phase Compile `
                -UsePinnedCompiler -CompilerDirectory $compilerDirectory | Out-Host
            $name = "PresetLauncher_Setup_$($plan.version)$($mapping.$Architecture.suffix).exe"
            $file = Get-Item -LiteralPath (Join-Path $output $name)
            $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
            New-Item -ItemType Directory -Force $phaseEvidence | Out-Null
            @{ installer = @{ size = $file.Length; sha256 = $hash }; smoke = $null } |
                ConvertTo-Json | Set-Content $resultPath -Encoding UTF8
        }
        Smoke {
            $result = Get-Content $resultPath -Raw | ConvertFrom-Json
            & "$PSScriptRoot/smoke-installer.ps1" -Architecture $Architecture -PlanPath $planPath
            $result.smoke = switch ($Architecture) {
                x64 { 'native' }
                x86 { 'wow64' }
                arm64 { 'incompatible-host-rejected' }
            }
            $result | ConvertTo-Json | Set-Content $resultPath -Encoding UTF8
        }
    }
}
catch {
    Write-Host ($_ | Out-String)
    throw
}
finally {
    Stop-Transcript | Out-Null
}
