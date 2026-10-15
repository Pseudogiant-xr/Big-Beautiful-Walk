param([string]$OnlineDependencyLock)
$ErrorActionPreference='Stop'
Set-StrictMode -Version 2.0
Import-Module (Join-Path $PSScriptRoot '../scripts/Setup.Core.psm1') -Force
Add-Type -AssemblyName System.IO.Compression.FileSystem
$scratch=Join-Path $PSScriptRoot ('../work/setup-tests-' + [guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($scratch)
$script:passed=0
function Put([string]$Path,[string]$Text) { [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($Path)); [IO.File]::WriteAllText($Path,$Text) }
function Assert([bool]$Condition,[string]$Message) { if (-not $Condition) { throw "ASSERT: $Message" } }
function Expect-Failure([scriptblock]$Action,[string]$Pattern) {
    $message=$null
    try { & $Action | Out-Null } catch { $message=$_.Exception.Message }
    Assert ($null -ne $message -and $message -match $Pattern) "Expected '$Pattern'; got '$message'"
}
function Case([string]$Name,[scriptblock]$Action) { & $Action; $script:passed++; Write-Output "PASS $Name" }
function New-Fixture([string]$Name) {
    $root=Join-Path $scratch $Name
    $game=Join-Path $root 'game'; $package=Join-Path $root 'package'; $deps=Join-Path $root 'deps'
    Put (Join-Path $game 'Big Walk.exe') 'fake-game'
    Put (Join-Path $game 'GameAssembly.dll') 'game-assembly-do-not-touch'
    [void][IO.Directory]::CreateDirectory((Join-Path $game 'Big Walk_Data'))
    Put (Join-Path $package 'payload/BigWalkDLSS/BigWalkDLSS.dll') 'plugin-one'
    Put (Join-Path $package 'payload/BigWalkDLSS/BigWalkDLSSNative.dll') 'native-one'
    Put (Join-Path $package 'payload/defaults/local.bigwalk.native-dlss.cfg') 'default-config'
    Put (Join-Path $package 'payload/defaults/OptiScaler.ini') 'default-opti'
    Put (Join-Path $root 'archive/winhttp.dll') 'loader-known'
    Put (Join-Path $root 'archive/BepInEx/core/BepInEx.Core.dll') 'bep-core-known'
    [void][IO.Directory]::CreateDirectory($deps)
    [IO.Compression.ZipFile]::CreateFromDirectory((Join-Path $root 'archive'),(Join-Path $deps 'bepinex.zip'))
    Put (Join-Path $deps 'nvngx_dlss.file') 'runtime-known'
    $lock=@{SchemaVersion=1;Dependencies=@(
        @{Id='bepinex';Url='https://example.org/bepinex.zip';SHA256=(Get-FileHash (Join-Path $deps 'bepinex.zip')).Hash;Archive='zip';Required=$true;Feature='core';Files=@(@{Source='winhttp.dll';Target='winhttp.dll'},@{Source='BepInEx/core/BepInEx.Core.dll';Target='BepInEx/core/BepInEx.Core.dll'})},
        @{Id='nvngx_dlss';Url='https://example.org/nvngx_dlss.dll';SHA256=(Get-FileHash (Join-Path $deps 'nvngx_dlss.file')).Hash;Archive='file';Required=$true;Feature='core';Files=@(@{Source='nvngx_dlss.dll';Target='nvngx_dlss.dll'})})}
    Put (Join-Path $package 'packaging/dependencies.lock.json') ($lock | ConvertTo-Json -Depth 8)
    return @{GamePath=$game;PackagePath=$package;DependencyDirectory=$deps}
}
function Install($Fixture,[int]$Fault=0) { Invoke-BbwSetup @Fixture -Mode Install -FaultAfter $Fault }
function Uninstall($Fixture) { Invoke-BbwSetup @Fixture -Mode Uninstall }
function Add-FgFixture($Fixture) {
    $bridge=Join-Path $Fixture.DependencyDirectory 'fg_bridge.file'
    Put $bridge 'experimental-fg-bridge'
    $lockPath=Join-Path $Fixture.PackagePath 'packaging/dependencies.lock.json'
    $lock=Get-Content $lockPath -Raw | ConvertFrom-Json
    $lock.Dependencies+=@{Id='fg_bridge';Url='https://example.org/fg_bridge.dll';SHA256=(Get-FileHash $bridge).Hash;Archive='file';Required=$false;Feature='fg';Files=@(@{Source='version.dll';Target='version.dll'})}
    Put $lockPath ($lock | ConvertTo-Json -Depth 8)
    Put (Join-Path $Fixture.PackagePath 'payload/defaults/OptiScaler.ini') '[OptiFG] FGType=off'
}
try {
    Case 'Windows PowerShell parses the GUI entry point' {
        $parseErrors=$null
        [void][Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot '../scripts/Setup.ps1'),[ref]$null,[ref]$parseErrors)
        Assert (@($parseErrors).Count -eq 0) 'GUI parses in Windows PowerShell'
    }
    Case 'anonymous HTTPS redirects support GitHub assets and relative locations' {
        $module=Get-Module Setup.Core
        $state=@{Requests=(New-Object 'Collections.Generic.List[object]')}
        $transport={
            param($Uri,$Destination,$Timeout)
            $state.Requests.Add(@{Uri=$Uri;Timeout=$Timeout})
            if ($state.Requests.Count -eq 1) { return @{Status=302;Location='https://release-assets.githubusercontent.com/asset.zip?signature=fixture-private'} }
            if ($state.Requests.Count -eq 2) { return @{Status=307;Location='/final.zip?signature=fixture-private'} }
            [IO.File]::WriteAllText($Destination,'verified-download-fixture')
            return @{Status=200;Location=$null}
        }.GetNewClosure()
        $destination=Join-Path $scratch 'redirect.zip'
        & $module { param($Path,$Transport) Receive-BbwDownload ([uri]'https://github.com/owner/repo/releases/download/v1/asset.zip') $Path $Transport } $destination $transport
        Assert ($state.Requests.Count -eq 3) 'three bounded HTTPS requests'
        Assert ($state.Requests[1].Uri.Host -eq 'release-assets.githubusercontent.com') 'GitHub asset redirect followed'
        Assert ($state.Requests[2].Uri.AbsolutePath -eq '/final.zip') 'relative redirect resolved'
        Assert (@($state.Requests | Where-Object { $_.Timeout -le 0 -or $_.Timeout -gt 180000 }).Count -eq 0) 'requests have bounded timeouts'
        Assert ([IO.File]::ReadAllText($destination) -eq 'verified-download-fixture') 'redirect body saved'
    }
    Case 'redirect download rejects downgrade credentials missing destinations and loops' {
        $module=Get-Module Setup.Core
        foreach ($location in @('http://example.org/asset.zip','https://user:password@example.org/asset.zip','')) {
            $state=@{Count=0;Location=$location}
            $transport={ param($Uri,$Destination,$Timeout) $state.Count++; return @{Status=302;Location=$state.Location} }.GetNewClosure()
            Expect-Failure { & $module { param($Path,$Transport) Receive-BbwDownload ([uri]'https://github.com/owner/release.zip') $Path $Transport } (Join-Path $scratch 'rejected.zip') $transport } 'anonymous HTTPS|no destination'
            Assert ($state.Count -eq 1) 'unsafe redirect never requested'
        }
        $state=@{Count=0}
        $transport={ param($Uri,$Destination,$Timeout) $state.Count++; return @{Status=301;Location='/loop.zip'} }.GetNewClosure()
        Expect-Failure { & $module { param($Path,$Transport) Receive-BbwDownload ([uri]'https://github.com/owner/release.zip') $Path $Transport } (Join-Path $scratch 'loop.zip') $transport } 'eight HTTPS redirects'
        Assert ($state.Count -eq 9) 'initial request plus at most eight redirects'
        $state.Count=0
        Expect-Failure { & $module { param($Path,$Transport) Receive-BbwDownload ([uri]'http://example.org/start.zip') $Path $Transport } (Join-Path $scratch 'http.zip') $transport } 'anonymous HTTPS'
        Assert ($state.Count -eq 0) 'insecure initial URL never requested'
    }
    if ($OnlineDependencyLock) {
        Case 'one real GitHub release download verifies pinned bytes and installs with other dependencies cached' {
            $proposal=Get-Content -LiteralPath $OnlineDependencyLock -Raw | ConvertFrom-Json
            $dependency=@($proposal.Dependencies | Where-Object { $_.Id -eq 'optiscaler_fg' })[0]
            Assert ($dependency.Url -like 'https://github.com/*/releases/download/*') 'online smoke uses a pinned GitHub release asset'
            $f=New-Fixture 'online-redirect'
            $download=Join-Path $f.DependencyDirectory ($dependency.Id+'.'+$dependency.Archive)
            $module=Get-Module Setup.Core
            & $module { param($Uri,$Path) Receive-BbwDownload ([uri]$Uri) $Path } $dependency.Url $download
            Assert ((Get-FileHash -LiteralPath $download -Algorithm SHA256).Hash -eq $dependency.SHA256) 'real downloaded asset matches locked SHA256'
            $lockPath=Join-Path $f.PackagePath 'packaging/dependencies.lock.json'
            $lock=Get-Content $lockPath -Raw | ConvertFrom-Json
            $lock.Dependencies+=$dependency
            Put $lockPath ($lock | ConvertTo-Json -Depth 8)
            Invoke-BbwSetup @f -Mode Install -Features fg | Out-Null
            Assert (Test-Path (Join-Path $f.GamePath 'version.dll')) 'real optional bridge installed into disposable game'
            Uninstall $f | Out-Null
            Assert (-not (Test-Path (Join-Path $f.GamePath 'version.dll'))) 'real optional bridge safely removed'
        }
    }
    Case 'setup locks one normalized game path while allowing another identical game' {
        $first=New-Fixture 'mutex-first'; $second=New-Fixture 'mutex-second'
        $module=Get-Module Setup.Core
        $name=& $module { param($path) Get-BbwMutexName $path } $first.GamePath
        $alias=& $module { param($path) Get-BbwMutexName $path } ($first.GamePath.ToUpperInvariant()+'\')
        Assert ($name -eq $alias) 'case and trailing slash share one mutex'
        $activeTarget=Join-Path $first.GamePath 'pending.dll'; Put $activeTarget 'active-transaction-bytes'
        $activeJournal=Join-Path $first.GamePath '.big-beautiful-walk/journal.json'
        Put $activeJournal (@{SchemaVersion=1;PreviousManifest=$null;Actions=@(@{Target='pending.dll';BeforeHash=$null;AfterHash=(Get-FileHash $activeTarget).Hash.ToLowerInvariant();TransactionBackup=$null})} | ConvertTo-Json -Depth 8)
        $journalBytes=[IO.File]::ReadAllText($activeJournal)
        $ready=New-Object Threading.ManualResetEvent($false)
        $release=New-Object Threading.ManualResetEvent($false)
        $holder=[PowerShell]::Create()
        [void]$holder.AddScript({
            param($Name,$Ready,$Release)
            $mutex=New-Object Threading.Mutex($false,$Name)
            try { [void]$mutex.WaitOne(); [void]$Ready.Set(); [void]$Release.WaitOne() }
            finally { $mutex.ReleaseMutex(); $mutex.Dispose() }
        }).AddArgument($name).AddArgument($ready).AddArgument($release)
        $operation=$holder.BeginInvoke()
        try {
            Assert ($ready.WaitOne(5000)) 'independent runspace holds first-game lock'
            Expect-Failure { Install $first } 'Another setup'
            Expect-Failure { Uninstall $first } 'Another setup'
            Assert ([IO.File]::ReadAllText($activeTarget) -eq 'active-transaction-bytes') 'competing setup cannot roll back active transaction bytes'
            Assert ([IO.File]::ReadAllText($activeJournal) -eq $journalBytes) 'competing setup preserves active journal exactly'
            Install $second | Out-Null; Uninstall $second | Out-Null
        } finally {
            [void]$release.Set(); [void]$holder.EndInvoke($operation)
            $holder.Dispose(); $ready.Dispose(); $release.Dispose()
        }
    }
    Case 'running game blocks mutation in its folder but allows diagnosis and another game folder' {
        $running=New-Fixture 'running-game'; $other=New-Fixture 'other-game'
        $executable=Join-Path $running.GamePath 'Big Walk.exe'
        Remove-Item -LiteralPath $executable
        $className='SetupWait'+[guid]::NewGuid().ToString('N')
        $source='public static class '+$className+' { public static void Main() { System.Threading.Thread.Sleep(30000); } }'
        $compile="Add-Type -TypeDefinition '"+$source+"' -OutputAssembly '"+$executable.Replace("'","''")+"' -OutputType ConsoleApplication -ErrorAction Stop"
        $encoded=[Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($compile))
        & powershell.exe -NoProfile -NonInteractive -EncodedCommand $encoded
        Assert ($LASTEXITCODE -eq 0 -and (Test-Path -LiteralPath $executable)) 'Windows PowerShell compiled disposable .NET Framework game process'
        $child=Start-Process -FilePath $executable -WindowStyle Hidden -PassThru
        try {
            Assert (-not $child.WaitForExit(150)) 'disposable game process is alive'
            Expect-Failure { Install $running } 'Close Big Walk'
            Expect-Failure { Uninstall $running } 'Close Big Walk'
            Assert (-not (Test-Path (Join-Path $running.GamePath '.big-beautiful-walk'))) 'running game has no journal or state changes'
            Assert (-not (Test-Path (Join-Path $running.GamePath 'BepInEx'))) 'running game has no target mods'
            $diagnosis=Invoke-BbwSetup @running -Mode Diagnose
            Assert ($diagnosis -match 'Game folder:') 'read-only diagnosis remains available while game runs'
            Install $other | Out-Null; Uninstall $other | Out-Null
        } finally {
            if (-not $child.HasExited) { $child.Kill(); [void]$child.WaitForExit(5000) }
            $child.Dispose()
        }
    }
    Case 'fresh install, uninstall and game bytes preserved' {
        $f=New-Fixture 'fresh'; Install $f | Out-Null
        Assert (Test-Path (Join-Path $f.GamePath 'BepInEx/plugins/BigWalkDLSS/BigWalkDLSS.dll')) 'plugin installed'
        Assert (-not (Test-Path (Join-Path $f.GamePath 'OptiScaler.ini'))) 'core-only install does not create OptiScaler config'
        Uninstall $f | Out-Null
        Assert (-not (Test-Path (Join-Path $f.GamePath 'winhttp.dll'))) 'owned loader removed'
        Assert (-not (Test-Path (Join-Path $f.GamePath 'BepInEx/plugins/BigWalkDLSS/BigWalkDLSS.dll'))) 'plugin removed'
        Assert ([IO.File]::ReadAllText((Join-Path $f.GamePath 'GameAssembly.dll')) -eq 'game-assembly-do-not-touch') 'game assembly preserved'
    }
    Case 'existing known dependency and config bytes preserved' {
        $f=New-Fixture 'preserve'
        Put (Join-Path $f.GamePath 'winhttp.dll') 'loader-known'
        Put (Join-Path $f.GamePath 'BepInEx/config/local.bigwalk.native-dlss.cfg') 'my-settings'
        Put (Join-Path $f.GamePath 'OptiScaler.ini') 'my-opti'
        Install $f | Out-Null; Uninstall $f | Out-Null
        Assert ([IO.File]::ReadAllText((Join-Path $f.GamePath 'winhttp.dll')) -eq 'loader-known') 'preexisting loader retained'
        Assert ([IO.File]::ReadAllText((Join-Path $f.GamePath 'BepInEx/config/local.bigwalk.native-dlss.cfg')) -eq 'my-settings') 'existing config retained'
        Assert ([IO.File]::ReadAllText((Join-Path $f.GamePath 'OptiScaler.ini')) -eq 'my-opti') 'existing Opti retained'
    }
    Case 'update and uninstall retain original preexisting files' {
        $f=New-Fixture 'update'; Put (Join-Path $f.GamePath 'winhttp.dll') 'loader-known'
        Install $f | Out-Null
        Put (Join-Path $f.PackagePath 'payload/BigWalkDLSS/BigWalkDLSS.dll') 'plugin-two'
        Install $f | Out-Null
        Assert ([IO.File]::ReadAllText((Join-Path $f.GamePath 'BepInEx/plugins/BigWalkDLSS/BigWalkDLSS.dll')) -eq 'plugin-two') 'updated plugin'
        Uninstall $f | Out-Null
        Assert ([IO.File]::ReadAllText((Join-Path $f.GamePath 'winhttp.dll')) -eq 'loader-known') 'original loader survives update'
    }
    Case 'modified owned file makes uninstall fail closed' {
        $f=New-Fixture 'conflict'; Install $f | Out-Null
        $changed=Join-Path $f.GamePath 'BepInEx/plugins/BigWalkDLSS/BigWalkDLSS.dll'
        Put $changed 'user-change'
        Expect-Failure { Uninstall $f } 'changed after installation'
        Assert ([IO.File]::ReadAllText($changed) -eq 'user-change') 'user change preserved'
        Assert (Test-Path (Join-Path $f.GamePath 'winhttp.dll')) 'no partial uninstall'
    }
    Case 'missing owned DLL repairs from verified payload and does not block uninstall' {
        $f=New-Fixture 'missing-owned'; Install $f | Out-Null
        $plugin=Join-Path $f.GamePath 'BepInEx/plugins/BigWalkDLSS/BigWalkDLSS.dll'
        $config=Join-Path $f.GamePath 'BepInEx/config/local.bigwalk.native-dlss.cfg'; Put $config 'my-retained-settings'
        $other=Join-Path $f.GamePath 'BepInEx/plugins/AnotherMod/AnotherMod.dll'; Put $other 'my-other-mod'
        [IO.File]::Delete($plugin)
        Assert ((Invoke-BbwSetup @f -Mode Diagnose) -match 'MISSING:.*Run Install/Repair') 'diagnostics explain missing-file repair'
        Install $f | Out-Null
        Assert ([IO.File]::ReadAllText($plugin) -eq 'plugin-one') 'verified release payload repairs absent DLL'
        [IO.File]::Delete($plugin)
        Uninstall $f | Out-Null
        Assert (-not (Test-Path $plugin)) 'already absent owned DLL stays absent on uninstall'
        Assert ([IO.File]::ReadAllText($config) -eq 'my-retained-settings') 'settings preserved after missing-DLL recovery'
        Assert ([IO.File]::ReadAllText($other) -eq 'my-other-mod') 'other mod preserved after missing-DLL recovery'
    }
    Case 'missing installed DLL restores verified original backup and refuses missing original backup' {
        foreach ($backupState in @('valid','missing')) {
            $f=New-Fixture ('original-backup-'+$backupState); Install $f | Out-Null
            $manifestPath=Join-Path $f.GamePath '.big-beautiful-walk/manifest.json'
            $manifest=Get-Content $manifestPath -Raw | ConvertFrom-Json
            $entry=@($manifest.Entries | Where-Object { $_.Target -eq 'BepInEx/plugins/BigWalkDLSS/BigWalkDLSS.dll' })[0]
            $backup=Join-Path $f.GamePath '.big-beautiful-walk/backups/original.bin'; Put $backup 'original-before-bbw'
            $entry.OriginalHash=(Get-FileHash $backup).Hash.ToLowerInvariant(); $entry.Backup='backups/original.bin'
            Put $manifestPath ($manifest | ConvertTo-Json -Depth 8)
            $plugin=Join-Path $f.GamePath $entry.Target; [IO.File]::Delete($plugin)
            if ($backupState -eq 'missing') {
                [IO.File]::Delete($backup)
                Expect-Failure { Uninstall $f } 'Original backup is damaged'
                Assert (-not (Test-Path $plugin)) 'missing original backup does not invent replacement bytes'
                Assert (Test-Path (Join-Path $f.GamePath 'BepInEx/plugins/BigWalkDLSS/BigWalkDLSSNative.dll')) 'missing original backup prevents partial uninstall'
            } else {
                Uninstall $f | Out-Null
                Assert ([IO.File]::ReadAllText($plugin) -eq 'original-before-bbw') 'verified original restored even if installed DLL is absent'
            }
        }
    }
    Case 'launch-like editable config mutation survives repair and uninstall' {
        foreach ($style in @('modern','legacy')) {
            $f=New-Fixture ('editable-'+$style); Install $f | Out-Null
            $manifestPath=Join-Path $f.GamePath '.big-beautiful-walk/manifest.json'
            $manifest=Get-Content $manifestPath -Raw | ConvertFrom-Json
            Assert (@($manifest.Entries | Where-Object { $_.PreserveOnRemove }).Count -eq 1) 'core editable flag recorded'
            if ($style -eq 'legacy') {
                foreach ($entry in $manifest.Entries) { $entry.PSObject.Properties.Remove('PreserveOnRemove'); $entry.PSObject.Properties.Remove('Role') }
                Put $manifestPath ($manifest | ConvertTo-Json -Depth 8)
            }
            $config=Join-Path $f.GamePath 'BepInEx/config/local.bigwalk.native-dlss.cfg'
            $opti=Join-Path $f.GamePath 'OptiScaler.ini'
            Put $config "[Rendering]`r`nSharpness=0.7`r`n# Written by the game"
            Put $opti 'user-opti-setting'
            $expected=[IO.File]::ReadAllText($config)
            Install $f | Out-Null; Uninstall $f | Out-Null
            Assert ([IO.File]::ReadAllText($config) -eq $expected) 'modified native settings preserved'
            Assert ([IO.File]::ReadAllText($opti) -eq 'user-opti-setting') 'modified Opti settings preserved'
            Assert (-not (Test-Path (Join-Path $f.GamePath 'BepInEx/plugins/BigWalkDLSS/BigWalkDLSS.dll'))) 'plugin removed despite edited config'
        }
    }
    Case 'other installed plugins keep shared BepInEx loader and core on uninstall' {
        foreach ($style in @('modern','legacy')) {
            $f=New-Fixture ('shared-'+$style); Install $f | Out-Null
            if ($style -eq 'legacy') {
                $manifestPath=Join-Path $f.GamePath '.big-beautiful-walk/manifest.json'
                $manifest=Get-Content $manifestPath -Raw | ConvertFrom-Json
                foreach ($entry in $manifest.Entries) { $entry.PSObject.Properties.Remove('PreserveOnRemove'); $entry.PSObject.Properties.Remove('Role') }
                Put $manifestPath ($manifest | ConvertTo-Json -Depth 8)
            }
            $other=Join-Path $f.GamePath 'BepInEx/plugins/AnotherMod/AnotherMod.dll'; Put $other 'another-plugin'
            Uninstall $f | Out-Null
            Assert ([IO.File]::ReadAllText($other) -eq 'another-plugin') 'other plugin untouched'
            Assert ([IO.File]::ReadAllText((Join-Path $f.GamePath 'winhttp.dll')) -eq 'loader-known') 'shared loader preserved'
            Assert ([IO.File]::ReadAllText((Join-Path $f.GamePath 'BepInEx/core/BepInEx.Core.dll')) -eq 'bep-core-known') 'shared core preserved'
            Assert (-not (Test-Path (Join-Path $f.GamePath 'BepInEx/plugins/BigWalkDLSS/BigWalkDLSS.dll'))) 'BBW plugin removed'
        }
    }
    Case 'other patchers and extra DLLs inside the managed plugin folder retain shared BepInEx' {
        foreach ($style in @('modern','legacy')) {
            foreach ($location in @('BepInEx/patchers/AnotherMod/OtherPatcher.dll','BepInEx/plugins/BigWalkDLSS/OtherHelper.dll')) {
                $f=New-Fixture ('shared-extra-'+$style+'-'+[guid]::NewGuid().ToString('N')); Install $f | Out-Null
                if ($style -eq 'legacy') {
                    $manifestPath=Join-Path $f.GamePath '.big-beautiful-walk/manifest.json'
                    $manifest=Get-Content $manifestPath -Raw | ConvertFrom-Json
                    foreach ($entry in $manifest.Entries) { $entry.PSObject.Properties.Remove('PreserveOnRemove'); $entry.PSObject.Properties.Remove('Role') }
                    Put $manifestPath ($manifest | ConvertTo-Json -Depth 8)
                }
                $other=Join-Path $f.GamePath $location; Put $other 'other-bep-component'
                Uninstall $f | Out-Null
                Assert ([IO.File]::ReadAllText($other) -eq 'other-bep-component') 'other component retained'
                Assert ([IO.File]::ReadAllText((Join-Path $f.GamePath 'winhttp.dll')) -eq 'loader-known') 'other component loader retained'
                Assert ([IO.File]::ReadAllText((Join-Path $f.GamePath 'BepInEx/core/BepInEx.Core.dll')) -eq 'bep-core-known') 'other component core retained'
                Assert (-not (Test-Path (Join-Path $f.GamePath 'BepInEx/plugins/BigWalkDLSS/BigWalkDLSS.dll'))) 'only owned plugin DLL removed'
            }
        }
    }
    Case 'optional FG selection installs bridge and fresh config while retaining FG off' {
        $f=New-Fixture 'optional-fg'; Add-FgFixture $f
        Install $f | Out-Null
        Assert (-not (Test-Path (Join-Path $f.GamePath 'version.dll'))) 'unchecked FG does not install bridge'
        Assert (-not (Test-Path (Join-Path $f.GamePath 'OptiScaler.ini'))) 'unchecked FG does not install Opti config'
        $message=Invoke-BbwSetup @f -Mode Install -Features fg
        Assert ([IO.File]::ReadAllText((Join-Path $f.GamePath 'version.dll')) -eq 'experimental-fg-bridge') 'selected bridge installed'
        $config=Join-Path $f.GamePath 'OptiScaler.ini'
        Assert ([IO.File]::ReadAllText($config) -eq '[OptiFG] FGType=off') 'fresh FG remains off'
        Assert ($message -match 'fresh defaults leave Frame Generation off') 'completion does not claim active FG'
        Put $config 'my-changed-FG-settings'
        Invoke-BbwSetup @f -Mode Install -Features fg | Out-Null
        Uninstall $f | Out-Null
        Assert ([IO.File]::ReadAllText($config) -eq 'my-changed-FG-settings') 'edited FG config survives repair and uninstall'
        Assert (-not (Test-Path (Join-Path $f.GamePath 'version.dll'))) 'owned FG bridge uninstalled'
    }
    Case 'optional FG preserves preexisting OptiScaler config bytes' {
        $f=New-Fixture 'optional-fg-existing'; Add-FgFixture $f
        $config=Join-Path $f.GamePath 'OptiScaler.ini'; Put $config 'existing-custom-settings'
        Invoke-BbwSetup @f -Mode Install -Features fg | Out-Null
        Assert ([IO.File]::ReadAllText($config) -eq 'existing-custom-settings') 'optional install preserves existing Opti bytes'
        Uninstall $f | Out-Null
        Assert ([IO.File]::ReadAllText($config) -eq 'existing-custom-settings') 'optional uninstall preserves existing Opti bytes'
    }
    Case 'missing and hash-invalid dependencies fail before mutation' {
        $f=New-Fixture 'missing'; Remove-Item (Join-Path $f.DependencyDirectory 'nvngx_dlss.file')
        Expect-Failure { Install $f } 'Offline dependency is missing'
        Assert (-not (Test-Path (Join-Path $f.GamePath '.big-beautiful-walk'))) 'missing dependency no mutation'
        Put (Join-Path $f.DependencyDirectory 'nvngx_dlss.file') 'tampered'
        Expect-Failure { Install $f } 'SHA256 mismatch'
        Assert (-not (Test-Path (Join-Path $f.GamePath '.big-beautiful-walk'))) 'hash rejection no mutation'
    }
    Case 'unknown injector collision fails before mutation' {
        $f=New-Fixture 'injector'; Put (Join-Path $f.GamePath 'dxgi.dll') 'another-mod'
        Expect-Failure { Install $f } 'Unknown injector'
        Assert (-not (Test-Path (Join-Path $f.GamePath '.big-beautiful-walk'))) 'injector rejection no mutation'
    }
    Case 'unavailable optional feature refuses before mutation' {
        $f=New-Fixture 'feature'
        Expect-Failure { Invoke-BbwSetup @f -Mode Install -Features fg } 'no verified runtime'
        Expect-Failure { Invoke-BbwSetup @f -Mode Install -Features nr } 'no verified runtime'
        Assert (-not (Test-Path (Join-Path $f.GamePath '.big-beautiful-walk'))) 'unavailable feature no mutation'
    }
    Case 'ZIP traversal rejected before mutation' {
        $f=New-Fixture 'zip'; $archive=Join-Path $f.DependencyDirectory 'bepinex.zip'
        Remove-Item $archive
        $zip=[IO.Compression.ZipFile]::Open($archive,[IO.Compression.ZipArchiveMode]::Create)
        [void]$zip.CreateEntry('../escape.dll'); $zip.Dispose()
        $lockPath=Join-Path $f.PackagePath 'packaging/dependencies.lock.json'
        $lock=Get-Content $lockPath -Raw | ConvertFrom-Json
        $lock.Dependencies[0].SHA256=(Get-FileHash $archive).Hash
        Put $lockPath ($lock | ConvertTo-Json -Depth 8)
        Expect-Failure { Install $f } 'traversal'
        Assert (-not (Test-Path (Join-Path $f.GamePath '.big-beautiful-walk'))) 'ZIP rejection no mutation'
    }
    Case 'dependency target traversal rejected before mutation' {
        $f=New-Fixture 'target'
        $lockPath=Join-Path $f.PackagePath 'packaging/dependencies.lock.json'; $lock=Get-Content $lockPath -Raw | ConvertFrom-Json
        $lock.Dependencies[0].Files[0].Target='../escape.dll'; Put $lockPath ($lock | ConvertTo-Json -Depth 8)
        Expect-Failure { Install $f } 'escapes|Unsafe Windows path'
        Assert (-not (Test-Path (Join-Path $f.GamePath '.big-beautiful-walk'))) 'target rejection no mutation'
    }
    Case 'Windows path aliases rejected before mutation' {
        $f=New-Fixture 'alias'
        $lockPath=Join-Path $f.PackagePath 'packaging/dependencies.lock.json'; $lock=Get-Content $lockPath -Raw | ConvertFrom-Json
        $lock.Dependencies[0].Files[0].Target='.. /escape.dll'; Put $lockPath ($lock | ConvertTo-Json -Depth 8)
        Expect-Failure { Install $f } 'Unsafe Windows path'
        Assert (-not (Test-Path (Join-Path $f.GamePath '.big-beautiful-walk'))) 'alias rejection no mutation'
    }
    Case 'junction target rejected before mutation' {
        $f=New-Fixture 'junction'; $outside=Join-Path $scratch 'junction-outside'
        [void][IO.Directory]::CreateDirectory($outside)
        $junction=Join-Path $f.GamePath 'BepInEx'
        [void](New-Item -ItemType Junction -Path $junction -Target $outside)
        try { Expect-Failure { Install $f } 'junctions'; Assert (@(Get-ChildItem $outside).Count -eq 0) 'junction destination unchanged' }
        finally { [IO.Directory]::Delete($junction) }
    }
    Case 'injected mid-install failure rolls back and rerun succeeds' {
        $f=New-Fixture 'fault'
        Expect-Failure { Install $f 2 } 'Injected transaction failure'
        Assert (-not (Test-Path (Join-Path $f.GamePath 'BepInEx/plugins/BigWalkDLSS/BigWalkDLSS.dll'))) 'first write rolled back'
        Assert (-not (Test-Path (Join-Path $f.GamePath '.big-beautiful-walk/journal.json'))) 'rollback clears journal'
        Install $f | Out-Null; Uninstall $f | Out-Null
    }
    Case 'interrupted journal recovers original bytes before install' {
        $f=New-Fixture 'recovery'; $state=Join-Path $f.GamePath '.big-beautiful-walk'
        $target=Join-Path $f.GamePath 'interrupted.txt'; Put $target 'before'
        $before=(Get-FileHash $target).Hash.ToLowerInvariant()
        Put (Join-Path $state 'transactions/test/0.bin') 'before'
        Put $target 'after'; $after=(Get-FileHash $target).Hash.ToLowerInvariant()
        Put (Join-Path $state 'journal.json') (@{SchemaVersion=1;PreviousManifest=$null;Actions=@(@{Target='interrupted.txt';BeforeHash=$before;AfterHash=$after;TransactionBackup='transactions/test/0.bin'})} | ConvertTo-Json -Depth 8)
        Install $f | Out-Null
        Assert ([IO.File]::ReadAllText($target) -eq 'before') 'journal restores before bytes'
        Uninstall $f | Out-Null
    }
    Case 'damaged recovery backup refuses all changes' {
        $f=New-Fixture 'bad-recovery'; $state=Join-Path $f.GamePath '.big-beautiful-walk'
        $target=Join-Path $f.GamePath 'interrupted.txt'; Put $target 'after'
        $after=(Get-FileHash $target).Hash.ToLowerInvariant()
        Put (Join-Path $state 'transactions/test/0.bin') 'corrupt'
        Put (Join-Path $state 'journal.json') (@{SchemaVersion=1;PreviousManifest=$null;Actions=@(@{Target='interrupted.txt';BeforeHash=('a'*64);AfterHash=$after;TransactionBackup='transactions/test/0.bin'})} | ConvertTo-Json -Depth 8)
        Expect-Failure { Install $f } 'backup is missing or damaged'
        Assert ([IO.File]::ReadAllText($target) -eq 'after') 'bad backup no mutation'
    }
    Case 'locked owned file cannot produce partial update' {
        $f=New-Fixture 'locked'; Install $f | Out-Null
        $plugin=Join-Path $f.GamePath 'BepInEx/plugins/BigWalkDLSS/BigWalkDLSS.dll'
        Put (Join-Path $f.PackagePath 'payload/BigWalkDLSS/BigWalkDLSS.dll') 'plugin-two'
        $handle=[IO.File]::Open($plugin,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read)
        try { Expect-Failure { Install $f } 'being used|access|process' } finally { $handle.Dispose() }
        Assert ([IO.File]::ReadAllText($plugin) -eq 'plugin-one') 'locked original retained'
        Uninstall $f | Out-Null
    }
    Write-Output "PASS $script:passed disposable installer integration cases."
} finally {
    $resolved=[IO.Path]::GetFullPath($scratch)
    $expected=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../work')).TrimEnd('\') + '\'
    if (-not $resolved.StartsWith($expected,[StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe scratch cleanup path.' }
    if (Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Recurse -Force }
}
