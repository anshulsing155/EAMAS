param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $repoRoot "artifacts\publish\$Runtime"
$installerDir = Join-Path $repoRoot "artifacts\installer"
$payloadZip = Join-Path $installerDir "EAMAS-$Runtime.zip"
$setupExe = Join-Path $installerDir "EAMAS-Setup-$Runtime.exe"
$bootstrapperProject = Join-Path $repoRoot "src\EAMAS.SetupBootstrapper\EAMAS.SetupBootstrapper.csproj"
$bootstrapperPayloadDir = Join-Path $repoRoot "src\EAMAS.SetupBootstrapper\Payload"
$bootstrapperPublishDir = Join-Path $repoRoot "artifacts\bootstrapper\$Runtime"
$installCmd = Join-Path $repoRoot "installer\install.cmd"
$uninstallCmd = Join-Path $repoRoot "installer\uninstall.cmd"
$dotnet = "C:\Program Files\dotnet\dotnet.exe"

New-Item -ItemType Directory -Force -Path $publishDir | Out-Null
New-Item -ItemType Directory -Force -Path $installerDir | Out-Null
New-Item -ItemType Directory -Force -Path $bootstrapperPayloadDir | Out-Null
New-Item -ItemType Directory -Force -Path $bootstrapperPublishDir | Out-Null

Write-Host "Publishing EAMAS for $Runtime..."
& $dotnet publish (Join-Path $repoRoot "src\EAMAS.Desktop\EAMAS.Desktop.csproj") `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:PublishTrimmed=false `
    -o $publishDir

if (Test-Path $payloadZip) {
    Remove-Item $payloadZip -Force
}

Write-Host "Creating payload zip..."
Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $payloadZip -Force

Copy-Item $payloadZip (Join-Path $bootstrapperPayloadDir "EAMAS-win-x64.zip") -Force

Write-Host "Building installer exe..."
& $dotnet publish $bootstrapperProject `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:PublishTrimmed=false `
    -o $bootstrapperPublishDir

Copy-Item (Join-Path $bootstrapperPublishDir "EAMAS.SetupBootstrapper.exe") $setupExe -Force

Write-Host ""
Write-Host "Publish output: $publishDir"
Write-Host "Installer output: $setupExe"
