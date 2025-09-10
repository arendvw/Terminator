# build - Wrapper script for the build tool
# Usage: ./run.ps1 [command] [arguments]

$ErrorActionPreference = 'Stop'

# Get the directory of this script, which is assumed to be the project root.
$Project_Path = $PSScriptRoot

$Build_Project = Join-Path $Project_Path "BuildTools.csproj"
$Build_Output = Join-Path $Project_Path "bin/Debug/net9.0/BuildTools.dll"

# Ensure we're in the project directory
Push-Location $Project_Path

# Check if the build DLL exists and rebuild only if it doesn't, or if source files are newer
$rebuild = $false
if (-not (Test-Path $Build_Output)) {
    $rebuild = $true
} else {
    $outputWriteTime = (Get-Item $Build_Output).LastWriteTime
    $newerSourceFiles = Get-ChildItem -Path . -Filter *.cs -Recurse | Where-Object { $_.LastWriteTime -gt $outputWriteTime }
    if ($newerSourceFiles) {
        $rebuild = $true
    }
}

if ($rebuild) {
    Write-Host "Building the build tool..."
    dotnet build $Build_Project --configuration Debug --no-restore
}

Pop-Location

# Run the build tool with all arguments passed to this script
dotnet $Build_Output $args
