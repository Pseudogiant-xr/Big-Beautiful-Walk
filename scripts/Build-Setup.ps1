param([Parameter(Mandatory=$true)][string]$Destination)
$ErrorActionPreference='Stop'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if (-not (Test-Path -LiteralPath $compiler -PathType Leaf)) { throw 'Packaging Setup.exe requires the Windows x64 .NET Framework compiler.' }
$source=Join-Path $PSScriptRoot '../src/setup/SetupLauncher.cs'
$output=[IO.Path]::GetFullPath($Destination)
[void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($output))
& $compiler /nologo /noconfig /target:winexe /platform:x64 /optimize+ /debug- /warn:4 /warnaserror+ /reference:System.dll /reference:System.Windows.Forms.dll ("/out:"+$output) $source
if ($LASTEXITCODE -ne 0) { throw "Setup launcher compilation failed with exit code $LASTEXITCODE." }
