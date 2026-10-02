<#
.SYNOPSIS
    Builds and packages the Windows release of AntigravityQuota as a single-file executable and zip archive.

.DESCRIPTION
    Compiles AntigravityQuota.App in Release configuration for win-x64 runtime (Single-File, ReadyToRun, Self-Contained),
    stages the executable and documentation, and produces dist/AntigravityQuota-v<Version>-windows-x64.zip.

.PARAMETER Version
    Optional version string. If omitted, the version is extracted automatically from AntigravityQuota.App.csproj.

.EXAMPLE
    .\scripts\build_windows_release.ps1
    .\scripts\build_windows_release.ps1 -Version "1.1.0"
#>

[CmdletBinding()]
param (
    [Parameter(Position = 0, Mandatory = $false)]
    [string]$Version = ""
)

$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = (Resolve-Path "$ScriptDir/..").Path
$ProjectFile = Join-Path $RepoRoot "src/windows/AntigravityQuota.App/AntigravityQuota.App.csproj"
$DistDir = Join-Path $RepoRoot "dist"

if (-not (Test-Path $ProjectFile)) {
    Write-Error "Project file not found at: $ProjectFile"
    exit 1
}

# 1. Resolve Version
if ([string]::IsNullOrWhiteSpace($Version)) {
    $csprojContent = Get-Content -Path $ProjectFile -Raw
    $versionMatch = [regex]::Match($csprojContent, '<Version>([^<]+)</Version>')
    if ($versionMatch.Success) {
        $Version = $versionMatch.Groups[1].Value.Trim()
    } else {
        $Version = "1.1.0"
    }
}

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  AntigravityQuota Windows Release Builder v$Version" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "Repository root : $RepoRoot"
Write-Host "Target project  : $ProjectFile"
Write-Host "Release version : $Version"
Write-Host ""

# 2. Compile and Publish Single-File Executable
Write-Host "=== 1. Compiling & Publishing Win-x64 Single-File Executable ===" -ForegroundColor Yellow
$publishArgs = @(
    "publish",
    $ProjectFile,
    "-c", "Release",
    "-r", "win-x64",
    "--self-contained", "true",
    "-p:PublishSingleFile=true",
    "-p:PublishReadyToRun=true",
    "-p:IncludeNativeLibrariesForSelfExtract=true"
)

Write-Host "Running: dotnet $publishArgs"
& dotnet $publishArgs

if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

# 3. Locate Published Executable
$PublishDir = Join-Path $RepoRoot "src/windows/AntigravityQuota.App/bin/Release/net9.0-windows7.0/win-x64/publish"
$ExePath = Join-Path $PublishDir "AntigravityQuota.exe"

if (-not (Test-Path $ExePath)) {
    Write-Error "Published executable not found at: $ExePath"
    exit 1
}

# 4. Prepare Staging Directory
Write-Host "`n=== 2. Staging Distribution Files ===" -ForegroundColor Yellow
if (-not (Test-Path $DistDir)) {
    New-Item -ItemType Directory -Path $DistDir -Force | Out-Null
}

$StagingDir = Join-Path $DistDir "staging_windows_x64"
if (Test-Path $StagingDir) {
    Remove-Item -Path $StagingDir -Recurse -Force
}
New-Item -ItemType Directory -Path $StagingDir -Force | Out-Null

Copy-Item -Path $ExePath -Destination (Join-Path $StagingDir "AntigravityQuota.exe") -Force
Write-Host "Copied: AntigravityQuota.exe"

$DocFiles = @("README.md", "README.ru.md", "LICENSE")
foreach ($doc in $DocFiles) {
    $docPath = Join-Path $RepoRoot $doc
    if (Test-Path $docPath) {
        Copy-Item -Path $docPath -Destination (Join-Path $StagingDir $doc) -Force
        Write-Host "Copied: $doc"
    }
}

# 5. Create Distribution Zip Archive
$ZipName = "AntigravityQuota-v$Version-windows-x64.zip"
$ZipPath = Join-Path $DistDir $ZipName

Write-Host "`n=== 3. Creating Distribution Archive ($ZipName) ===" -ForegroundColor Yellow
if (Test-Path $ZipPath) {
    Remove-Item -Path $ZipPath -Force
}

Compress-Archive -Path "$StagingDir/*" -DestinationPath $ZipPath -Force

# 6. Clean up Staging Directory
Remove-Item -Path $StagingDir -Recurse -Force

# 7. Print Release Summary & Checksums
$zipItem = Get-Item $ZipPath
$zipSizeMb = [math]::Round($zipItem.Length / 1MB, 2)
$hash = Get-FileHash -Path $ZipPath -Algorithm SHA256

Write-Host "`n==========================================================" -ForegroundColor Green
Write-Host "  Windows Release Build Completed Successfully!  " -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
Write-Host "Archive  : $ZipPath"
Write-Host "Size     : $zipSizeMb MB ($($zipItem.Length) bytes)"
Write-Host "SHA256   : $($hash.Hash)"
Write-Host "==========================================================" -ForegroundColor Green
