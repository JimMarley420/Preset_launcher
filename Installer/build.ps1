param(
    [ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version = '0.0.0',
    [ValidateSet('x64', 'x86', 'arm64', 'all')][string]$Architecture = 'x64',
    [switch]$PublishOnly,
    [switch]$UsePinnedCompiler
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$mapping = Get-Content (Join-Path $PSScriptRoot architectures.json) -Raw | ConvertFrom-Json
$targets = if ($Architecture -eq 'all') { $mapping.PSObject.Properties.Name } else { $Architecture.ToLowerInvariant() }
$temp = $null

if (-not $PublishOnly) {
    if ($UsePinnedCompiler) {
        $temp = Join-Path $env:TEMP "presetlauncher-inno-$([guid]::NewGuid())"
        New-Item -ItemType Directory $temp | Out-Null
        $download = Join-Path $temp 'innosetup-6.7.1.exe'
        $targetCompiler = Join-Path $temp 'compiler'
        $compiler = Join-Path $targetCompiler 'ISCC.exe'
    } else {
        $found = Get-Command ISCC.exe -ErrorAction SilentlyContinue
        $pathCompiler = if ($found) { $found.Source }
        $compiler = @("${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
            "$env:ProgramFiles\Inno Setup 6\ISCC.exe", $pathCompiler) |
            Where-Object { $_ -and (Test-Path -LiteralPath $_ -PathType Leaf) } | Select-Object -First 1
        if (-not $compiler) { throw 'Install Inno Setup 6 or use -UsePinnedCompiler.' }
    }
}

Push-Location $root
try {
    if ($UsePinnedCompiler -and -not $PublishOnly) {
        $timer = [Diagnostics.Stopwatch]::StartNew()
        curl.exe -fsSL --connect-timeout 20 --max-time 90 --retry 2 --retry-max-time 120 https://github.com/jrsoftware/issrc/releases/download/is-6_7_1/innosetup-6.7.1.exe -o $download
        if ($LASTEXITCODE -ne 0 -or (Get-FileHash -LiteralPath $download -Algorithm SHA256).Hash -ine
            '4d11e8050b6185e0d49bd9e8cc661a7a59f44959a621d31d11033124c4e8a7b0') { throw 'Pinned Inno Setup download verification failed' }
        $process = Start-Process $download -ArgumentList ('/PORTABLE=1 /VERYSILENT /SUPPRESSMSGBOXES /CURRENTUSER /NORESTART /DIR="{0}"' -f $targetCompiler) -WindowStyle Hidden -Wait -PassThru
        if ($process.ExitCode -ne 0 -or -not (Test-Path $compiler -PathType Leaf)) { throw 'Pinned Inno Setup installation failed' }
        Write-Host "Inno Setup preparation: $($timer.Elapsed)"
    }
    $output = Join-Path $PSScriptRoot Output
    $project = 'PresetLauncherWinUI3/PresetLauncherWinUI3.csproj'
    foreach ($target in $targets) {
        $entry = $mapping.$target
        $publishRoot = Join-Path $root publish
        $publish = Join-Path $publishRoot $entry.rid
        if (Test-Path -LiteralPath $publish) {
            $resolved = (Resolve-Path -LiteralPath $publish).Path
            if ($resolved -ne $publish -or (Get-Item -LiteralPath $publishRoot).Attributes.HasFlag([IO.FileAttributes]::ReparsePoint) -or
                (Get-Item -LiteralPath $publish).Attributes.HasFlag([IO.FileAttributes]::ReparsePoint)) { throw "Unsafe publish directory: $publish" }
            Remove-Item -LiteralPath $resolved -Recurse -Force
        }
        $properties = @("-p:Platform=$($entry.platform)", "-p:RuntimeIdentifiers=$($entry.rid)", "-p:Version=$Version",
            '-p:WindowsPackageType=None', '-p:PublishTrimmed=false', '-p:PublishReadyToRun=false',
            '-p:WarningsAsErrors=NU1900%3BNU1903%3BNU1904%3BNU1905')
        # Artifacts layout isolates the restore graph as well as intermediate binaries.
        $work = Join-Path $root "artifacts/publish-$target"
        $timer = [Diagnostics.Stopwatch]::StartNew()
        dotnet restore $project --runtime $entry.rid -p:Configuration=Release "-p:ArtifactsPath=$work" -p:SelfContained=true @properties
        if ($LASTEXITCODE -ne 0) { throw "Restore failed: $target" }
        Write-Host "$target restore: $($timer.Elapsed)"
        $timer.Restart()
        dotnet publish $project -c Release -r $entry.rid --self-contained true --no-restore --artifacts-path $work --output $publish @properties
        if ($LASTEXITCODE -ne 0) { throw "Publish failed: $target" }
        & "$PSScriptRoot/assert-payload.ps1" -Directory $publish -Machine $entry.machine
        Write-Host "$target publish and PE verification: $($timer.Elapsed)"
        if ($PublishOnly) { continue }

        $timer.Restart()
        New-Item -ItemType Directory -Force $output | Out-Null
        $name = "PresetLauncher_Setup_${Version}$($entry.suffix)"
        $pin = if ($UsePinnedCompiler) { @('/DRequirePinnedCompiler') } else { @() }
        & $compiler @pin "/DAppVersion=$Version" "/DTargetArchitecture=$target" `
            "/DPayloadRid=$($entry.rid)" "/DSetupName=$name" "/DAllowedArchitecture=$($entry.allowed)" `
            "/DMode64Architecture=$($entry.mode64)" "/O$output" (Join-Path $PSScriptRoot PresetLauncher.iss)
        if ($LASTEXITCODE -ne 0 -or -not (Test-Path (Join-Path $output "$name.exe") -PathType Leaf)) {
            throw "Installer compilation failed: $target"
        }
        Write-Host "$target installer compression: $($timer.Elapsed)"
    }
}
finally {
    Pop-Location
    if ($temp -and (Test-Path -LiteralPath $temp)) {
        $resolved = (Resolve-Path -LiteralPath $temp).Path
        if ($resolved -eq $temp -and (Split-Path $resolved -Parent) -eq (Resolve-Path -LiteralPath $env:TEMP).Path -and
            -not (Get-Item -LiteralPath $resolved).Attributes.HasFlag([IO.FileAttributes]::ReparsePoint)) {
            Remove-Item -LiteralPath $resolved -Recurse -Force
        }
    }
}
