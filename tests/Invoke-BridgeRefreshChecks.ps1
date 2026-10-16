param([string]$Dotnet='dotnet', [string]$DependencyDirectory)
$ErrorActionPreference='Stop'
Set-StrictMode -Version 3
$root=Split-Path $PSScriptRoot -Parent
$lock=Get-Content -LiteralPath (Join-Path $root 'packaging/dependencies.lock.json') -Raw | ConvertFrom-Json
$dependency=@($lock.Dependencies | Where-Object Id -eq 'optiscaler_fg')[0]
$scratch=Join-Path ([IO.Path]::GetTempPath()) ('bbw-bridge-refresh-'+[guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($scratch)
try {
    $archive=Join-Path $scratch 'optiscaler_fg.zip'
    if($DependencyDirectory) { [IO.File]::Copy((Join-Path $DependencyDirectory 'optiscaler_fg.zip'),$archive) }
    else {
        Import-Module (Join-Path $root 'scripts/Setup.Core.psm1') -Force
        & (Get-Module Setup.Core) { param($Url,$Destination) Receive-BbwDownload ([uri]$Url) $Destination } $dependency.Url $archive
    }
    if((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $dependency.SHA256){throw 'Proxy fixture archive hash mismatch.'}
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip=[IO.Compression.ZipFile]::OpenRead($archive)
    try {
        $maps=@($dependency.Files | Where-Object Target -eq 'version.dll')
        if($maps.Count -ne 1){throw 'Pinned proxy file map is ambiguous.'}
        $entry=$zip.GetEntry($maps[0].Source)
        if(-not $entry){throw 'Pinned proxy archive member missing.'}
        $proxy=Join-Path $scratch 'version.dll'
        [IO.Compression.ZipFileExtensions]::ExtractToFile($entry,$proxy,$false)
    } finally {$zip.Dispose()}
    & $Dotnet run --project (Join-Path $PSScriptRoot 'BridgeRefreshChecks.csproj') -c Release -- $proxy
    if($LASTEXITCODE -ne 0){throw "Bridge refresh checks failed: $LASTEXITCODE"}
} finally {
    $resolved=[IO.Path]::GetFullPath($scratch)
    $temporary=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')+'\'
    if(-not $resolved.StartsWith($temporary,[StringComparison]::OrdinalIgnoreCase) -or
        [IO.Path]::GetFileName($resolved) -notlike 'bbw-bridge-refresh-*'){throw 'Unsafe fixture cleanup path.'}
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
