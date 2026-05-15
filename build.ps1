# EAMAS Build + Release Script
# Usage:  .\build.ps1 -Version 1.2.0 -ReleaseNotes "What changed"
# Token:  Stored in %LOCALAPPDATA%\EAMAS\github-token.txt (never committed)
param(
    [string]$Version = "",
    [string]$ReleaseNotes = "",
    [switch]$SkipRelease   # pass -SkipRelease to build without publishing to GitHub
)

$Root         = $PSScriptRoot
$SrcProject   = "$Root\src\EAMAS.Desktop\EAMAS.Desktop.csproj"
$PublishOut   = "$Root\build\publish"
$InstallerDir = "$Root\installer"
$VersionJson  = "$Root\version.json"
$GithubRepo   = "anshulsing155/EAMAS"
$ManifestRepo = "anshulsing155/EAMAS-updates"
$TokenFile    = "$env:LOCALAPPDATA\EAMAS\github-token.txt"

# --- Locate Inno Setup -------------------------------------------------------
$IsccPath = ""
$candidates = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\iscc.exe",
    "C:\Program Files (x86)\Inno Setup 6\iscc.exe",
    "C:\Program Files\Inno Setup 6\iscc.exe"
)
$fromPath = Get-Command iscc -ErrorAction SilentlyContinue
if ($fromPath) { $candidates += $fromPath.Source }
foreach ($c in $candidates) {
    if ($c -and (Test-Path $c)) { $IsccPath = $c; break }
}

# --- Read current version from csproj if not supplied -----------------------
if (-not $Version) {
    $csproj  = [xml](Get-Content $SrcProject)
    $Version = $csproj.Project.PropertyGroup.Version
    if (-not $Version) { $Version = "1.0.0" }
}

$InstallerFile = "$InstallerDir\EAMAS-Setup-$Version.exe"

Write-Host ""
Write-Host "======================================================" -ForegroundColor Cyan
Write-Host "  EAMAS Build  --  v$Version" -ForegroundColor Cyan
Write-Host "======================================================" -ForegroundColor Cyan
Write-Host ""

# --- 1. Patch version --------------------------------------------------------
Write-Host "[1/6] Patching version $Version into project files..." -ForegroundColor Yellow
$content = Get-Content $SrcProject -Raw
$content = $content -replace '<Version>.*?</Version>',                 "<Version>$Version</Version>"
$content = $content -replace '<AssemblyVersion>.*?</AssemblyVersion>', "<AssemblyVersion>$Version.0</AssemblyVersion>"
$content = $content -replace '<FileVersion>.*?</FileVersion>',         "<FileVersion>$Version.0</FileVersion>"
Set-Content $SrcProject $content -Encoding utf8

