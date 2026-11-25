$ErrorActionPreference = 'Stop'
$version = (Get-Content global.json -Raw | ConvertFrom-Json).sdk.version
$installDir = Join-Path $env:ProgramFiles 'dotnet'
$dotnet = Join-Path $installDir 'dotnet.exe'
if ($version -notmatch '^\d+\.\d+\.\d+$' -or (Get-Command dotnet -CommandType Application).Source -ine $dotnet) {
    throw 'Expected an exact stable SDK pin and the Windows x64 dotnet host on PATH'
}

$cache = New-Item -ItemType Directory -Force artifacts/sdk-cache
$archive = Join-Path $cache.FullName 'sdk.zip'
$installed = & $dotnet --list-sdks
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
if (-not ($installed | Where-Object { $_ -match "^$([regex]::Escape($version)) " })) {
    # The expected digest is Microsoft's win-x64 SDK ZIP SHA-512 from the .NET release metadata.
    $expected = (Get-Content .circleci/dotnet-sdk.sha512 -Raw).Trim()
    if ($expected -notmatch '^[0-9a-f]{128}$') { throw 'Invalid SDK archive digest' }
    if (Test-Path -LiteralPath $archive -PathType Leaf) {
        Write-Host "Using cached SDK $version archive"
    } else {
        Write-Host "Downloading SDK $version from Microsoft"
        curl.exe -fsSL --connect-timeout 20 --max-time 120 --retry 2 --retry-max-time 180 "https://builds.dotnet.microsoft.com/dotnet/Sdk/$version/dotnet-sdk-$version-win-x64.zip" -o $archive
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }
    if ((Get-FileHash -LiteralPath $archive -Algorithm SHA512).Hash -ine $expected) {
        throw 'SDK archive SHA-512 mismatch'
    }
    tar.exe -xf $archive -C $installDir
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
$actual = & $dotnet --version
if ($LASTEXITCODE -ne 0 -or $actual -ne $version) { throw "Expected SDK $version, got $actual" }
& $dotnet --info
exit $LASTEXITCODE
