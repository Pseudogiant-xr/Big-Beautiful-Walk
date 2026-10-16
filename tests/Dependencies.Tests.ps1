param(
    [Parameter(Mandatory=$true)][string]$PackagePath,
    [Parameter(Mandatory=$true)][string]$DependencyDirectory
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version 2.0
Import-Module (Join-Path $PSScriptRoot '../scripts/Setup.Core.psm1') -Force
$scratch=Join-Path ([IO.Path]::GetTempPath()) ('bbw-dependencies-'+[guid]::NewGuid().ToString('N'))
try {
    foreach ($feature in @('fg','nr')) {
        $game=Join-Path $scratch ('game-'+$feature)
        [void][IO.Directory]::CreateDirectory((Join-Path $game 'Big Walk_Data'))
        [IO.File]::WriteAllText((Join-Path $game 'Big Walk.exe'),'Disposable file-operation fixture; never executed.')
        [IO.File]::WriteAllText((Join-Path $game 'GameAssembly.dll'),'Disposable game assembly sentinel.')
        $arguments=@{GamePath=$game;PackagePath=$PackagePath;DependencyDirectory=$DependencyDirectory;Features=@($feature)}
        Invoke-BbwSetup @arguments -Mode Install | Out-Null
        $manifest=Get-Content -LiteralPath (Join-Path $game '.big-beautiful-walk/manifest.json') -Raw | ConvertFrom-Json
        foreach ($entry in $manifest.Entries) {
            $file=Join-Path $game $entry.Target
            if (-not (Test-Path -LiteralPath $file -PathType Leaf) -or
                (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.InstalledHash) {
                throw "Installed bytes differ: $($entry.Target)"
            }
        }
        $notices=Join-Path $game 'BepInEx/BigWalkDLSS/notices'
        $links=0
        foreach ($document in Get-ChildItem -LiteralPath $notices -Filter '*.md' -Recurse -File) {
            foreach ($link in [regex]::Matches([IO.File]::ReadAllText($document.FullName),'\]\(([^)]+)\)')) {
                $relative=$link.Groups[1].Value.Split('#')[0]
                if (-not $relative -or $relative -match '^[a-zA-Z][a-zA-Z0-9+.-]*:') { continue }
                if (-not (Test-Path -LiteralPath (Join-Path $document.DirectoryName $relative))) { throw "Broken installed notice link in $($document.Name): $relative" }
                $links++
            }
        }
        foreach ($relative in @('OptiScaler/amd_fidelityfx_framegeneration_dx12.dll','OptiScaler/libxess_fg.dll',
            'OptiScaler/libxell.dll','OptiScaler/nvngx.dll_dlssnr.dll','BepInEx/BigWalkDLSS/notices/NOTICE')) {
            if (-not (Test-Path -LiteralPath (Join-Path $game $relative))) { throw "Provider/notice missing: $relative" }
        }
        $model=Join-Path $game 'OptiScaler/nvngx_dlssnr.dll'
        if ($feature -eq 'fg') {
            if (Test-Path -LiteralPath $model) { throw 'FG-only selection unexpectedly installed the optional NR model.' }
        } elseif ((Get-FileHash -LiteralPath $model -Algorithm SHA256).Hash -ne '6EB209E764F39872625DEBD6ABAF45E2BB6322F6F270F781F70C059AE30B3927') {
            throw 'Installed NR model differs from the pinned community RHI SF-v2 model.'
        }
        $ini=Get-Content -LiteralPath (Join-Path $game 'OptiScaler.ini') -Raw
        if (@([regex]::Matches($ini,'(?m)^Enabled=false\r?$')).Count -ne 2) { throw 'Fresh FG and NR defaults must be off.' }
        # Repair with no new opt-in must retain exactly the already-owned features.
        Invoke-BbwSetup -GamePath $game -PackagePath $PackagePath -DependencyDirectory $DependencyDirectory -Mode Install | Out-Null
        if ($feature -eq 'fg' -and (Test-Path -LiteralPath $model)) { throw 'FG-only repair unexpectedly opted into NR.' }
        if ($feature -eq 'nr' -and (Get-FileHash -LiteralPath $model -Algorithm SHA256).Hash -ne '6EB209E764F39872625DEBD6ABAF45E2BB6322F6F270F781F70C059AE30B3927') { throw 'NR repair changed the pinned model bytes.' }
        Invoke-BbwSetup @arguments -Mode Uninstall | Out-Null
        foreach ($entry in $manifest.Entries) {
            if (-not $entry.PreserveOnRemove -and (Test-Path -LiteralPath (Join-Path $game $entry.Target))) {
                throw "Owned file survived removal: $($entry.Target)"
            }
        }
        if ([IO.File]::ReadAllText((Join-Path $game 'GameAssembly.dll')) -ne 'Disposable game assembly sentinel.') { throw 'Game sentinel changed.' }
        Write-Output "PASS pinned $feature dependencies installed, repaired and removed in an unlaunched disposable fixture; $(@($manifest.Entries).Count) installed file hashes and $links notice links verified."
    }
}
finally {
    $resolved=[IO.Path]::GetFullPath($scratch)
    $temporary=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')+'\'
    if (-not $resolved.StartsWith($temporary,[StringComparison]::OrdinalIgnoreCase) -or
        [IO.Path]::GetFileName($resolved) -notlike 'bbw-dependencies-*') { throw 'Fixture cleanup target escaped the temporary directory.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