$iss = Get-Content "$InstallerDir\EAMAS.iss" -Raw
$iss = $iss -replace '#define AppVersion\s+"[^"]+"', "#define AppVersion   `"$Version`""
Set-Content "$InstallerDir\EAMAS.iss" $iss -Encoding utf8
Write-Host "    Done." -ForegroundColor Green

# --- 2. Publish self-contained exe -------------------------------------------
Write-Host "[2/6] Publishing self-contained single-file executable..." -ForegroundColor Yellow
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

# --- 3. Build installer ------------------------------------------------------
Write-Host "[3/6] Building installer..." -ForegroundColor Yellow
if ($IsccPath) {
    & $IsccPath "$InstallerDir\EAMAS.iss"
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Inno Setup compilation failed." -ForegroundColor Red
        exit 1
    }
    Write-Host "    Installer: $InstallerFile" -ForegroundColor Green
} else {
    Write-Host "    Inno Setup not found -- skipping installer build." -ForegroundColor Yellow
    $SkipRelease = $true
}

# --- 4. Update version.json --------------------------------------------------
Write-Host "[4/6] Updating version.json..." -ForegroundColor Yellow
$notes = if ($ReleaseNotes) { $ReleaseNotes } else {
    (Get-Content $VersionJson -Raw | ConvertFrom-Json).releaseNotes
}
$manifest = [ordered]@{
    version      = $Version
    downloadUrl  = "https://github.com/$GithubRepo/releases/download/v$Version/EAMAS-Setup-$Version.exe"
    releaseNotes = $notes
}
$manifestJson = $manifest | ConvertTo-Json -Depth 2
$manifestJson | Set-Content $VersionJson -Encoding utf8
Write-Host "    version.json updated." -ForegroundColor Green

# Push version.json to public manifest repo so clients can check for updates
Write-Host "    Pushing manifest to public update repo..." -ForegroundColor Yellow
$manifestToken = if (Test-Path $TokenFile) { (Get-Content $TokenFile -Raw).Trim() } else { "" }
if ($manifestToken) {
    $manifestHeaders = @{
        Authorization          = "Bearer $manifestToken"
        Accept                 = "application/vnd.github+json"
        "X-GitHub-Api-Version" = "2022-11-28"
    }
    # Get current SHA of version.json in the public repo (needed for update)
    try {
        $existing = Invoke-RestMethod `
            -Uri "https://api.github.com/repos/$ManifestRepo/contents/version.json" `
            -Headers $manifestHeaders -ErrorAction Stop
        $sha = $existing.sha
    } catch { $sha = $null }

    $encoded = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes($manifestJson))
    $putBody = @{ message = "Update manifest to v$Version"; content = $encoded }
    if ($sha) { $putBody["sha"] = $sha }

    try {
        Invoke-RestMethod `
            -Uri "https://api.github.com/repos/$ManifestRepo/contents/version.json" `
            -Method PUT -Headers $manifestHeaders `
            -Body ($putBody | ConvertTo-Json) `
            -ContentType "application/json; charset=utf-8" | Out-Null
        Write-Host "    Manifest live at: https://raw.githubusercontent.com/$ManifestRepo/main/version.json" -ForegroundColor Green
    } catch {
        Write-Host "    WARNING: Could not push manifest -- $_" -ForegroundColor Yellow
    }
} else {
    Write-Host "    WARNING: No token found -- manifest not pushed to public repo." -ForegroundColor Yellow
}

# --- 5. Commit and push source changes ---------------------------------------
Write-Host "[5/6] Committing and pushing source changes..." -ForegroundColor Yellow
git add version.json "installer\EAMAS.iss" "src\EAMAS.Desktop\EAMAS.Desktop.csproj"
git add -u  # stage any other tracked modifications
$staged = git diff --cached --name-only
if ($staged) {
    git commit -m "Release v$Version"
    git push origin main
    if ($LASTEXITCODE -ne 0) {
        Write-Host "WARNING: git push failed -- check your network/auth." -ForegroundColor Yellow
    } else {
        Write-Host "    Pushed to GitHub." -ForegroundColor Green
    }
} else {
    Write-Host "    No source changes to commit." -ForegroundColor Yellow
}

# --- 6. Create GitHub Release and upload installer ---------------------------
if ($SkipRelease) {
    Write-Host "[6/6] Skipping GitHub Release (no installer or -SkipRelease passed)." -ForegroundColor Yellow
} else {
    Write-Host "[6/6] Creating GitHub Release v$Version and uploading installer..." -ForegroundColor Yellow

    # Read token
    if (-not (Test-Path $TokenFile)) {
        Write-Host "    ERROR: GitHub token not found at $TokenFile" -ForegroundColor Red
        Write-Host "    Run:  '$env:LOCALAPPDATA\EAMAS' folder, create github-token.txt with your PAT." -ForegroundColor Yellow
        exit 1
    }
    $token = (Get-Content $TokenFile -Raw).Trim()
    $headers = @{
        Authorization = "Bearer $token"
        Accept        = "application/vnd.github+json"
        "X-GitHub-Api-Version" = "2022-11-28"
    }

    # Create the release
    $releaseBody = @{
        tag_name         = "v$Version"
        target_commitish = "main"
        name             = "EAMAS v$Version"
        body             = $notes
        draft            = $false
        prerelease       = $false
    } | ConvertTo-Json

    try {
        $release = Invoke-RestMethod `
            -Uri "https://api.github.com/repos/$GithubRepo/releases" `
            -Method POST `
            -Headers $headers `
            -Body $releaseBody `
            -ContentType "application/json; charset=utf-8"
        Write-Host "    Release created: $($release.html_url)" -ForegroundColor Green
    } catch {
        Write-Host "    ERROR creating release: $_" -ForegroundColor Red
        exit 1
    }

    # Upload installer asset
    $uploadUrl = "https://uploads.github.com/repos/$GithubRepo/releases/$($release.id)/assets?name=EAMAS-Setup-$Version.exe"
    $fileBytes = [System.IO.File]::ReadAllBytes($InstallerFile)

    try {
        $uploadHeaders = $headers.Clone()
        $uploadHeaders["Content-Type"] = "application/octet-stream"
        $asset = Invoke-RestMethod `
            -Uri $uploadUrl `
            -Method POST `
            -Headers $uploadHeaders `
            -Body $fileBytes
        Write-Host "    Asset uploaded: $($asset.browser_download_url)" -ForegroundColor Green
    } catch {
        Write-Host "    ERROR uploading asset: $_" -ForegroundColor Red
        Write-Host "    Upload manually at: $($release.upload_url)" -ForegroundColor Yellow
        exit 1
    }
}

# --- Done --------------------------------------------------------------------
Write-Host ""
Write-Host "Build complete!" -ForegroundColor Green
Write-Host ""
Write-Host "  Executable   : $PublishOut\EAMAS.exe"
Write-Host "  Installer    : $InstallerFile"
Write-Host "  version.json : updated"
if (-not $SkipRelease) {
    Write-Host "  GitHub Release: https://github.com/$GithubRepo/releases/tag/v$Version" -ForegroundColor Cyan
    Write-Host "  --> All installed EAMAS clients will see the update notification." -ForegroundColor Green
}
Write-Host ""
