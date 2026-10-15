Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

function Get-BbwHash([string]$Path) {
    if (-not [IO.File]::Exists($Path)) { return $null }
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Get-BbwMutexName([string]$GamePath) {
    $normalized=[IO.Path]::GetFullPath($GamePath).TrimEnd('\','/').ToUpperInvariant()
    $sha=[Security.Cryptography.SHA256]::Create()
    try { $key=[BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($normalized))).Replace('-','') }
    finally { $sha.Dispose() }
    return 'Local\BigBeautifulWalk-' + $key
}

function Resolve-BbwPath([string]$Root, [string]$Relative) {
    if ([string]::IsNullOrWhiteSpace($Relative) -or [IO.Path]::IsPathRooted($Relative) -or $Relative.Contains(':')) { throw "Unsafe relative path: $Relative" }
    foreach ($segment in ($Relative -split '[\\/]')) {
        if ($segment -and ($segment.EndsWith('.') -or $segment.EndsWith(' ') -or $segment -match '["<>|?*]' -or $segment -match '^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(\.|$)')) { throw "Unsafe Windows path segment: $Relative" }
    }
    $base = [IO.Path]::GetFullPath($Root).TrimEnd('\','/')
    $path = [IO.Path]::GetFullPath((Join-Path $base $Relative))
    if (-not $path.StartsWith($base + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw "Path escapes its directory: $Relative" }
    $cursor = $path
    while ($cursor) {
        if (Test-Path -LiteralPath $cursor) {
            if ((Get-Item -LiteralPath $cursor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Symbolic links and junctions are not supported: $cursor" }
        }
        $parent = [IO.Path]::GetDirectoryName($cursor)
        if ($parent -eq $cursor) { break }
        $cursor = $parent
    }
    return $path
}

function Write-BbwJson([string]$Path, $Value) {
    $temp = $Path + '.tmp'
    $buffer = (New-Object Text.UTF8Encoding($false)).GetBytes(($Value | ConvertTo-Json -Depth 20))
    $stream = [IO.File]::Open($temp, [IO.FileMode]::Create, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try { $stream.Write($buffer,0,$buffer.Length); $stream.Flush($true) } finally { $stream.Dispose() }
    if ([IO.File]::Exists($Path)) { [IO.File]::Replace($temp,$Path,[NullString]::Value) }
    else { [IO.File]::Move($temp,$Path) }
}

function Set-BbwFile([string]$Source, [string]$Target, [string]$ExpectedHash) {
    # Replace through a same-directory temporary file so interruption cannot leave
    # a half-written DLL that neither side of the journal can recognize.
    $temporary = $Target + '.bbw-' + [guid]::NewGuid().ToString('N') + '.tmp'
    try {
        [IO.File]::Copy($Source, $temporary)
        if ((Get-BbwHash $temporary) -ne $ExpectedHash) { throw "Source bytes changed while preparing $Target" }
        $stream=[IO.File]::Open($temporary,[IO.FileMode]::Open,[IO.FileAccess]::Write,[IO.FileShare]::None)
        try { $stream.Flush($true) } finally { $stream.Dispose() }
        if ([IO.File]::Exists($Target)) { [IO.File]::Replace($temporary, $Target, [NullString]::Value) }
        else { [IO.File]::Move($temporary, $Target) }
    } finally {
        if ([IO.File]::Exists($temporary)) { [IO.File]::Delete($temporary) }
    }
}

function Read-BbwJson([string]$Path) {
    if (Test-Path -LiteralPath $Path) { return (Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json) }
    return $null
}

function Test-BbwEditable($Entry) {
    if ($Entry.PSObject.Properties['PreserveOnRemove']) { return [bool]$Entry.PreserveOnRemove }
    # Compatibility with manifests written before editable-settings ownership.
    return ($Entry.Target.Replace('\','/') -in @('OptiScaler.ini','BepInEx/config/local.bigwalk.native-dlss.cfg'))
}

function Get-BbwRole($Entry) {
    if ($Entry.PSObject.Properties['Role']) { return [string]$Entry.Role }
    $relative=$Entry.Target.Replace('\','/')
    if ($relative -match '^(BepInEx/core/|dotnet/)' -or $relative -in @('winhttp.dll','doorstop_config.ini','.doorstop_version')) { return 'bepinex' }
    return 'plugin'
}

function Test-BbwOtherPlugins([string]$GamePath) {
    $managed=@(
        (Resolve-BbwPath $GamePath 'BepInEx/plugins/BigWalkDLSS/BigWalkDLSS.dll'),
        (Resolve-BbwPath $GamePath 'BepInEx/plugins/BigWalkDLSS/BigWalkDLSSNative.dll')
    )
    $pending=New-Object 'Collections.Generic.Stack[string]'
    foreach ($relative in @('BepInEx/plugins','BepInEx/patchers')) {
        $directory=Resolve-BbwPath $GamePath $relative
        if ([IO.Directory]::Exists($directory)) { $pending.Push($directory) }
    }
    while ($pending.Count) {
        foreach ($item in @(Get-ChildItem -LiteralPath $pending.Pop() -Force)) {
            # Unknown linked plugin trees also need their shared loader preserved.
            if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { return $true }
            if ($item.PSIsContainer) { $pending.Push($item.FullName) }
            elseif ($item.Extension -ieq '.dll' -and $managed -notcontains $item.FullName) { return $true }
        }
    }
    return $false
}

function Test-BbwGame([string]$GamePath) {
    foreach ($relative in @('Big Walk.exe','GameAssembly.dll','Big Walk_Data')) {
        $path = Resolve-BbwPath $GamePath $relative
        if (-not (Test-Path -LiteralPath $path)) { throw "This is not the Big Walk game folder: missing $relative. Select the folder containing Big Walk.exe." }
    }
    [void](Resolve-BbwPath $GamePath '.big-beautiful-walk/manifest.json')
}

function Assert-BbwGameClosed([string]$GamePath) {
    $expected=[IO.Path]::GetFullPath((Join-Path $GamePath 'Big Walk.exe'))
    foreach ($process in [Diagnostics.Process]::GetProcessesByName('Big Walk')) {
        try {
            try { $executable=$process.MainModule.FileName }
            catch {
                try { if ($process.HasExited) { continue } } catch { }
                throw 'Close Big Walk before installing or uninstalling. Setup cannot verify the location of a running Big Walk process; retry after closing it.'
            }
            if ([string]::IsNullOrWhiteSpace($executable)) { throw 'Close Big Walk before installing or uninstalling. Setup could not verify a running game process.' }
            if ([IO.Path]::GetFullPath($executable).Equals($expected,[StringComparison]::OrdinalIgnoreCase)) { throw 'Close Big Walk before installing or uninstalling, then try again. No game files were changed.' }
        } finally { $process.Dispose() }
    }
}

function Get-BbwSteamGames {
    $roots = New-Object 'Collections.Generic.List[string]'
    foreach ($key in @('HKCU:\Software\Valve\Steam','HKLM:\SOFTWARE\WOW6432Node\Valve\Steam')) {
        $item = Get-ItemProperty -LiteralPath $key -ErrorAction SilentlyContinue
        if ($item) {
            foreach ($name in @('SteamPath','InstallPath')) {
                if ($item.PSObject.Properties[$name] -and $item.$name) { $roots.Add([string]$item.$name) }
            }
        }
    }
    if (${env:ProgramFiles(x86)}) { $roots.Add((Join-Path ${env:ProgramFiles(x86)} 'Steam')) }
    $libraries = @($roots.ToArray())
    foreach ($root in @($roots.ToArray())) {
        $vdf = Join-Path $root 'steamapps/libraryfolders.vdf'
        if (Test-Path -LiteralPath $vdf) {
            $content = Get-Content -LiteralPath $vdf -Raw
            foreach ($match in [regex]::Matches($content, '"path"\s+"([^"]+)"')) { $libraries += $match.Groups[1].Value.Replace('\\','\') }
        }
    }
    foreach ($library in ($libraries | Select-Object -Unique)) {
        $game = Join-Path $library 'steamapps/common/Big Walk'
        if (Test-Path -LiteralPath (Join-Path $game 'Big Walk.exe')) { $game }
    }
}

function Expand-BbwZip([string]$Archive, [string]$Destination) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [IO.Compression.ZipFile]::OpenRead($Archive)
    try {
        # Validate every entry before extracting any bytes.
        foreach ($entry in $zip.Entries) {
            if ($entry.FullName -match '(^|[\\/])\.\.([\\/]|$)') { throw "Archive contains a traversal path: $($entry.FullName)" }
            [void](Resolve-BbwPath $Destination $entry.FullName)
            if (($entry.ExternalAttributes -shr 16 -band 61440) -eq 40960) { throw 'Archive contains a symbolic link.' }
        }
        foreach ($entry in $zip.Entries) {
            $target = Resolve-BbwPath $Destination $entry.FullName
            if ($entry.FullName.EndsWith('/') -or $entry.FullName.EndsWith('\')) { [void][IO.Directory]::CreateDirectory($target); continue }
            [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))
            [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $target, $false)
        }
    } finally { $zip.Dispose() }
}

function Invoke-BbwDownloadRequest([uri]$Uri, [string]$Destination, [int]$TimeoutMilliseconds) {
    $request=[Net.HttpWebRequest]::Create($Uri)
    $request.AllowAutoRedirect=$false
    $request.Method='GET'
    $request.UseDefaultCredentials=$false
    $request.Credentials=$null
    $request.PreAuthenticate=$false
    $request.Timeout=$TimeoutMilliseconds
    $request.ReadWriteTimeout=$TimeoutMilliseconds
    $request.UserAgent='BigBeautifulWalk-Setup/0.6.0'
    $response=$null
    $elapsed=[Diagnostics.Stopwatch]::StartNew()
    try {
        $response=$request.GetResponse()
        $status=[int]$response.StatusCode
        if ($status -in @(301,302,303,307,308)) { return [pscustomobject]@{Status=$status;Location=[string]$response.Headers['Location']} }
        if ($status -ne 200) { throw 'Unexpected HTTP response.' }
        $responseStream=$response.GetResponseStream()
        $output=[IO.File]::Open($Destination,[IO.FileMode]::Create,[IO.FileAccess]::Write,[IO.FileShare]::None)
        try {
            $buffer=New-Object byte[] 65536
            while ($true) {
                $remaining=$TimeoutMilliseconds-[int]$elapsed.ElapsedMilliseconds
                if ($remaining -le 0) { throw 'Download timed out.' }
                if ($responseStream.CanTimeout) { $responseStream.ReadTimeout=$remaining }
                $read=$responseStream.Read($buffer,0,$buffer.Length)
                if (-not $read) { break }
                $output.Write($buffer,0,$read)
            }
        } finally { $output.Dispose(); $responseStream.Dispose() }
        return [pscustomobject]@{Status=200;Location=$null}
    } catch {
        # Asset redirects can contain signed query strings; never echo them.
        throw "HTTPS download from $($Uri.DnsSafeHost) failed. Check your connection and retry; no game files were changed."
    } finally { if ($response) { $response.Dispose() } }
}

function Receive-BbwDownload([uri]$Uri, [string]$Destination, [scriptblock]$Transport=$null) {
    [Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12
    $current=$Uri
    $elapsed=[Diagnostics.Stopwatch]::StartNew()
    $redirects=0
    while ($true) {
        if ($current.Scheme -ne 'https' -or $current.UserInfo) { throw 'Dependency download rejected: every URL must use anonymous HTTPS.' }
        $remaining=180000-[int]$elapsed.ElapsedMilliseconds
        if ($remaining -le 0) { throw 'Dependency download exceeded the 180-second timeout. Retry when your connection is ready.' }
        # Every hop is a new anonymous request: no cookies or authorization
        # headers cross origins. The locked SHA256 remains the content authority.
        if ($Transport) { $response=& $Transport $current $Destination $remaining }
        else { $response=Invoke-BbwDownloadRequest $current $Destination $remaining }
        if ($response.Status -eq 200) { return }
        if ($response.Status -notin @(301,302,303,307,308)) { throw 'Dependency download returned an unsupported HTTP status.' }
        if ($redirects -ge 8) { throw 'Dependency download exceeded eight HTTPS redirects. Obtain the release dependencies manually and retry offline.' }
        if ([string]::IsNullOrWhiteSpace($response.Location)) { throw 'Dependency download redirect has no destination.' }
        try { $next=New-Object Uri($current,[string]$response.Location) }
        catch { throw 'Dependency download redirect has an invalid destination.' }
        if ($next.Scheme -ne 'https' -or $next.UserInfo) { throw 'Dependency download redirect rejected: every URL must use anonymous HTTPS.' }
        $current=$next
        $redirects++
    }
}

function Get-BbwStage([string]$PackagePath, [string]$Stage, [string]$DependencyDirectory, [string[]]$Features) {
    $items = New-Object 'Collections.Generic.List[object]'
    foreach ($name in @('BigWalkDLSS.dll','BigWalkDLSSNative.dll')) {
        $source = Resolve-BbwPath $PackagePath "payload/BigWalkDLSS/$name"
        if (-not [IO.File]::Exists($source)) { throw "Release payload is incomplete: missing $name. Extract the complete release ZIP and run Setup.cmd again." }
        $items.Add([pscustomobject]@{ Source=$source; Target="BepInEx/plugins/BigWalkDLSS/$name"; FreshOnly=$false; Role='plugin' })
    }
    $lock = Read-BbwJson (Resolve-BbwPath $PackagePath 'packaging/dependencies.lock.json')
    if (-not $lock -or -not $lock.PSObject.Properties['SchemaVersion'] -or $lock.SchemaVersion -ne 1 -or -not $lock.PSObject.Properties['Dependencies']) { throw 'Dependency lock is missing or incomplete. This alpha cannot install until official dependency URLs and SHA256 hashes are supplied in packaging/dependencies.lock.json.' }
    foreach ($feature in $Features) {
        if (-not @($lock.Dependencies | Where-Object { $_.Feature -eq $feature }).Count) { throw "Optional feature '$feature' has no verified runtime in this release. Core setup provides DLSS Super Resolution and Advanced Graphics. See docs/DEPENDENCIES.md for manual FG/NR setup; no game files were changed." }
    }
    $selected = @($lock.Dependencies | Where-Object { $_.Required -or $_.Feature -eq 'core' -or $Features -contains $_.Feature })
    foreach ($id in @('bepinex','nvngx_dlss')) {
        if (-not @($selected | Where-Object { $_.Id -eq $id }).Count) { throw "Required locked dependency '$id' is unavailable. Obtain a complete release; no game files were changed." }
    }
    $seenIds = @{}
    foreach ($dep in $selected) {
        if ($dep.Id -notmatch '^[a-zA-Z0-9_-]+$' -or $seenIds.ContainsKey($dep.Id)) { throw 'Invalid or duplicate dependency ID.' }
        $seenIds[$dep.Id] = $true
        $uri = $null
        if (-not [uri]::TryCreate([string]$dep.Url, [UriKind]::Absolute, [ref]$uri) -or $uri.Scheme -ne 'https' -or $uri.UserInfo -or $dep.SHA256 -notmatch '^[a-fA-F0-9]{64}$' -or $dep.Archive -notin @('zip','file') -or -not @($dep.Files).Count) { throw "Locked dependency '$($dep.Id)' lacks a valid HTTPS URL, SHA256 hash or file map." }
        $download = Resolve-BbwPath $Stage "$($dep.Id).$($dep.Archive)"
        if ($DependencyDirectory) {
            $offline = Resolve-BbwPath $DependencyDirectory "$($dep.Id).$($dep.Archive)"
            if (-not [IO.File]::Exists($offline)) { throw "Offline dependency is missing: $($dep.Id).$($dep.Archive). No game files were changed." }
            [IO.File]::Copy($offline, $download)
        } else {
            Receive-BbwDownload $uri $download
        }
        if ((Get-BbwHash $download) -ne $dep.SHA256.ToLowerInvariant()) { throw "SHA256 mismatch for $($dep.Id). Download rejected; no game files were changed." }
        $expanded = Resolve-BbwPath $Stage $dep.Id
        [void][IO.Directory]::CreateDirectory($expanded)
        if ($dep.Archive -eq 'zip') { Expand-BbwZip $download $expanded }
        foreach ($map in $dep.Files) {
            $source = $download
            if ($dep.Archive -eq 'zip') { $source = Resolve-BbwPath $expanded $map.Source }
            if (-not [IO.File]::Exists($source)) { throw "Locked file missing from $($dep.Id): $($map.Source)" }
            $items.Add([pscustomobject]@{ Source=$source; Target=[string]$map.Target; FreshOnly=$false; Role=[string]$dep.Id })
        }
    }
    foreach ($default in @(@('local.bigwalk.native-dlss.cfg','BepInEx/config/local.bigwalk.native-dlss.cfg'),@('OptiScaler.ini','OptiScaler.ini'))) {
        if ($default[0] -eq 'OptiScaler.ini' -and $Features -notcontains 'fg') { continue }
        $source = Resolve-BbwPath $PackagePath ('payload/defaults/' + $default[0])
        if ([IO.File]::Exists($source)) { $items.Add([pscustomobject]@{ Source=$source; Target=$default[1]; FreshOnly=$true; Role='config' }) }
    }
    return $items.ToArray()
}

function Restore-BbwTransaction([string]$GamePath) {
    $state = Resolve-BbwPath $GamePath '.big-beautiful-walk'
    $journalPath = Resolve-BbwPath $state 'journal.json'
    $journal = Read-BbwJson $journalPath
    if (-not $journal) { return }
    # Refuse all recovery if even one file changed outside this transaction.
    foreach ($action in @($journal.Actions)) {
        $target = Resolve-BbwPath $GamePath $action.Target
        $hash = Get-BbwHash $target
        if ($hash -ne $action.BeforeHash -and $hash -ne $action.AfterHash) { throw "Interrupted setup needs manual attention: '$($action.Target)' changed after the transaction. Preserve this file and .big-beautiful-walk; see Diagnose." }
        if ($action.BeforeHash) {
            $backup = Resolve-BbwPath $state $action.TransactionBackup
            if ((Get-BbwHash $backup) -ne $action.BeforeHash) { throw "Recovery backup is missing or damaged for $($action.Target). Preserve .big-beautiful-walk and restore your backup." }
        }
    }
    $actions = @($journal.Actions)
    [array]::Reverse($actions)
    foreach ($action in $actions) {
        $target = Resolve-BbwPath $GamePath $action.Target
        if ((Get-BbwHash $target) -eq $action.BeforeHash) { continue }
        if ($action.BeforeHash) { Set-BbwFile (Resolve-BbwPath $state $action.TransactionBackup) $target $action.BeforeHash }
        elseif ([IO.File]::Exists($target)) { [IO.File]::Delete($target) }
    }
    $manifest = Resolve-BbwPath $state 'manifest.json'
    if ($journal.PreviousManifest) { [IO.File]::WriteAllText($manifest, [string]$journal.PreviousManifest, (New-Object Text.UTF8Encoding($false))) }
    elseif ([IO.File]::Exists($manifest)) { [IO.File]::Delete($manifest) }
    [IO.File]::Delete($journalPath)
}

function Invoke-BbwSetup {
    [CmdletBinding()]
    param([Parameter(Mandatory=$true)][string]$GamePath,
          [ValidateSet('Install','Uninstall','Diagnose')][string]$Mode='Install',
          [string]$PackagePath=(Split-Path $PSScriptRoot -Parent),
          [string]$DependencyDirectory, [string[]]$Features=@(), [int]$FaultAfter=0)
    Test-BbwGame $GamePath
    $GamePath = [IO.Path]::GetFullPath($GamePath)
    $state = Resolve-BbwPath $GamePath '.big-beautiful-walk'
    $manifestPath = Resolve-BbwPath $state 'manifest.json'
    $journalPath = Resolve-BbwPath $state 'journal.json'
    if ($Mode -eq 'Diagnose') {
        $messages = New-Object 'Collections.Generic.List[string]'
        $messages.Add("Game folder: $GamePath")
        if (Test-Path -LiteralPath $journalPath) { $messages.Add('Interrupted transaction detected. Install/Repair or Uninstall will attempt safe rollback first.') }
        $manifest = Read-BbwJson $manifestPath
        if (-not $manifest) { $messages.Add('Big Beautiful Walk is not installed by this installer.') }
        else {
            foreach ($entry in @($manifest.Entries)) {
                $entryPath=Resolve-BbwPath $GamePath $entry.Target
                if (Test-BbwEditable $entry) { $messages.Add("Editable settings preserved: $($entry.Target)") }
                elseif ((Get-BbwHash $entryPath) -eq $entry.InstalledHash) { $messages.Add("OK: $($entry.Target)") }
                elseif (-not (Test-Path -LiteralPath $entryPath)) { $messages.Add("MISSING: $($entry.Target). Run Install/Repair to restore verified release bytes, or Uninstall to remove the remaining owned files.") }
                else { $messages.Add("CONFLICT: $($entry.Target) changed. Preserve your changes and restore the installed bytes before setup can modify it.") }
            }
        }
        foreach ($name in @('dxgi.dll','version.dll','winhttp.dll','nvngx_dlss.dll','nvngx_dlssnr.dll')) {
            $path = Resolve-BbwPath $GamePath $name
            if ([IO.File]::Exists($path)) { $messages.Add("Present: $name (SHA256 $(Get-BbwHash $path))") }
        }
        return ($messages -join [Environment]::NewLine)
    }
    Assert-BbwGameClosed $GamePath
    # A process-wide file lock prevents two copies of Setup touching one game.
    $mutexName = Get-BbwMutexName $GamePath
    $mutex = New-Object Threading.Mutex($false, $mutexName)
    $locked = $false
    $stage = Join-Path ([IO.Path]::GetTempPath()) ('bbw-' + [guid]::NewGuid().ToString('N'))
    try {
        try { $locked = $mutex.WaitOne(0) } catch [Threading.AbandonedMutexException] { $locked=$true }
        if (-not $locked) { throw 'Another setup is using this game. Close it and try again.' }
        Restore-BbwTransaction $GamePath
        $manifest = Read-BbwJson $manifestPath
        $owned = @{}
        if ($manifest) {
            foreach ($entry in @($manifest.Entries)) {
                if ($owned.ContainsKey($entry.Target)) { throw 'Installed manifest contains duplicate paths.' }
                $owned[$entry.Target] = $entry
                $ownedPath=Resolve-BbwPath $GamePath $entry.Target
                $currentHash=Get-BbwHash $ownedPath
                if (-not (Test-BbwEditable $entry) -and ([IO.Directory]::Exists($ownedPath) -or ($currentHash -and $currentHash -ne $entry.InstalledHash))) { throw "Setup stopped: '$($entry.Target)' was changed after installation. Preserve your changes and restore the installed version before retrying; Diagnose lists affected files." }
                if ($entry.OriginalHash -and (Get-BbwHash (Resolve-BbwPath $state $entry.Backup)) -ne $entry.OriginalHash) { throw "Original backup is damaged for $($entry.Target). Preserve .big-beautiful-walk and restore your backup." }
            }
        }
        if ($Mode -eq 'Uninstall' -and -not $manifest) { return 'Nothing to uninstall. Existing mods and settings were preserved.' }
        [void][IO.Directory]::CreateDirectory($stage)
        $plans = New-Object 'Collections.Generic.List[object]'
        $entries = New-Object 'Collections.Generic.List[object]'
        if ($Mode -eq 'Install') {
            $items = @(Get-BbwStage $PackagePath $stage $DependencyDirectory $Features)
            $targets = @{}
            $payloadIndex=0
            foreach ($item in $items) {
                # Freeze package payloads alongside the verified downloads before mutation.
                $sourceHash=Get-BbwHash $item.Source
                $frozen=Resolve-BbwPath $stage ("payload-$payloadIndex.bin")
                [IO.File]::Copy($item.Source,$frozen)
                if ((Get-BbwHash $frozen) -ne $sourceHash) { throw 'Release payload changed while preparing setup. Extract it again and retry.' }
                $item.Source=$frozen
                $payloadIndex++
                $target = Resolve-BbwPath $GamePath $item.Target
                if ([IO.Directory]::Exists($target)) { throw "A directory blocks the expected file '$($item.Target)'. Move that directory manually and retry; no game files were changed." }
                # Installer state and game binaries are never valid dependency targets.
                if ($item.Target -match '(^|[\\/])\.big-beautiful-walk([\\/]|$)' -or $item.Target -match '(^|[\\/])(GameAssembly\.dll|UnityPlayer\.dll|Big Walk\.exe)([\\/]|$)') { throw "Disallowed dependency target: $($item.Target)" }
                $key = $target.ToLowerInvariant()
                if ($targets.ContainsKey($key)) { throw "Duplicate package target: $($item.Target)" }
                $targets[$key]=$true
                $before = Get-BbwHash $target
                $after = Get-BbwHash $item.Source
                $prior = $owned[$item.Target]
                if ($item.FreshOnly -and $before) {
                    if ($prior) { $entries.Add($prior) }
                    continue
                }
                if ($before -and -not $prior) {
                    if ($before -eq $after) { continue }
                    throw "Existing file collision: '$($item.Target)' belongs to another installation or mod. Move that mod using its own uninstaller or select a clean game folder; setup will not overwrite it."
                }
                $backup = $null; $original = $null
                if ($prior) { $backup=$prior.Backup; $original=$prior.OriginalHash }
                $entries.Add([pscustomobject]@{ Target=$item.Target; InstalledHash=$after; OriginalHash=$original; Backup=$backup; PreserveOnRemove=[bool]$item.FreshOnly; Role=$item.Role })
                if ($before -ne $after) { $plans.Add([pscustomobject]@{ Target=$item.Target; BeforeHash=$before; AfterHash=$after; Source=$item.Source; TransactionBackup=$null }) }
            }
            # Unrequested files from an earlier version remain owned until uninstall.
            foreach ($entry in @($owned.Values)) { if (-not @($entries | Where-Object { $_.Target -eq $entry.Target }).Count) { $entries.Add($entry) } }
            foreach ($injector in @('dxgi.dll','version.dll','winhttp.dll','nvngx_dlss.dll','nvngx_dlssnr.dll')) {
                $path = Resolve-BbwPath $GamePath $injector
                if ([IO.File]::Exists($path) -and -not $owned.ContainsKey($injector) -and -not $targets.ContainsKey($path.ToLowerInvariant())) { throw "Unknown injector/runtime '$injector' is present. Use Diagnose and the existing mod's uninstall instructions before installing; no files were changed." }
            }
        } else {
            $otherPlugins=Test-BbwOtherPlugins $GamePath
            foreach ($entry in @($manifest.Entries)) {
                if ((Test-BbwEditable $entry) -or ($otherPlugins -and (Get-BbwRole $entry) -eq 'bepinex')) { continue }
                $beforeHash=Get-BbwHash (Resolve-BbwPath $GamePath $entry.Target)
                if (-not $beforeHash -and -not $entry.OriginalHash) { continue }
                $source = $null
                if ($entry.OriginalHash) { $source = Resolve-BbwPath $state $entry.Backup }
                $plans.Add([pscustomobject]@{ Target=$entry.Target; BeforeHash=$beforeHash; AfterHash=$entry.OriginalHash; Source=$source; TransactionBackup=$null })
            }
        }
        # All downloads, paths, collisions and installed hashes have passed before here.
        Assert-BbwGameClosed $GamePath
        [void][IO.Directory]::CreateDirectory($state)
        $transaction = 'transactions/' + [guid]::NewGuid().ToString('N')
        [void][IO.Directory]::CreateDirectory((Resolve-BbwPath $state $transaction))
        $index=0
        foreach ($plan in $plans) {
            if ($plan.BeforeHash) {
                $plan.TransactionBackup = "$transaction/$index.bin"
                [IO.File]::Copy((Resolve-BbwPath $GamePath $plan.Target), (Resolve-BbwPath $state $plan.TransactionBackup))
                if ((Get-BbwHash (Resolve-BbwPath $state $plan.TransactionBackup)) -ne $plan.BeforeHash) { throw 'File changed while setup was preparing. Close the game and retry.' }
            }
            $index++
        }
        $previous = $null
        if ([IO.File]::Exists($manifestPath)) { $previous=[IO.File]::ReadAllText($manifestPath) }
        Write-BbwJson $journalPath ([pscustomobject]@{ SchemaVersion=1; PreviousManifest=$previous; Actions=@($plans.ToArray()) })
        $applied=0
        foreach ($plan in $plans) {
            $target = Resolve-BbwPath $GamePath $plan.Target
            if ((Get-BbwHash $target) -ne $plan.BeforeHash) { throw "File changed during setup: $($plan.Target)" }
            [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))
            if ($plan.Source) { Set-BbwFile $plan.Source $target $plan.AfterHash }
            else { [IO.File]::Delete($target) }
            if ((Get-BbwHash $target) -ne $plan.AfterHash) { throw "Write verification failed: $($plan.Target)" }
            $applied++
            if ($FaultAfter -gt 0 -and $applied -eq $FaultAfter) { throw 'Injected transaction failure.' }
        }
        if ($Mode -eq 'Install') { Write-BbwJson $manifestPath ([pscustomobject]@{ SchemaVersion=1; Version='0.6.0'; Entries=@($entries.ToArray()) }) }
        elseif ([IO.File]::Exists($manifestPath)) { [IO.File]::Delete($manifestPath) }
        [IO.File]::Delete($journalPath)
        if ($Mode -eq 'Install') {
            $message='Install/Repair completed. Existing settings and other mods were preserved. Start Big Walk through Steam when ready.'
            if ($Features -contains 'fg') { $message+=' The optional Frame Generation bridge was installed. Fresh defaults leave Frame Generation off; follow docs/DEPENDENCIES.md to configure it.' }
            return $message
        }
        return 'Uninstall completed. Your settings, saves and other mods were preserved; installer backups remain in .big-beautiful-walk.'
    } catch {
        $failure = $_.Exception.Message
        if ($locked) {
            try { Restore-BbwTransaction $GamePath } catch { throw "$failure Recovery stopped safely: $($_.Exception.Message)" }
        }
        throw $failure
    } finally {
        if ($locked) { $mutex.ReleaseMutex() }
        $mutex.Dispose()
        $tempRoot=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\','/') + [IO.Path]::DirectorySeparatorChar
        if (-not [IO.Path]::GetFullPath($stage).StartsWith($tempRoot,[StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe temporary cleanup path.' }
        if ([IO.Directory]::Exists($stage)) { Remove-Item -LiteralPath $stage -Recurse -Force }
    }
}

Export-ModuleMember -Function Invoke-BbwSetup,Get-BbwSteamGames
