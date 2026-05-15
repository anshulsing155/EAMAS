# ─────────────────────────────────────────────────────────────────────────────
# EAMAS Build Script
# Usage:  .\build.ps1               — builds current version
#         .\build.ps1 -Version 1.2.0 — bumps version, builds, updates version.json
# ─────────────────────────────────────────────────────────────────────────────
param(
    [string]$Version = "",
    [string]$ReleaseNotes = ""
)

$Root       = $PSScriptRoot
$SrcProject = "$Root\src\EAMAS.Desktop\EAMAS.Desktop.csproj"
$PublishOut = "$Root\build\publish"
$InstallerDir = "$Root\installer"
$VersionJson  = "$Root\version.json"
$IsccPath   = ""

# ── Locate Inno Setup ────────────────────────────────────────────────────────
$candidates = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\iscc.exe",   # user-scoped install (winget default)
    "C:\Program Files (x86)\Inno Setup 6\iscc.exe",
    "C:\Program Files\Inno Setup 6\iscc.exe",
    (Get-Command iscc -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -ErrorAction SilentlyContinue)
)
foreach ($c in $candidates) {
    if ($c -and (Test-Path $c)) { $IsccPath = $c; break }
}

# ── Read current version from csproj if not supplied ────────────────────────
if (-not $Version) {
    $csproj  = [xml](Get-Content $SrcProject)
    $Version = $csproj.Project.PropertyGroup.Version
    if (-not $Version) { $Version = "1.1.0" }
}

Write-Host ""
Write-Host "======================================================" -ForegroundColor Cyan
Write-Host "  EAMAS Build  —  v$Version" -ForegroundColor Cyan
Write-Host "======================================================" -ForegroundColor Cyan
Write-Host ""

# ── 1. Patch version in csproj ───────────────────────────────────────────────
Write-Host "[1/5] Patching version $Version into project files..." -ForegroundColor Yellow
$content = Get-Content $SrcProject -Raw
$content = $content -replace '<Version>.*?</Version>',             "<Version>$Version</Version>"
$content = $content -replace '<AssemblyVersion>.*?</AssemblyVersion>', "<AssemblyVersion>$Version.0</AssemblyVersion>"
$content = $content -replace '<FileVersion>.*?</FileVersion>',     "<FileVersion>$Version.0</FileVersion>"
Set-Content $SrcProject $content -Encoding utf8

# Patch installer script
$issFile = "$InstallerDir\EAMAS.iss"
$iss = Get-Content $issFile -Raw
$iss = $iss -replace '#define AppVersion\s+"[^"]+"', "#define AppVersion   `"$Version`""
Set-Content $issFile $iss -Encoding utf8

Write-Host "    Done." -ForegroundColor Green

# ── 2. Publish self-contained single-file exe ─────────────────────────────────
Write-Host "[2/5] Publishing self-contained single-file executable..." -ForegroundColor Yellow
if (Test-Path $PublishOut) { Remove-Item $PublishOut -Recurse -Force }
New-Item -ItemType Directory -Force $PublishOut | Out-Null

dotnet publish $SrcProject `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    --output $PublishOut `
    --nologo

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: dotnet publish failed." -ForegroundColor Red
    exit 1
}
Write-Host "    Published to: $PublishOut" -ForegroundColor Green

# ── 3. Build Inno Setup installer ─────────────────────────────────────────────
Write-Host "[3/5] Building installer..." -ForegroundColor Yellow
if ($IsccPath) {
    & $IsccPath "$InstallerDir\EAMAS.iss"
    if ($LASTEXITCODE -ne 0) {
        Write-Host "WARNING: Inno Setup compilation failed." -ForegroundColor Yellow
    } else {
        Write-Host "    Installer: $InstallerDir\EAMAS-Setup-$Version.exe" -ForegroundColor Green
    }
} else {
    Write-Host "    Inno Setup not found — skipping installer build." -ForegroundColor Yellow
    Write-Host "    Install from: https://jrsoftware.org/isdl.php  then re-run this script." -ForegroundColor Yellow
}

# ── 4. Update version.json manifest ──────────────────────────────────────────
Write-Host "[4/5] Updating version.json..." -ForegroundColor Yellow
$notes = if ($ReleaseNotes) { $ReleaseNotes } else {
    (Get-Content $VersionJson -Raw | ConvertFrom-Json).releaseNotes
}
$installerFilename = "EAMAS-Setup-$Version.exe"
$manifest = [ordered]@{
    version      = $Version
    downloadUrl  = "https://github.com/anshulsing155/EAMAS/releases/download/v$Version/$installerFilename"
    releaseNotes = $notes
}
$manifest | ConvertTo-Json -Depth 2 | Set-Content $VersionJson -Encoding utf8
Write-Host "    version.json updated." -ForegroundColor Green

# ── 5. Summary ────────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "[5/5] Build complete!" -ForegroundColor Green
Write-Host ""
Write-Host "  Executable : $PublishOut\EAMAS.exe"
if ($IsccPath) {
    Write-Host "  Installer  : $InstallerDir\EAMAS-Setup-$Version.exe"
}
Write-Host "  version.json updated — push this file + the installer to GitHub to trigger auto-update on all clients."
Write-Host ""
Write-Host "  To release update on client machines:" -ForegroundColor Cyan
Write-Host "    1. git add installer\EAMAS-Setup-$Version.exe version.json"
Write-Host "    2. git commit -m `"Release v$Version`""
Write-Host "    3. git push"
Write-Host "    → All installed EAMAS clients will see the update notification within minutes." -ForegroundColor Green
Write-Host ""
