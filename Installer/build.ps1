# ============================================================
#  build.ps1 — Compile + package PresetLauncher en un installeur
#  Usage : .\Installer\build.ps1
#          .\Installer\build.ps1 -Version "1.3.0"
# ============================================================
param(
    [string]$Version = ""
)

$ErrorActionPreference = "Stop"
$root    = Split-Path $PSScriptRoot -Parent
$issFile = Join-Path $PSScriptRoot "PresetLauncher.iss"

# ── 1. Lire la version depuis le .iss si non fournie ─────────────────────────
if (-not $Version) {
    $match = Select-String -Path $issFile -Pattern '#define AppVersion "(.+)"'
    $Version = if ($match) { $match.Matches[0].Groups[1].Value } else { "1.0.0" }
}
Write-Host "Version : $Version" -ForegroundColor Cyan

# ── 2. dotnet publish (self-contained, dossier) ───────────────────────────────
Write-Host "`n[1/2] Publication .NET..." -ForegroundColor Cyan

$publishDir = Join-Path $root "publish\win-x64"
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }

$csproj = Join-Path $root "PresetLauncherWinUI3\PresetLauncherWinUI3.csproj"

dotnet publish $csproj `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    -p:WindowsPackageType=None `
    -p:PublishReadyToRun=true `
    -p:Version=$Version `
    --output $publishDir

if ($LASTEXITCODE -ne 0) { Write-Error "dotnet publish a echoue (code $LASTEXITCODE)"; exit 1 }

$exe = Join-Path $publishDir "PresetLauncherWinUI3.exe"
if (-not (Test-Path $exe)) { Write-Error "Executable introuvable : $exe"; exit 1 }

$sizeMB = [math]::Round((Get-Item $exe).Length / 1MB, 1)
Write-Host "  -> PresetLauncherWinUI3.exe ($sizeMB MB)" -ForegroundColor Green

# ── 3. Inno Setup ─────────────────────────────────────────────────────────────
Write-Host "`n[2/2] Compilation Inno Setup..." -ForegroundColor Cyan

$isccCmd = Get-Command ISCC -ErrorAction SilentlyContinue
$isccFromPath = if ($isccCmd) { $isccCmd.Source } else { $null }

$iscc = @(
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe",
    $isccFromPath
) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1

if (-not $iscc) {
    Write-Error "Inno Setup introuvable. Telecharger : https://jrsoftware.org/isdl.php"
    exit 1
}

$outputDir = Join-Path $PSScriptRoot "Output"
New-Item -ItemType Directory -Force $outputDir | Out-Null

& $iscc "/DAppVersion=$Version" "/O$outputDir" $issFile

if ($LASTEXITCODE -ne 0) { Write-Error "Inno Setup a echoue (code $LASTEXITCODE)"; exit 1 }

# ── 4. Resultat ───────────────────────────────────────────────────────────────
$installer = Get-ChildItem $outputDir -Filter "*.exe" | Sort-Object LastWriteTime -Desc | Select-Object -First 1
if ($installer) {
    $sizeMB2 = [math]::Round($installer.Length / 1MB, 1)
    Write-Host "`n=== DONE ===" -ForegroundColor Green
    Write-Host "Installeur pret : $($installer.FullName) ($sizeMB2 MB)" -ForegroundColor Cyan
} else {
    Write-Error "Installeur non trouve dans $outputDir"
}
