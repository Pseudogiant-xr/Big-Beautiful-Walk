Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

function Get-BbwHash([string]$Path) {
    if (-not [IO.File]::Exists($Path)) { return $null }
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Get-BbwFinalPath([string]$Path) {
    # One spelling per file or folder: junctions, symbolic links, subst drives and 8.3 names
    # all resolve to the same final path, so the running-game check and the installer lock
    # cannot be bypassed by choosing an alias of the game folder.
    $full=[IO.Path]::GetFullPath($Path)
    if (-not ('BigBeautifulWalk.FinalPath' -as [type])) {
        Add-Type -Language CSharp -TypeDefinition @'
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
namespace BigBeautifulWalk {
    public static class FinalPath {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateFileW(string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint GetFinalPathNameByHandleW(IntPtr handle, StringBuilder path, uint length, uint flags);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);
        public static string Resolve(string path) {
            // No access rights are requested; backup semantics allow opening directories.
            IntPtr handle = CreateFileW(path, 0, 7, IntPtr.Zero, 3, 0x02000000, IntPtr.Zero);
            if (handle == new IntPtr(-1)) throw new Win32Exception(Marshal.GetLastWin32Error());
            try {
                var buffer = new StringBuilder(1024);
                uint length = GetFinalPathNameByHandleW(handle, buffer, (uint)buffer.Capacity, 0);
                if (length > buffer.Capacity) {
                    buffer = new StringBuilder((int)length);
                    length = GetFinalPathNameByHandleW(handle, buffer, (uint)buffer.Capacity, 0);
                }
                if (length == 0 || length > buffer.Capacity) throw new Win32Exception(Marshal.GetLastWin32Error());
                string result = buffer.ToString();
                if (result.StartsWith(@"\\?\UNC\", StringComparison.Ordinal)) return @"\\" + result.Substring(8);
                if (result.StartsWith(@"\\?\", StringComparison.Ordinal)) return result.Substring(4);
                return result;
            } finally { CloseHandle(handle); }
        }
    }
}
'@
    }
    return [BigBeautifulWalk.FinalPath]::Resolve($full).TrimEnd('\','/')
}

function Get-BbwMutexName([string]$GamePath) {
    $normalized=(Get-BbwFinalPath $GamePath).ToUpperInvariant()
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
    # Check only the components below the chosen root. Folders above it (a Steam library
    # behind a junction, a synced Desktop) are the user's choice and are not written to.
    $cursor = $path
    while ($cursor -and $cursor.Length -gt $base.Length) {
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
        (Resolve-BbwPath $GamePath 'BepInEx/plugins/BigWalkDLSS/BigWalkDLSSNative.dll'),
        (Resolve-BbwPath $GamePath 'BepInEx/plugins/BigWalkDLSS/BigWalkBridgeNative.dll')
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

function Get-BbwDisplayDeviceIds {
    # Present display adapters first: Windows keeps class entries for removed cards. A present
    # Enum\PCI instance of the display class has a live Control key. .NET registry APIs only:
    # the GUI worker runspace runs without module autoload.
    $present=New-Object 'Collections.Generic.List[string]'
    try {
        $pci=[Microsoft.Win32.Registry]::LocalMachine.OpenSubKey('SYSTEM\CurrentControlSet\Enum\PCI')
        if ($pci) {
            try {
                foreach ($device in $pci.GetSubKeyNames()) {
                    $deviceKey=$null
                    try { $deviceKey=$pci.OpenSubKey($device) } catch { continue }
                    if (-not $deviceKey) { continue }
                    try {
                        foreach ($instance in $deviceKey.GetSubKeyNames()) {
                            $instanceKey=$null
                            try { $instanceKey=$deviceKey.OpenSubKey($instance) } catch { continue }
                            if (-not $instanceKey) { continue }
                            try {
                                if ([string]$instanceKey.GetValue('ClassGUID') -ne '{4d36e968-e325-11ce-bfc1-08002be10318}') { continue }
                                $control=$instanceKey.OpenSubKey('Control')
                                if ($control) { $control.Dispose(); $present.Add('PCI\' + $device); break }
                            } finally { $instanceKey.Dispose() }
                        }
                    } finally { $deviceKey.Dispose() }
                }
            } finally { $pci.Dispose() }
        }
    } catch { }
    if ($present.Count) { return $present.ToArray() }
    $ids=New-Object 'Collections.Generic.List[string]'
    $class=$null
    try { $class=[Microsoft.Win32.Registry]::LocalMachine.OpenSubKey('SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}') } catch { }
    if (-not $class) { return $ids.ToArray() }
    try {
        foreach ($name in $class.GetSubKeyNames()) {
            # Adapter instances are NNNN; the Properties subkey is not readable.
            if ($name -notmatch '^\d{4}$') { continue }
            $adapter=$null
            try { $adapter=$class.OpenSubKey($name) } catch { continue }
            if (-not $adapter) { continue }
            try {
                foreach ($value in @($adapter.GetValue('MatchingDeviceId'),$adapter.GetValue('HardwareID'))) {
                    foreach ($id in @($value)) { if ($id) { $ids.Add([string]$id) } }
                }
            } finally { $adapter.Dispose() }
        }
    } finally { $class.Dispose() }
    return $ids.ToArray()
}

function Get-BbwGpuPath([ValidateSet('Auto','Nvidia','Bridge')][string]$Override='Auto', [string[]]$DeviceIds) {
    # 'nvidia' uses DLSS; 'bridge' is the AMD/Intel path through OptiScaler.
    if ($Override -eq 'Nvidia') { return 'nvidia' }
    if ($Override -eq 'Bridge') { return 'bridge' }
    if (-not $PSBoundParameters.ContainsKey('DeviceIds')) { $DeviceIds=@(Get-BbwDisplayDeviceIds) }
    $pci=@($DeviceIds | Where-Object { [string]$_ -match 'ven_[0-9a-f]{4}' })
    foreach ($id in $pci) { if ([string]$id -match 'ven_10de') { return 'nvidia' } }
    # No readable PCI display adapter: keep the established NVIDIA path, matching the plugin,
    # which then trusts the GPU the game reports. -GpuPath Bridge overrides this.
    if ($pci.Count -eq 0) { return 'nvidia' }
    return 'bridge'
}

function Test-BbwHybridGpu([string[]]$DeviceIds) {
    # An NVIDIA GPU next to an AMD or Intel one (most often a laptop). Windows chooses which runs the game.
    if (-not $PSBoundParameters.ContainsKey('DeviceIds')) { $DeviceIds=@(Get-BbwDisplayDeviceIds) }
    $nvidia=@($DeviceIds | Where-Object { [string]$_ -match 'ven_10de' }).Count -gt 0
    $other=@($DeviceIds | Where-Object { [string]$_ -match 'ven_(1002|8086)' }).Count -gt 0
    return ($nvidia -and $other)
}

function Update-BbwBridgeSettings([string]$GamePath, [switch]$SpoofingOnly, [string]$StateRelative='.big-beautiful-walk') {
    # OptiScaler reads OptiScaler.ini before the plugin starts. One kept from an NVIDIA install can ask
    # for DLSS, NVIDIA Frame Generation or Neural Rendering, which AMD and Intel GPUs cannot run and
    # which can stop the game. GPU spoofing (on unless set false) would also make an AMD/Intel GPU look
    # like NVIDIA to the plugin. -SpoofingOnly is for hybrid PCs that keep the NVIDIA path. Returns what changed.
    $IniPath=Resolve-BbwPath $GamePath 'OptiScaler.ini'
    if (-not [IO.File]::Exists($IniPath)) { return @() }
    $bytes=[IO.File]::ReadAllBytes($IniPath)
    if ($bytes.Length -ge 2 -and (($bytes[0] -eq 0xFF -and $bytes[1] -eq 0xFE) -or ($bytes[0] -eq 0xFE -and $bytes[1] -eq 0xFF))) { return @() }
    $bom=$bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
    $skip=if ($bom) { 3 } else { 0 }
    # Strict decoding: an ANSI file with accented characters must not be rewritten as replacement characters.
    $text=(New-Object Text.UTF8Encoding($false,$true)).GetString($bytes,$skip,$bytes.Length-$skip)
    $lines=[regex]::Split($text,'(?<=\n)')
    $pattern='^(\s*([^=;#\[\s][^=]*?)\s*=\s*)([^;#\r\n]*?)(\s*(?:[;#].*)?\r?\n?)$'
    $values=@{}; $section=''
    foreach ($line in $lines) {
        if ($line -match '^\s*\[([^\]]+)\]') { $section=$Matches[1].Trim(); continue }
        if ($line -match $pattern) { $values[("$section|" + $Matches[2]).ToLowerInvariant()]=$Matches[3] }
    }
    $set=@{}; $changed=New-Object 'Collections.Generic.List[string]'
    if (-not $SpoofingOnly) {
        if ($values['upscalers|dx11upscaler'] -in @('dlss','dlss_12')) { $set['upscalers|dx11upscaler']='fsr31'; $changed.Add('FSR 3.1 selected instead of DLSS') }
        if ($values['framegen|enabled'] -eq 'true' -and $values['framegen|fgoutput'] -eq 'DLSSG') { $set['framegen|enabled']='false'; $changed.Add('NVIDIA Frame Generation turned off') }
        if ($values['dlssnr|enabled'] -eq 'true') { $set['dlssnr|enabled']='false'; $changed.Add('Neural Rendering turned off') }
    }
    $addSpoofing=-not $values.ContainsKey('spoofing|dxgi')
    if (-not $addSpoofing -and $values['spoofing|dxgi'] -ne 'false') { $set['spoofing|dxgi']='false' }
    if ($addSpoofing -or $set.ContainsKey('spoofing|dxgi')) { $changed.Add('OptiScaler GPU spoofing turned off') }
    if (-not $changed.Count) { return @() }
    $newline=if ($text.Contains("`r`n")) { "`r`n" } else { "`n" }
    $out=New-Object Text.StringBuilder; $section=''
    foreach ($line in $lines) {
        if ($line -match '^\s*\[([^\]]+)\]') { $section=$Matches[1].Trim() }
        elseif ($line -match $pattern) {
            $key=("$section|" + $Matches[2]).ToLowerInvariant()
            if ($set.ContainsKey($key)) { $line=$Matches[1] + $set[$key] + $Matches[4] }
        }
        [void]$out.Append($line)
        if ($addSpoofing -and $line -match '^\s*\[\s*Spoofing\s*\]') {
            if (-not $line.EndsWith("`n")) { [void]$out.Append($newline) }
            [void]$out.Append('Dxgi=false' + $newline); $addSpoofing=$false
        }
    }
    if ($addSpoofing) {
        if ($out.Length -and -not $out.ToString().EndsWith("`n")) { [void]$out.Append($newline) }
        [void]$out.Append('[Spoofing]' + $newline + 'Dxgi=false' + $newline)
    }
    $backupDirectory=Resolve-BbwPath $GamePath "$StateRelative/config-backups"
    [void][IO.Directory]::CreateDirectory($backupDirectory)
    $backup=Resolve-BbwPath $GamePath ("$StateRelative/config-backups/OptiScaler-" + [DateTime]::Now.ToString('yyyyMMdd-HHmmss-fff') + '.ini')
    [IO.File]::Copy($IniPath,$backup,$false)
    $body=(New-Object Text.UTF8Encoding($false)).GetBytes($out.ToString())
    $next=New-Object byte[] ($skip + $body.Length)
    if ($bom) { $next[0]=0xEF; $next[1]=0xBB; $next[2]=0xBF }
    [Array]::Copy($body,0,$next,$skip,$body.Length)
    $temp=Resolve-BbwPath $GamePath ('OptiScaler.ini.' + [guid]::NewGuid().ToString('N') + '.tmp')
    # CreateNew never follows or replaces an existing path.
    $stream=[IO.File]::Open($temp,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
    try { $stream.Write($next,0,$next.Length) } finally { $stream.Dispose() }
    try { [IO.File]::Replace($temp,$IniPath,[NullString]::Value) }
    finally { if ([IO.File]::Exists($temp)) { [IO.File]::Delete($temp) } }
    return $changed.ToArray()
}

function Test-BbwGame([string]$GamePath) {
    foreach ($relative in @('Big Walk.exe','GameAssembly.dll','Big Walk_Data')) {
        $path = Resolve-BbwPath $GamePath $relative
        if (-not (Test-Path -LiteralPath $path)) { throw "This is not the Big Walk game folder: missing $relative. Select the folder containing Big Walk.exe." }
    }
    [void](Resolve-BbwPath $GamePath '.big-beautiful-walk/manifest.json')
}

function Assert-BbwGameClosed([string]$GamePath) {
    $expected=Get-BbwFinalPath (Join-Path $GamePath 'Big Walk.exe')
    foreach ($process in [Diagnostics.Process]::GetProcessesByName('Big Walk')) {
        try {
            try { $executable=$process.MainModule.FileName }
            catch {
                try { if ($process.HasExited) { continue } } catch { }
                throw 'Close Big Walk before installing or uninstalling. Setup cannot verify the location of a running Big Walk process; retry after closing it.'
            }
            if ([string]::IsNullOrWhiteSpace($executable)) { throw 'Close Big Walk before installing or uninstalling. Setup could not verify a running game process.' }
            try { $running=Get-BbwFinalPath $executable } catch { $running=[IO.Path]::GetFullPath($executable) }
            if ($running.Equals($expected,[StringComparison]::OrdinalIgnoreCase)) { throw 'Close Big Walk before installing or uninstalling, then try again. No game files were changed.' }
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

function Invoke-BbwDownloadRequest([uri]$Uri, [string]$Destination, [int]$TimeoutMilliseconds, [int]$TotalMilliseconds=1800000) {
    # $TimeoutMilliseconds bounds each wait for a response or for more data, so a slow but
    # moving download continues. $TotalMilliseconds is a generous cap for the whole transfer.
    $request=[Net.HttpWebRequest]::Create($Uri)
    $request.AllowAutoRedirect=$false
    $request.Method='GET'
    $request.UseDefaultCredentials=$false
    $request.Credentials=$null
    $request.PreAuthenticate=$false
    $request.Timeout=$TimeoutMilliseconds
    $request.ReadWriteTimeout=$TimeoutMilliseconds
    $request.UserAgent='BigBeautifulWalk-Setup/0.7.0'
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
            if ($responseStream.CanTimeout) { $responseStream.ReadTimeout=$TimeoutMilliseconds }
            while ($true) {
                if ($elapsed.ElapsedMilliseconds -ge $TotalMilliseconds) { throw 'Download timed out.' }
                $read=$responseStream.Read($buffer,0,$buffer.Length)
                if (-not $read) { break }
                $output.Write($buffer,0,$read)
            }
        } finally { $output.Dispose(); $responseStream.Dispose() }
        return [pscustomobject]@{Status=200;Location=$null}
    } catch {
        # Asset redirects can contain signed query strings; never echo them.
        throw "HTTPS download from $($Uri.DnsSafeHost) failed or stalled. Check your connection and retry, or install offline with -DependencyDirectory (see docs/INSTALLATION.md); no game files were changed."
    } finally { if ($response) { $response.Dispose() } }
}

function Receive-BbwDownload([uri]$Uri, [string]$Destination, [scriptblock]$Transport=$null) {
    [Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12
    $current=$Uri
    $elapsed=[Diagnostics.Stopwatch]::StartNew()
    $redirects=0
    while ($true) {
        if ($current.Scheme -ne 'https' -or $current.UserInfo) { throw 'Dependency download rejected: every URL must use anonymous HTTPS.' }
        $remaining=1800000-[int]$elapsed.ElapsedMilliseconds
        if ($remaining -le 0) { throw 'Dependency download exceeded 30 minutes. Retry when your connection is ready, or install offline with -DependencyDirectory (see docs/INSTALLATION.md).' }
        # Every hop is a new anonymous request: no cookies or authorization
        # headers cross origins. The locked SHA256 remains the content authority.
        $stall=[Math]::Min(60000,$remaining)
        if ($Transport) { $response=& $Transport $current $Destination $stall }
        else { $response=Invoke-BbwDownloadRequest $current $Destination $stall $remaining }
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

function Get-BbwRepairFeatures([object[]]$Dependencies, [string[]]$Requested, [string[]]$OwnedRoles) {
    $selected = New-Object 'Collections.Generic.List[string]'
    foreach ($feature in $Requested) {
        if ($selected -notcontains $feature) { $selected.Add($feature) }
    }
    foreach ($dependency in $Dependencies) {
        if ($dependency.Feature -and $dependency.Feature -ne 'core' -and $OwnedRoles -contains $dependency.Id -and $selected -notcontains $dependency.Feature) {
            $selected.Add([string]$dependency.Feature)
        }
    }
    # The NR model uses the optional bridge and its disabled-by-default config.
    if ($selected -contains 'nr' -and $selected -notcontains 'fg') { $selected.Add('fg') }
    return $selected.ToArray()
}

function Get-BbwStage([string]$PackagePath, [string]$Stage, [string]$DependencyDirectory, [string[]]$Features, [string]$GpuPath='nvidia') {
    $items = New-Object 'Collections.Generic.List[object]'
    $plugins = @('BigWalkDLSS.dll','BigWalkBridgeNative.dll')
    if ($GpuPath -eq 'nvidia') { $plugins += 'BigWalkDLSSNative.dll' }
    foreach ($name in $plugins) {
        $source = Resolve-BbwPath $PackagePath "payload/BigWalkDLSS/$name"
        if (-not [IO.File]::Exists($source)) { throw "Release payload is incomplete: missing $name. Extract the complete release ZIP and run Setup.cmd again." }
        $items.Add([pscustomobject]@{ Source=$source; Target="BepInEx/plugins/BigWalkDLSS/$name"; FreshOnly=$false; Role='plugin' })
    }
    foreach ($name in @('LICENSE','NOTICE','THIRD_PARTY_NOTICES.md')) {
        $source = Resolve-BbwPath $PackagePath $name
        if (-not [IO.File]::Exists($source)) { throw "Release notices are incomplete: missing $name. Extract the complete release ZIP and retry." }
        $items.Add([pscustomobject]@{ Source=$source; Target="BepInEx/BigWalkDLSS/notices/$name"; FreshOnly=$false; Role='notices' })
    }
    foreach ($name in @('LICENSING.md','RELEASE-CHECKLIST.md','UPSCALERS.md')) {
        $source = Resolve-BbwPath $PackagePath "docs/$name"
        if (-not [IO.File]::Exists($source)) { throw "Release notices are incomplete: missing docs/$name. Extract the complete release ZIP and retry." }
        $items.Add([pscustomobject]@{ Source=$source; Target="BepInEx/BigWalkDLSS/notices/docs/$name"; FreshOnly=$false; Role='notices' })
    }
    $licenses = Resolve-BbwPath $PackagePath 'docs/licenses'
    if (-not [IO.Directory]::Exists($licenses)) { throw 'Release licenses are missing. Extract the complete release ZIP and retry.' }
    foreach ($file in Get-ChildItem -LiteralPath $licenses -Recurse -File) {
        $relative = $file.FullName.Substring($licenses.Length + 1).Replace('\','/')
        $source = Resolve-BbwPath $licenses $relative
        $items.Add([pscustomobject]@{ Source=$source; Target="BepInEx/BigWalkDLSS/notices/docs/licenses/$relative"; FreshOnly=$false; Role='notices' })
    }
    $lock = Read-BbwJson (Resolve-BbwPath $PackagePath 'packaging/dependencies.lock.json')
    if (-not $lock -or -not $lock.PSObject.Properties['SchemaVersion'] -or $lock.SchemaVersion -ne 1 -or -not $lock.PSObject.Properties['Dependencies']) { throw 'Dependency lock is missing or incomplete. This alpha cannot install until official dependency URLs and SHA256 hashes are supplied in packaging/dependencies.lock.json.' }
    # NVIDIA-only runtimes (DLSS, DLSS-G, the NR model) are never installed on the AMD/Intel path.
    $available = @($lock.Dependencies | Where-Object { $GpuPath -eq 'nvidia' -or -not ($_.PSObject.Properties['Vendor'] -and $_.Vendor -eq 'nvidia') })
    foreach ($feature in $Features) {
        if (-not @($available | Where-Object { $_.Feature -eq $feature }).Count) {
            if ($GpuPath -eq 'bridge' -and $feature -eq 'fg') { throw 'AMD and Intel GPUs need the OptiScaler bridge, but this release has no verified bridge runtime. Obtain a complete release; no game files were changed.' }
            throw "Optional feature '$feature' has no verified runtime in this release. Core setup provides DLSS Super Resolution and Advanced Graphics. See docs/DEPENDENCIES.md for manual FG/NR setup; no game files were changed."
        }
    }
    $selected = @($available | Where-Object { $_.Required -or $_.Feature -eq 'core' -or $Features -contains $_.Feature })
    $requiredIds = @('bepinex')
    if ($GpuPath -eq 'nvidia') { $requiredIds += 'nvngx_dlss' }
    foreach ($id in $requiredIds) {
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
        if ($default[0] -eq 'OptiScaler.ini' -and $GpuPath -eq 'bridge') {
            # FSR defaults without DXGI spoofing; the NVIDIA default would select DLSS.
            $source = Resolve-BbwPath $PackagePath 'payload/defaults/OptiScaler.bridge.ini'
            if (-not [IO.File]::Exists($source)) { throw 'Release payload is incomplete: missing payload/defaults/OptiScaler.bridge.ini. Extract the complete release ZIP and run Setup.cmd again.' }
            $items.Add([pscustomobject]@{ Source=$source; Target=$default[1]; FreshOnly=$true; Role='config' })
            continue
        }
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
          [string]$DependencyDirectory, [string[]]$Features=@(), [int]$FaultAfter=0,
          [ValidateSet('Auto','Nvidia','Bridge')][string]$GpuPath='Auto')
    Test-BbwGame $GamePath
    $GamePath = Get-BbwFinalPath $GamePath
    $state = Resolve-BbwPath $GamePath '.big-beautiful-walk'
    $manifestPath = Resolve-BbwPath $state 'manifest.json'
    $journalPath = Resolve-BbwPath $state 'journal.json'
    $gpu = Get-BbwGpuPath $GpuPath
    if ($Mode -eq 'Diagnose') {
        $messages = New-Object 'Collections.Generic.List[string]'
        $messages.Add("Game folder: $GamePath")
        $gpuSource = 'detected'
        if ($GpuPath -ne 'Auto') { $gpuSource = 'selected with -GpuPath' }
        if ($gpu -eq 'nvidia') { $messages.Add("GPU path: NVIDIA (DLSS), $gpuSource.") }
        else { $messages.Add("GPU path: AMD/Intel (OptiScaler bridge), $gpuSource.") }
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
    if ($Mode -eq 'Install' -and $gpu -eq 'bridge') {
        if ($Features -contains 'nr') { throw 'Neural Rendering needs an NVIDIA RTX GPU; it is not available on AMD or Intel GPUs. No game files were changed.' }
        # The OptiScaler bridge is what renders on AMD/Intel, so it is not optional there.
        if ($Features -notcontains 'fg') { $Features = @($Features) + 'fg' }
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
            # Repair components already recorded as ours, even when the optional
            # checkbox was not selected again. File presence is not ownership.
            $dependencyLock = Read-BbwJson (Resolve-BbwPath $PackagePath 'packaging/dependencies.lock.json')
            if ($dependencyLock -and $dependencyLock.PSObject.Properties['Dependencies']) {
                $roles = @($owned.Values | Where-Object { $_.PSObject.Properties['Role'] } | ForEach-Object { [string]$_.Role })
                $Features = @(Get-BbwRepairFeatures $dependencyLock.Dependencies $Features $roles)
            }
            # An NR model owned from an earlier NVIDIA install stays owned but is not repaired here.
            if ($gpu -eq 'bridge') { $Features = @($Features | Where-Object { $_ -ne 'nr' }) }
            $items = @(Get-BbwStage $PackagePath $stage $DependencyDirectory $Features $gpu)
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
            foreach ($entry in @($owned.Values)) {
                if (@($entries | Where-Object { $_.Target -eq $entry.Target }).Count) { continue }
                if ($gpu -eq 'bridge' -and $entry.Target -eq 'BepInEx/plugins/BigWalkDLSS/BigWalkDLSSNative.dll') {
                    # The NVIDIA native plugin is removed (or its original restored) as on uninstall.
                    $beforeHash=Get-BbwHash (Resolve-BbwPath $GamePath $entry.Target)
                    $source=$null
                    if ($entry.OriginalHash) { $source=Resolve-BbwPath $state $entry.Backup }
                    if ($beforeHash -ne $entry.OriginalHash) { $plans.Add([pscustomobject]@{ Target=$entry.Target; BeforeHash=$beforeHash; AfterHash=$entry.OriginalHash; Source=$source; TransactionBackup=$null }) }
                    continue
                }
                $entries.Add($entry)
            }
            foreach ($injector in @('dxgi.dll','version.dll','winhttp.dll','nvngx_dlss.dll','nvngx_dlssnr.dll')) {
                $path = Resolve-BbwPath $GamePath $injector
                if ([IO.File]::Exists($path) -and -not $owned.ContainsKey($injector) -and -not $targets.ContainsKey($path.ToLowerInvariant())) { throw "Unknown injector/runtime '$injector' is present. Use Diagnose and the existing mod's uninstall instructions before installing; no files were changed." }
            }
        } else {
            $otherPlugins=Test-BbwOtherPlugins $GamePath
            foreach ($entry in @($manifest.Entries)) {
                if (Test-BbwEditable $entry) { continue }
                if ($otherPlugins -and (Get-BbwRole $entry) -in @('bepinex','notices')) {
                    # Preserve ownership with the shared files, so a later repair can
                    # update notices and a later uninstall can remove our loader safely.
                    $entries.Add($entry)
                    continue
                }
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
        if ($Mode -eq 'Install' -or $entries.Count -gt 0) { Write-BbwJson $manifestPath ([pscustomobject]@{ SchemaVersion=1; Version='0.7.0'; Entries=@($entries.ToArray()) }) }
        elseif ([IO.File]::Exists($manifestPath)) { [IO.File]::Delete($manifestPath) }
        [IO.File]::Delete($journalPath)
        if ($Mode -eq 'Install') {
            $settingsNote=''
            $hybrid=$gpu -eq 'nvidia' -and $GpuPath -eq 'Auto' -and (Test-BbwHybridGpu)
            if ($gpu -eq 'bridge' -or $hybrid) {
                try {
                    $adjusted=@(Update-BbwBridgeSettings $GamePath -SpoofingOnly:$hybrid)
                    if ($adjusted.Count) { $settingsNote=' OptiScaler.ini was updated for this GPU: ' + ($adjusted -join '; ') + '. The previous copy is in .big-beautiful-walk/config-backups.' }
                } catch { $settingsNote=' OptiScaler.ini could not be updated for this GPU. After launching, open Home and switch off NVIDIA-only features.' }
            }
            $message='Install/Repair completed. Existing settings and other mods were preserved. Start Big Walk through Steam when ready.'
            if ($gpu -eq 'bridge') { $message+=' AMD/Intel GPU path used: the bridge plugin and the OptiScaler upscaler bridge (FSR by default) were installed or repaired; NVIDIA-only DLSS and Neural Rendering files were skipped. Fresh defaults leave Frame Generation off. Open Home, then Frame Gen; see docs/FRAME-GENERATION.md for restart and comparison steps.' }
            elseif ($Features -contains 'fg') { $message+=' The optional upscaler and Frame Generation bridge was installed or repaired. Fresh defaults leave Frame Generation off; Neural Rendering also starts off. Open Home, then Frame Gen or Neural; see docs/FRAME-GENERATION.md for restart and comparison steps.' }
            if ($Features -contains 'nr') { $message+=' The community RHI SF-v2 Neural Rendering model was installed or repaired; this does not enable Neural Rendering.' }
            $message+=$settingsNote
            if ($gpu -eq 'nvidia' -and $GpuPath -eq 'Auto' -and $Features -notcontains 'fg' -and (Test-BbwHybridGpu)) { $message+=' This PC also has an AMD or Intel GPU. If Windows runs Big Walk on it, the mod needs the bridge: run Setup again and tick it, or set Windows to use the NVIDIA GPU for Big Walk.' }
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
        # Best effort: a temporary file held open (for example by an antivirus scan) must not
        # replace the real outcome. Only a staging folder under %TEMP% is ever deleted.
        try {
            $tempRoot=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\','/') + [IO.Path]::DirectorySeparatorChar
            if ([IO.Path]::GetFullPath($stage).StartsWith($tempRoot,[StringComparison]::OrdinalIgnoreCase) -and [IO.Directory]::Exists($stage)) {
                Remove-Item -LiteralPath $stage -Recurse -Force -ErrorAction Stop
            }
        } catch { Write-Warning "Setup could not remove its temporary folder: $stage" }
    }
}

Export-ModuleMember -Function Invoke-BbwSetup,Get-BbwSteamGames,Get-BbwGpuPath,Test-BbwHybridGpu
