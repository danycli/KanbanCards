param(
    [switch]$SelfContained = $false
)

$ErrorActionPreference = "Stop"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host " Building Kanban Cards Modern Setup Bundle " -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

$InstallerDir = $PSScriptRoot
$RepoRoot = Split-Path $InstallerDir -Parent
$PublishDir = Join-Path $RepoRoot "bin\Release\net8.0-windows\win-x64\publish"
$SetupProjectDir = Join-Path $RepoRoot "src\KanbanCards.Setup"
$AppZipPath = Join-Path $SetupProjectDir "app.zip"
[xml]$versionProps = Get-Content (Join-Path $RepoRoot "Directory.Build.props")
$releaseVersion = $versionProps.Project.PropertyGroup.Version
$OutputDir = Join-Path (Join-Path $InstallerDir "Output") $releaseVersion

# Reuse of a publish directory can retain files from an earlier self-contained
# build and silently inflate a framework-dependent release by hundreds of MB.
$repoFullPath = [System.IO.Path]::GetFullPath($RepoRoot).TrimEnd('\') + '\'
$publishFullPath = [System.IO.Path]::GetFullPath($PublishDir)
if (-not $publishFullPath.StartsWith((Join-Path $repoFullPath 'bin\'), [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to clean publish path outside the repository build output: $publishFullPath"
}
if (Test-Path -LiteralPath $publishFullPath) {
    Remove-Item -LiteralPath $publishFullPath -Recurse -Force
}
$outputFullPath = [System.IO.Path]::GetFullPath($OutputDir)
$outputRoot = [System.IO.Path]::GetFullPath((Join-Path $InstallerDir 'Output')).TrimEnd('\') + '\'
if (-not $outputFullPath.StartsWith($outputRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to clean release output outside Installer/Output: $outputFullPath"
}
if (Test-Path -LiteralPath $outputFullPath) {
    Remove-Item -LiteralPath $outputFullPath -Recurse -Force
}

# 1. Publish main application
Write-Host "[1/5] Publishing main application (Release/win-x64)..." -ForegroundColor Yellow
$selfContainedArg = if ($SelfContained) { "--self-contained:true" } else { "--self-contained:false" }
& dotnet publish "$RepoRoot\KanbanCards.csproj" -c Release -r win-x64 $selfContainedArg -o $PublishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish KanbanCards failed" }

# 2. Package application files into zip
Write-Host "[2/5] Compressing payload archive..." -ForegroundColor Yellow
Add-Type -AssemblyName System.IO.Compression.FileSystem
if (Test-Path $AppZipPath) { Remove-Item $AppZipPath -Force }
[System.IO.Compression.ZipFile]::CreateFromDirectory($PublishDir, $AppZipPath, [System.IO.Compression.CompressionLevel]::Optimal, $false)
$zipSize = (Get-Item $AppZipPath).Length / 1MB
Write-Host ("      Payload compressed: {0:N2} MB" -f $zipSize) -ForegroundColor Green

# 3. Publish setup executable
Write-Host "[3/5] Publishing native KanbanCardsSetup.exe..." -ForegroundColor Yellow
if (-not (Test-Path $OutputDir)) { New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null }
& dotnet publish "$SetupProjectDir\KanbanCards.Setup.csproj" -c Release -r win-x64 "-p:PublishSingleFile=true" "-p:IncludeNativeLibrariesForSelfExtract=true" $selfContainedArg -o $OutputDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish KanbanCards.Setup failed" }

# 4. Clean up temporary app.zip
Write-Host "[4/5] Cleaning up temporary archive..." -ForegroundColor Yellow
if (Test-Path $AppZipPath) { Remove-Item $AppZipPath -Force }

# 5. Compute SHA-256 Checksums
Write-Host "[5/5] Generating SHA256 checksums..." -ForegroundColor Yellow
$setupExe = Join-Path $OutputDir "KanbanCardsSetup.exe"
if (Test-Path $setupExe) {
    $hash = (Get-FileHash -Algorithm SHA256 $setupExe).Hash.ToLowerInvariant()
    $checksumFile = Join-Path $OutputDir "SHA256SUMS"
    "$hash  KanbanCardsSetup.exe" | Set-Content -Path $checksumFile -Encoding ASCII
    Write-Host ("      SHA256: {0}" -f $hash) -ForegroundColor Green
}

# Clean any PDB from output
Get-ChildItem -Path $OutputDir -Filter "*.pdb" -ErrorAction SilentlyContinue | Remove-Item -Force

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host " Build Complete! Installer ready at:     " -ForegroundColor Cyan
Write-Host " $setupExe" -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Cyan
