param([switch]$SdkOnly)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
if ($SdkOnly) {
    $archive = 'artifacts/sdk-benchmark/sdk.zip'
    $hash = '330ddd2665b43cd2e428216a0d8ea1ac7bb19b5434fdc530e62010dc3b52823a620bf95489d628e1ed6f5122401793ee4887cca4a019122361a1231497f632ee'
    $watch = [Diagnostics.Stopwatch]::StartNew()
    if ((Get-FileHash $archive -Algorithm SHA512).Hash -ine $hash) { throw 'SDK integrity check failed' }
    $destination = Join-Path $env:TEMP "review-sdk-$([guid]::NewGuid())"
    New-Item -ItemType Directory $destination | Out-Null
    tar.exe -xf $archive -C $destination
    if ($LASTEXITCODE) { exit $LASTEXITCODE }
    $sdk = & "$destination/dotnet.exe" --version
    if ($LASTEXITCODE -or $sdk -ne '10.0.404') { throw 'Wrong restored SDK' }
    Write-Host "SDK verify/extract/version seconds: $($watch.Elapsed.TotalSeconds)"
    return
}
$env:NUGET_HTTP_CACHE_PATH = Join-Path $env:TEMP 'review-nuget-http'
$env:NUGET_PACKAGES = Join-Path $env:TEMP 'review-nuget-cold'
$watch = [Diagnostics.Stopwatch]::StartNew()
dotnet restore tests/PresetLauncher.Tests/PresetLauncher.Tests.csproj -r win-x64 -p:Platform=x64 -p:Configuration=Release --no-http-cache --force
if ($LASTEXITCODE) { exit $LASTEXITCODE }
Write-Host "Cold audited NuGet restore seconds: $($watch.Elapsed.TotalSeconds)"
Remove-Item Env:NUGET_PACKAGES
if (Test-Path "$env:USERPROFILE/.nuget/packages") {
    $watch.Restart()
    dotnet restore tests/PresetLauncher.Tests/PresetLauncher.Tests.csproj -r win-x64 -p:Platform=x64 -p:Configuration=Release --no-http-cache --force
    if ($LASTEXITCODE) { exit $LASTEXITCODE }
    Write-Host "Audited NuGet restore after cache attachment seconds: $($watch.Elapsed.TotalSeconds)"
}
$iscc = Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6/ISCC.exe'
if (-not (Test-Path $iscc)) { $iscc = Join-Path $env:ProgramFiles 'Inno Setup 6/ISCC.exe' }
$plan = Get-Content artifacts/release-plan.json -Raw | ConvertFrom-Json
$version = $plan.version
$iss = [IO.File]::ReadAllText((Resolve-Path Installer/PresetLauncher.iss))
$root = (Get-Location).Path
$output = Join-Path $root 'Installer/Output'
New-Item -ItemType Directory -Force $output, artifacts/installer-benchmark | Out-Null
foreach ($r2r in 'true','false') {
    $watch.Restart()
    dotnet publish PresetLauncherWinUI3/PresetLauncherWinUI3.csproj -c Release -r win-x64 --self-contained true -p:Platform=x64 -p:PublishTrimmed=false "-p:PublishReadyToRun=$r2r" "-p:Version=$version" --output publish/win-x64
    if ($LASTEXITCODE) { exit $LASTEXITCODE }
    Write-Host ("R2R {0}: publish {1:F2}s, {2} bytes" -f $r2r,$watch.Elapsed.TotalSeconds,((Get-ChildItem publish/win-x64 -Recurse -File | Measure-Object Length -Sum).Sum))
    $levels = if ($r2r -eq 'true') { @('ultra64','max','fast') } else { @('max') }
    foreach ($level in $levels) {
        $scenario = "$r2r-$level"
        $path = Join-Path $root 'Installer/review-benchmark.iss'
        [IO.File]::WriteAllText($path,$iss.Replace('Compression=lzma2/ultra64',"Compression=lzma2/$level"))
        $watch.Restart()
        & $iscc /Q /DRequirePinnedCompiler "/DAppVersion=$version" "/O$output" $path
        if ($LASTEXITCODE) { exit $LASTEXITCODE }
        $setup = Join-Path $output "PresetLauncher_Setup_$version.exe"
        Write-Host ("Variant {0}: compile {1:F2}s, {2} bytes" -f $scenario,$watch.Elapsed.TotalSeconds,(Get-Item $setup).Length)
        & .circleci/smoke-installer.ps1 -PlanPath artifacts/release-plan.json
        Move-Item -LiteralPath artifacts/installer-logs -Destination "artifacts/installer-benchmark/$scenario"
    }
    Move-Item -LiteralPath publish/win-x64 -Destination "publish/bench-$r2r"
}
