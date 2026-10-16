param(
    [string]$ManagedDll,
    [string]$NativeDll,
    [Parameter(Mandatory=$true)][string]$Destination
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if (-not $ManagedDll) { $ManagedDll = Join-Path $root 'src/managed/bin/Release/net6.0/BigWalkDLSS.dll' }
if (-not $NativeDll) { $NativeDll = Join-Path $root 'build/native/Release/BigWalkDLSSNative.dll' }
foreach ($path in @($ManagedDll,$NativeDll)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Build first: missing $path" }
}
$destinationPath = [IO.Path]::GetFullPath($Destination)
if (Test-Path -LiteralPath $destinationPath) { throw 'Choose a new, empty destination folder.' }
[void][IO.Directory]::CreateDirectory($destinationPath)
foreach ($name in @('README.md','LICENSE','NOTICE','THIRD_PARTY_NOTICES.md','CHANGELOG.md','Setup.cmd')) {
    Copy-Item -LiteralPath (Join-Path $root $name) -Destination $destinationPath
}
& (Join-Path $PSScriptRoot 'Build-Setup.ps1') -Destination (Join-Path $destinationPath 'Setup.exe')
foreach ($folder in @('src','scripts','docs','tests','packaging')) {
    $sourceRoot = Join-Path $root $folder
    foreach ($file in Get-ChildItem -LiteralPath $sourceRoot -Recurse -File) {
        $relative = $file.FullName.Substring($root.Length + 1)
        if ($relative -match '(^|[\\/])(bin|obj)([\\/]|$)') { continue }
        $target = Join-Path $destinationPath $relative
        [void][IO.Directory]::CreateDirectory((Split-Path $target -Parent))
        Copy-Item -LiteralPath $file.FullName -Destination $target
    }
}
$payload = Join-Path $destinationPath 'payload/BigWalkDLSS'
[void][IO.Directory]::CreateDirectory($payload)
Copy-Item -LiteralPath $ManagedDll -Destination (Join-Path $payload 'BigWalkDLSS.dll')
Copy-Item -LiteralPath $NativeDll -Destination (Join-Path $payload 'BigWalkDLSSNative.dll')
$defaults = Join-Path $destinationPath 'payload/defaults'
[void][IO.Directory]::CreateDirectory($defaults)
Copy-Item -LiteralPath (Join-Path $root 'packaging/default-native.cfg') -Destination (Join-Path $defaults 'local.bigwalk.native-dlss.cfg')
if (Test-Path -LiteralPath (Join-Path $root 'packaging/default-opti.ini')) {
    Copy-Item -LiteralPath (Join-Path $root 'packaging/default-opti.ini') -Destination (Join-Path $defaults 'OptiScaler.ini')
}
$hashes = foreach ($file in Get-ChildItem -LiteralPath $destinationPath -Recurse -File | Sort-Object FullName) {
    $relative = $file.FullName.Substring($destinationPath.Length+1).Replace('\','/')
    "$( (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant())  $relative"
}
[IO.File]::WriteAllLines((Join-Path $destinationPath 'SHA256SUMS.txt'),[string[]]$hashes,(New-Object Text.UTF8Encoding($false)))
Compress-Archive -Path (Join-Path $destinationPath '*') -DestinationPath ($destinationPath + '.zip')
Write-Output "Release package: $destinationPath.zip"
Get-FileHash -LiteralPath ($destinationPath + '.zip') -Algorithm SHA256
