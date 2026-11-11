$ErrorActionPreference = "Stop"

$InstallerDir = $PSScriptRoot
$RepoRoot = Split-Path $InstallerDir -Parent
[xml]$versionProps = Get-Content (Join-Path $RepoRoot "Directory.Build.props")
$releaseVersion = $versionProps.Project.PropertyGroup.Version
$OutputDir = Join-Path (Join-Path $InstallerDir "Output") $releaseVersion
$SetupExe = Join-Path $OutputDir "KanbanCardsSetup.exe"
$ChecksumFile = Join-Path $OutputDir "SHA256SUMS"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host " Verifying Kanban Cards Installer Bundle  " -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

if (-not (Test-Path $SetupExe)) {
    Write-Error "Installer executable not found: $SetupExe"
    exit 1
}

if (-not (Test-Path $ChecksumFile)) {
    Write-Error "Checksum file not found: $ChecksumFile"
    exit 1
}

$expectedContent = Get-Content $ChecksumFile -Raw
$expectedHash = ($expectedContent -split '\s+')[0].Trim().ToLowerInvariant()
$actualHash = (Get-FileHash -Algorithm SHA256 $SetupExe).Hash.ToLowerInvariant()
$fileSizeMb = ((Get-Item $SetupExe).Length / 1MB)

Write-Host ("File:         {0}" -f $SetupExe)
Write-Host ("Size:         {0:N2} MB" -f $fileSizeMb)
Write-Host ("Expected SHA: {0}" -f $expectedHash)
Write-Host ("Actual SHA:   {0}" -f $actualHash)

if ($expectedHash -eq $actualHash) {
    Write-Host "`n[PASS] Installer verification successful! Checksum matches." -ForegroundColor Green
    exit 0
} else {
    Write-Host "`n[FAIL] Checksum mismatch! The installer may be corrupted or rebuilt." -ForegroundColor Red
    exit 1
}
