param(
    [switch]$SelfContained = $false
)

$ErrorActionPreference = "Stop"
$installerBuildScript = Join-Path $PSScriptRoot "Installer\build.ps1"
if (-not (Test-Path -LiteralPath $installerBuildScript)) {
    throw "Installer build script not found: $installerBuildScript"
}

$buildArguments = @()
if ($SelfContained) { $buildArguments += "-SelfContained" }
& $installerBuildScript @buildArguments
if ($LASTEXITCODE -ne 0) { throw "Kanban Cards installer build failed" }
