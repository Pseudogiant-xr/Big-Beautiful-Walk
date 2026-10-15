param(
    [Parameter(Mandatory=$true)][string]$GameDir,
    [Parameter(Mandatory=$true)][string]$NgxRoot,
    [Parameter(Mandatory=$true)][string]$StreamlineRoot,
    [string]$Dotnet = 'dotnet'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
& $Dotnet build (Join-Path $root 'src/managed/BigWalkDLSS.csproj') -c Release "-p:GameDir=$GameDir" --nologo
if ($LASTEXITCODE -ne 0) { throw 'Managed build failed.' }
$native = Join-Path $root 'build/native'
cmake -S (Join-Path $root 'src/native') -B $native -G 'Visual Studio 17 2022' -A x64 "-DNGX_ROOT=$NgxRoot" "-DSTREAMLINE_ROOT=$StreamlineRoot"
if ($LASTEXITCODE -ne 0) { throw 'Native CMake configuration failed.' }
cmake --build $native --config Release --parallel 2
if ($LASTEXITCODE -ne 0) { throw 'Native build failed.' }
Write-Output 'Build complete. Run scripts/Package.ps1 to create the release ZIP.'
