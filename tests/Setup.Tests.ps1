param([string]$OnlineDependencyLock, [string]$CaseFilter='*', [string]$SetupCorePath=(Join-Path $PSScriptRoot '../scripts/Setup.Core.psm1'), [string]$SetupPath=(Join-Path $PSScriptRoot '../scripts/Setup.ps1'))
$ErrorActionPreference='Stop'
Set-StrictMode -Version 2.0
Import-Module $SetupCorePath -Force
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
function Case([string]$Name,[scriptblock]$Action) { if ($Name -notlike $CaseFilter) { return }; & $Action; $script:passed++; Write-Output "PASS $Name" }
function New-Fixture([string]$Name) {
    $root=Join-Path $scratch $Name
    $game=Join-Path $root 'game'; $package=Join-Path $root 'package'; $deps=Join-Path $root 'deps'
    Put (Join-Path $game 'Big Walk.exe') 'fake-game'
    Put (Join-Path $game 'GameAssembly.dll') 'game-assembly-do-not-touch'
    [void][IO.Directory]::CreateDirectory((Join-Path $game 'Big Walk_Data'))
    Put (Join-Path $package 'payload/BigWalkDLSS/BigWalkDLSS.dll') 'plugin-one'
    Put (Join-Path $package 'payload/BigWalkDLSS/BigWalkDLSSNative.dll') 'native-one'
    Put (Join-Path $package 'payload/BigWalkDLSS/BigWalkBridgeNative.dll') 'bridge-native-one'
    Put (Join-Path $package 'payload/defaults/local.bigwalk.native-dlss.cfg') 'default-config'
    Put (Join-Path $package 'payload/defaults/OptiScaler.ini') 'default-opti'
    Put (Join-Path $package 'payload/defaults/OptiScaler.bridge.ini') "[Upscalers]`r`nDx11Upscaler=fsr31`r`n[FrameGen]`r`nEnabled=false"
    foreach ($name in @('LICENSE','NOTICE','THIRD_PARTY_NOTICES.md')) { Put (Join-Path $package $name) "fixture-$name" }
    Put (Join-Path $package 'THIRD_PARTY_NOTICES.md') '[Vendor](docs/licenses/vendor.txt) [Assessment](docs/LICENSING.md)'
    Put (Join-Path $package 'docs/LICENSING.md') '[Project license](../LICENSE) [Checklist](RELEASE-CHECKLIST.md)'
    Put (Join-Path $package 'docs/RELEASE-CHECKLIST.md') '[Sources](licenses/SOURCES.md)'
    Put (Join-Path $package 'docs/UPSCALERS.md') 'fixture upscaler guide'
    Put (Join-Path $package 'docs/licenses/SOURCES.md') '[Project license](../../LICENSE)'
    Put (Join-Path $package 'docs/licenses/vendor.txt') 'fixture-vendor-license'
    Put (Join-Path $root 'archive/winhttp.dll') 'loader-known'
    Put (Join-Path $root 'archive/BepInEx/core/BepInEx.Core.dll') 'bep-core-known'
    [void][IO.Directory]::CreateDirectory($deps)
    [IO.Compression.ZipFile]::CreateFromDirectory((Join-Path $root 'archive'),(Join-Path $deps 'bepinex.zip'))
    Put (Join-Path $deps 'nvngx_dlss.file') 'runtime-known'
    $lock=@{SchemaVersion=1;Dependencies=@(
        @{Id='bepinex';Url='https://example.org/bepinex.zip';SHA256=(Get-FileHash (Join-Path $deps 'bepinex.zip')).Hash;Archive='zip';Required=$true;Feature='core';Files=@(@{Source='winhttp.dll';Target='winhttp.dll'},@{Source='BepInEx/core/BepInEx.Core.dll';Target='BepInEx/core/BepInEx.Core.dll'})},
        @{Id='nvngx_dlss';Vendor='nvidia';Url='https://example.org/nvngx_dlss.dll';SHA256=(Get-FileHash (Join-Path $deps 'nvngx_dlss.file')).Hash;Archive='file';Required=$true;Feature='core';Files=@(@{Source='nvngx_dlss.dll';Target='nvngx_dlss.dll'})})}
    Put (Join-Path $package 'packaging/dependencies.lock.json') ($lock | ConvertTo-Json -Depth 8)
    # Pin the NVIDIA path so results do not depend on this machine's GPU.
    return @{GamePath=$game;PackagePath=$package;DependencyDirectory=$deps;GpuPath='Nvidia'}
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
function Add-NrFixture($Fixture) {
    Add-FgFixture $Fixture
    $model=Join-Path $Fixture.DependencyDirectory 'nvidia_nr.file'
    Put $model 'community-model-fixture'
    $lockPath=Join-Path $Fixture.PackagePath 'packaging/dependencies.lock.json'
    $lock=Get-Content $lockPath -Raw | ConvertFrom-Json
    $lock.Dependencies+=@{Id='nvidia_nr';Vendor='nvidia';Url='https://example.org/nvngx_dlssnr.dll';SHA256=(Get-FileHash $model).Hash;Archive='file';Required=$false;Feature='nr';Files=@(@{Source='nvngx_dlssnr.dll';Target='OptiScaler/nvngx_dlssnr.dll'})}
    Put $lockPath ($lock | ConvertTo-Json -Depth 8)
    Put (Join-Path $Fixture.PackagePath 'payload/defaults/OptiScaler.ini') "[OptiFG]`r`nEnabled=false`r`n[NeuralRendering]`r`nEnabled=false"
}
function Add-BridgeFixture($Fixture) {
    # Full optional set plus an NVIDIA-only FG runtime, as in the real lock.
    Add-NrFixture $Fixture
    $streamline=Join-Path $Fixture.DependencyDirectory 'streamline_fg.file'
    Put $streamline 'nvidia-only-fg-runtime'
    $lockPath=Join-Path $Fixture.PackagePath 'packaging/dependencies.lock.json'
    $lock=Get-Content $lockPath -Raw | ConvertFrom-Json
    $lock.Dependencies+=@{Id='streamline_fg';Vendor='nvidia';Url='https://example.org/sl.interposer.dll';SHA256=(Get-FileHash $streamline).Hash;Archive='file';Required=$false;Feature='fg';Files=@(@{Source='sl.interposer.dll';Target='OptiScaler/streamline/sl.interposer.dll'})}
    Put $lockPath ($lock | ConvertTo-Json -Depth 8)
}
try {
    Case 'Windows PowerShell parses the GUI entry point' {
        $parseErrors=$null
        [void][Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot '../scripts/Setup.ps1'),[ref]$null,[ref]$parseErrors)
        Assert (@($parseErrors).Count -eq 0) 'GUI parses in Windows PowerShell'
    }
    Case 'production GUI worker installs repairs diagnoses and removes without module autoload' {
        $parseErrors=$null
        $ast=[Management.Automation.Language.Parser]::ParseFile($SetupPath,[ref]$null,[ref]$parseErrors)
        Assert (@($parseErrors).Count -eq 0) 'GUI worker source parses'
        $calls=@($ast.FindAll({
            param($node)
            $node -is [Management.Automation.Language.InvokeMemberExpressionAst] -and $node.Member.Value -eq 'AddScript'
        },$true))
        Assert ($calls.Count -eq 1) 'one production AddScript worker'
        $workerBody=$calls[0].Arguments[0].ScriptBlock.GetScriptBlock().ToString()
        foreach ($context in @('Default','NoAutoload','Cold','LocalModel')) {
            $f=New-Fixture ('gui-worker-'+$context); Add-NrFixture $f
            $expectedModel='community-model-fixture'
            $local=$null
            if ($context -eq 'LocalModel') {
                $expectedModel='local-3109-model-fixture'
                $local=Join-Path $f.DependencyDirectory 'new-model.dll'; Put $local $expectedModel
                $lockPath=Join-Path $f.PackagePath 'packaging/dependencies.lock.json'
                $lock=Get-Content $lockPath -Raw | ConvertFrom-Json
                @($lock.Dependencies | Where-Object { $_.Id -eq 'nvidia_nr' })[0] | Add-Member NoteProperty LocalModelSHA256 (Get-FileHash $local).Hash
                Put $lockPath ($lock | ConvertTo-Json -Depth 8)
            }
            $modulePath=Join-Path $f.PackagePath 'scripts/Setup.Core.psm1'
            [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($modulePath))
            [IO.File]::Copy([IO.Path]::GetFullPath($SetupCorePath),$modulePath)
            $model=Join-Path $f.GamePath 'OptiScaler/nvngx_dlssnr.dll'
            $plugin=Join-Path $f.GamePath 'BepInEx/plugins/BigWalkDLSS/BigWalkDLSS.dll'
            $config=Join-Path $f.GamePath 'OptiScaler.ini'
            foreach ($action in @('Install','Install','Diagnose','Uninstall')) {
                $worker=[PowerShell]::Create()
                $runspace=$null
                try {
                    if ($context -eq 'Cold') {
                        # Simulate a host with core commands only and no module discovery.
                        $runspace=[Management.Automation.Runspaces.RunspaceFactory]::CreateRunspace([Management.Automation.Runspaces.InitialSessionState]::CreateDefault2())
                        $runspace.Open()
                        $runspace.SessionStateProxy.SetVariable('PSModuleAutoLoadingPreference','None')
                        $worker.Runspace=$runspace
                    } elseif ($context -eq 'NoAutoload') {
                        [void]$worker.AddScript({ $PSModuleAutoLoadingPreference='None' }).AddStatement()
                    }
                    $selected=@()
                    if (-not (Test-Path -LiteralPath $model)) { $selected=@('nr','fg') }
                    $workerModel=$null
                    if ($local -and -not (Test-Path -LiteralPath $model)) { $workerModel=$local }
                    [void]$worker.AddScript($workerBody).AddArgument($modulePath).AddArgument($f.GamePath).AddArgument($action).AddArgument($f.DependencyDirectory).AddArgument($selected).AddArgument('Nvidia').AddArgument($workerModel)
                    $operation=$worker.BeginInvoke()
                    $result=$worker.EndInvoke($operation)
                    Assert (-not $worker.HadErrors) ('GUI worker errors: '+[string]$worker.Streams.Error[0])
                } finally {
                    $worker.Dispose()
                    if ($runspace) { $runspace.Dispose() }
                }
                if ($action -eq 'Install') {
                    Assert ([IO.File]::ReadAllText($model) -eq $expectedModel) 'GUI worker installs and repairs selected NR model'
                    Assert ([IO.File]::ReadAllText($plugin) -eq 'plugin-one') 'GUI worker repairs missing owned plugin'
                    Assert ([IO.File]::ReadAllText((Join-Path $f.GamePath 'version.dll')) -eq 'experimental-fg-bridge') 'GUI worker includes bridge'
                    if ([IO.File]::ReadAllText($config) -ne 'user-NR-options') {
                        Assert (@([regex]::Matches([IO.File]::ReadAllText($config),'(?m)^Enabled=false\r?$')).Count -eq 2) 'GUI worker defaults leave FG and NR disabled'
                        Put $config 'user-NR-options'
                        [IO.File]::Delete($plugin)
                    }
                } elseif ($action -eq 'Diagnose') {
                    Assert (($result -join "`n") -match 'OK: OptiScaler/nvngx_dlssnr.dll') 'GUI worker diagnoses verified NR'
                    Assert ([IO.File]::ReadAllText($config) -eq 'user-NR-options') 'GUI worker repair preserves settings'
                } else {
                    Assert (-not (Test-Path -LiteralPath $model) -and -not (Test-Path -LiteralPath $plugin)) 'GUI worker removes owned files'
                    Assert ([IO.File]::ReadAllText($config) -eq 'user-NR-options') 'GUI worker uninstall preserves settings'
                    Assert ([IO.File]::ReadAllText((Join-Path $f.GamePath 'GameAssembly.dll')) -eq 'game-assembly-do-not-touch') 'GUI worker preserves game assembly'
                }
            }
        }
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
        Assert (@($state.Requests | Where-Object { $_.Timeout -ne 60000 }).Count -eq 0) 'each request gets the 60-second stall timeout, not a shrinking total budget'
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
        Assert ([IO.File]::ReadAllText((Join-Path $f.GamePath 'BepInEx/plugins/BigWalkDLSS/BigWalkDLSSNative.dll')) -eq 'native-one') 'NVIDIA path installs DLSS native plugin'
        Assert ([IO.File]::ReadAllText((Join-Path $f.GamePath 'BepInEx/plugins/BigWalkDLSS/BigWalkBridgeNative.dll')) -eq 'bridge-native-one') 'NVIDIA path also installs bridge native plugin'
        Assert ([IO.File]::ReadAllText((Join-Path $f.GamePath 'nvngx_dlss.dll')) -eq 'runtime-known') 'NVIDIA path installs DLSS runtime'
        Assert ((Invoke-BbwSetup @f -Mode Diagnose) -match 'GPU path: NVIDIA') 'diagnosis reports NVIDIA path'
        Assert (-not (Test-Path (Join-Path $f.GamePath 'OptiScaler.ini'))) 'core-only install does not create OptiScaler config'
        Assert ([IO.File]::ReadAllText((Join-Path $f.GamePath 'BepInEx/BigWalkDLSS/notices/NOTICE')) -eq 'fixture-NOTICE') 'attribution installed'
        Assert ([IO.File]::ReadAllText((Join-Path $f.GamePath 'BepInEx/BigWalkDLSS/notices/docs/licenses/vendor.txt')) -eq 'fixture-vendor-license') 'vendor license installed'
        $notices=Join-Path $f.GamePath 'BepInEx/BigWalkDLSS/notices'
        foreach ($document in Get-ChildItem -LiteralPath $notices -Filter '*.md' -Recurse -File) {
            foreach ($link in [regex]::Matches([IO.File]::ReadAllText($document.FullName),'\]\(([^)]+)\)')) {
                Assert (Test-Path -LiteralPath (Join-Path $document.DirectoryName $link.Groups[1].Value)) 'installed attribution link resolves'
            }
        }
        Uninstall $f | Out-Null
        Assert (-not (Test-Path (Join-Path $f.GamePath 'winhttp.dll'))) 'owned loader removed'
        Assert (-not (Test-Path (Join-Path $f.GamePath 'BepInEx/plugins/BigWalkDLSS/BigWalkDLSS.dll'))) 'plugin removed'
        Assert (-not (Test-Path (Join-Path $f.GamePath 'BepInEx/plugins/BigWalkDLSS/BigWalkBridgeNative.dll'))) 'bridge native plugin removed'
        Assert (-not (Test-Path (Join-Path $f.GamePath 'BepInEx/BigWalkDLSS/notices/NOTICE'))) 'owned notices removed when shared loader removed'
        Assert ([IO.File]::ReadAllText((Join-Path $f.GamePath 'GameAssembly.dll')) -eq 'game-assembly-do-not-touch') 'game assembly preserved'
    }
    Case 'UI artwork updates as an owned payload and preserves edited files' {
        $f=New-Fixture 'ui-artwork'
        $source=Join-Path $f.PackagePath 'packaging/ui-landscape.png'
        $target=Join-Path $f.GamePath 'BepInEx/plugins/BigWalkDLSS/ui-landscape.png'
        Put $source 'first-artwork-fixture'
        Install $f | Out-Null
        Assert ([IO.File]::ReadAllText($target) -eq 'first-artwork-fixture') 'artwork installs with the core'
        Put $source 'updated-artwork-fixture'
        Install $f | Out-Null
        Assert ([IO.File]::ReadAllText($target) -eq 'updated-artwork-fixture') 'owned artwork updates on repair'
        Put $target 'user-artwork-fixture'
        Expect-Failure { Uninstall $f } 'changed after installation'
        Assert ([IO.File]::ReadAllText($target) -eq 'user-artwork-fixture') 'uninstall preserves edited artwork'
        Put $target 'updated-artwork-fixture'
        Uninstall $f | Out-Null
        Assert (-not (Test-Path $target)) 'unchanged owned artwork removes on uninstall'
    }
    Case 'missing release attribution refuses before mutation' {
        $f=New-Fixture 'missing-notice'
        Remove-Item -LiteralPath (Join-Path $f.PackagePath 'NOTICE')
        Expect-Failure { Install $f } 'notices are incomplete'
        Assert (-not (Test-Path (Join-Path $f.GamePath 'winhttp.dll'))) 'missing notice cannot leave a partial install'
    }
    Case 'shared loader retains its attribution after mod removal' {
        $f=New-Fixture 'shared-notices'; Install $f | Out-Null
        Put (Join-Path $f.GamePath 'BepInEx/plugins/AnotherMod.dll') 'other-mod'
        Uninstall $f | Out-Null
        Assert (Test-Path (Join-Path $f.GamePath 'winhttp.dll')) 'shared loader retained'
        Assert (Test-Path (Join-Path $f.GamePath 'BepInEx/BigWalkDLSS/notices/docs/licenses/vendor.txt')) 'shared loader licenses retained'
    }
    Case 'retained shared notices remain repairable and removable after another mod leaves' {
        $f=New-Fixture 'retained-ownership'; Install $f | Out-Null
        $other=Join-Path $f.GamePath 'BepInEx/plugins/AnotherMod.dll'
        Put $other 'other-mod'
        Uninstall $f | Out-Null
        $manifest=Get-Content -LiteralPath (Join-Path $f.GamePath '.big-beautiful-walk/manifest.json') -Raw | ConvertFrom-Json
        Assert (@($manifest.Entries | Where-Object { $_.Role -eq 'plugin' }).Count -eq 0) 'removed plugin ownership discarded'
        Assert (@($manifest.Entries | Where-Object { $_.Role -eq 'notices' }).Count -gt 0) 'retained notices stay owned'
        Put (Join-Path $f.PackagePath 'NOTICE') 'updated-attribution'
        Install $f | Out-Null
        Assert ([IO.File]::ReadAllText((Join-Path $f.GamePath 'BepInEx/BigWalkDLSS/notices/NOTICE')) -eq 'updated-attribution') 'known retained attribution updates without collision'
        Uninstall $f | Out-Null
        Assert (Test-Path -LiteralPath $other) 'other mod preserved through reinstall and removal'
        Remove-Item -LiteralPath $other
        Uninstall $f | Out-Null
        Assert (-not (Test-Path (Join-Path $f.GamePath 'winhttp.dll'))) 'owned shared loader removable after last other mod leaves'
        Assert (-not (Test-Path (Join-Path $f.GamePath 'BepInEx/BigWalkDLSS/notices/NOTICE'))) 'no orphan notices after shared loader removal'
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
    Case 'default repair updates already-installed optional bridge and preserves settings' {
        $f=New-Fixture 'optional-default-repair'; Add-FgFixture $f
        Invoke-BbwSetup @f -Mode Install -Features fg | Out-Null
        $config=Join-Path $f.GamePath 'OptiScaler.ini'; Put $config 'user-kept-options'
        $bridge=Join-Path $f.DependencyDirectory 'fg_bridge.file'; Put $bridge 'verified-bridge-update'
        $lockPath=Join-Path $f.PackagePath 'packaging/dependencies.lock.json'
        $lock=Get-Content $lockPath -Raw | ConvertFrom-Json
        @($lock.Dependencies | Where-Object { $_.Id -eq 'fg_bridge' })[0].SHA256=(Get-FileHash $bridge).Hash
        Put $lockPath ($lock | ConvertTo-Json -Depth 8)
        Install $f | Out-Null
        Assert ([IO.File]::ReadAllText((Join-Path $f.GamePath 'version.dll')) -eq 'verified-bridge-update') 'default repair includes already-owned optional files'
        Assert ([IO.File]::ReadAllText($config) -eq 'user-kept-options') 'default optional repair preserves edited configuration'
        Uninstall $f | Out-Null
    }
    Case 'optional FG preserves preexisting OptiScaler config bytes' {
        $f=New-Fixture 'optional-fg-existing'; Add-FgFixture $f
        $config=Join-Path $f.GamePath 'OptiScaler.ini'; Put $config 'existing-custom-settings'
        Invoke-BbwSetup @f -Mode Install -Features fg | Out-Null
        Assert ([IO.File]::ReadAllText($config) -eq 'existing-custom-settings') 'optional install preserves existing Opti bytes'
        Uninstall $f | Out-Null
        Assert ([IO.File]::ReadAllText($config) -eq 'existing-custom-settings') 'optional uninstall preserves existing Opti bytes'
    }
    Case 'optional NR implies bridge repairs ownership and preserves disabled defaults' {
        $f=New-Fixture 'optional-nr'; Add-NrFixture $f
        $message=Invoke-BbwSetup @f -Mode Install -Features nr
        $model=Join-Path $f.GamePath 'OptiScaler/nvngx_dlssnr.dll'
        $bridge=Join-Path $f.GamePath 'version.dll'
        $config=Join-Path $f.GamePath 'OptiScaler.ini'
        Assert ([IO.File]::ReadAllText($model) -eq 'community-model-fixture') 'NR model installed'
        Assert ([IO.File]::ReadAllText($bridge) -eq 'experimental-fg-bridge') 'NR includes bridge on fresh install'
        Assert (@([regex]::Matches([IO.File]::ReadAllText($config),'(?m)^Enabled=false\r?$')).Count -eq 2) 'fresh FG and NR remain disabled'
        Assert ($message -match 'community RHI SF-v2' -and $message -match 'does not enable Neural Rendering') 'completion identifies optional model without claiming enabled NR'
        $manifest=Get-Content (Join-Path $f.GamePath '.big-beautiful-walk/manifest.json') -Raw | ConvertFrom-Json
        Assert (@($manifest.Entries | Where-Object { $_.Role -eq 'nvidia_nr' }).Count -eq 1) 'NR ownership recorded'
        Put $config 'existing-NR-options'
        # Change verified fixture inputs to prove unchecked repair selects both owned packs.
        $modelInput=Join-Path $f.DependencyDirectory 'nvidia_nr.file'; Put $modelInput 'verified-model-update'
        $bridgeInput=Join-Path $f.DependencyDirectory 'fg_bridge.file'; Put $bridgeInput 'verified-bridge-update'
        $lockPath=Join-Path $f.PackagePath 'packaging/dependencies.lock.json'
        $lock=Get-Content $lockPath -Raw | ConvertFrom-Json
        @($lock.Dependencies | Where-Object { $_.Id -eq 'nvidia_nr' })[0].SHA256=(Get-FileHash $modelInput).Hash
        @($lock.Dependencies | Where-Object { $_.Id -eq 'fg_bridge' })[0].SHA256=(Get-FileHash $bridgeInput).Hash
        Put $lockPath ($lock | ConvertTo-Json -Depth 8)
        Install $f | Out-Null
        Assert ([IO.File]::ReadAllText($model) -eq 'verified-model-update') 'unchecked repair includes owned NR'
        Assert ([IO.File]::ReadAllText($bridge) -eq 'verified-bridge-update') 'unchecked NR repair retains bridge'
        Uninstall $f | Out-Null
        Assert (-not (Test-Path $model) -and -not (Test-Path $bridge)) 'owned model and bridge removed'
        Assert ([IO.File]::ReadAllText($config) -eq 'existing-NR-options') 'NR settings preserved through repair and removal'
    }
    Case 'local NR upgrades owned model and survives ordinary repair without a downgrade' {
        $f=New-Fixture 'local-nr-upgrade'; Add-NrFixture $f
        Invoke-BbwSetup @f -Mode Install -Features nr | Out-Null
        $local=Join-Path $f.DependencyDirectory 'new-model.dll'; Put $local 'local-3109-model-fixture'
        $lockPath=Join-Path $f.PackagePath 'packaging/dependencies.lock.json'
        $lock=Get-Content $lockPath -Raw | ConvertFrom-Json
        $nr=@($lock.Dependencies | Where-Object { $_.Id -eq 'nvidia_nr' })[0]
        $nr | Add-Member NoteProperty LocalModelSHA256 (Get-FileHash $local).Hash
        Put $lockPath ($lock | ConvertTo-Json -Depth 8)
        $model=Join-Path $f.GamePath 'OptiScaler/nvngx_dlssnr.dll'
        $config=Join-Path $f.GamePath 'OptiScaler.ini'; Put $config 'user-NR-settings'
        $message=Invoke-BbwSetup @f -Mode Install -NeuralModelPath $local
        Assert ($message -match 'local NR 310.9.16' -and $message -match 'does not enable') 'local model result identifies version and runtime limit'
        Assert ([IO.File]::ReadAllText($model) -eq 'local-3109-model-fixture') 'owned old model upgraded'
        Remove-Item -LiteralPath (Join-Path $f.DependencyDirectory 'nvidia_nr.file')
        Install $f | Out-Null
        Assert ([IO.File]::ReadAllText($model) -eq 'local-3109-model-fixture') 'repair retains local model without old archive'
        Assert ([IO.File]::ReadAllText($config) -eq 'user-NR-settings') 'upgrade and repair preserve settings'
        Remove-Item -LiteralPath $model
        Expect-Failure { Install $f } 'imported NR 310.9.16 model is missing'
        Invoke-BbwSetup @f -Mode Install -NeuralModelPath $local | Out-Null
        Uninstall $f | Out-Null
        Assert (-not (Test-Path $model)) 'local model remains owned for uninstall'
        Assert ([IO.File]::ReadAllText($local) -eq 'local-3109-model-fixture') 'source model remains unchanged'
    }
    Case 'local NR fresh install implies bridge and rejects unpinned bytes before mutation' {
        $f=New-Fixture 'local-nr-fresh'; Add-NrFixture $f
        $local=Join-Path $f.DependencyDirectory 'new-model.dll'; Put $local 'local-3109-model-fixture'
        $lockPath=Join-Path $f.PackagePath 'packaging/dependencies.lock.json'
        $lock=Get-Content $lockPath -Raw | ConvertFrom-Json
        $nr=@($lock.Dependencies | Where-Object { $_.Id -eq 'nvidia_nr' })[0]
        $nr | Add-Member NoteProperty LocalModelSHA256 (Get-FileHash $local).Hash
        Put $lockPath ($lock | ConvertTo-Json -Depth 8)
        Put $local 'wrong-model'
        Expect-Failure { Invoke-BbwSetup @f -Mode Install -NeuralModelPath $local } 'SHA256 mismatch for local'
        Assert (-not (Test-Path (Join-Path $f.GamePath 'winhttp.dll'))) 'wrong model cannot partially install core'
        Put $local 'local-3109-model-fixture'
        Invoke-BbwSetup @f -Mode Install -NeuralModelPath $local | Out-Null
        Assert (Test-Path (Join-Path $f.GamePath 'version.dll')) 'local NR automatically installs bridge'
        $model=Join-Path $f.GamePath 'OptiScaler/nvngx_dlssnr.dll'
        Assert ([IO.File]::ReadAllText($model) -eq 'local-3109-model-fixture') 'verified local model installed'
        $config=Get-Content (Join-Path $f.GamePath 'OptiScaler.ini') -Raw
        Assert (@([regex]::Matches($config,'(?m)^Enabled=false\r?$')).Count -eq 2) 'local model leaves fresh FG and NR off'
        Put $model 'user-edited-model'
        Expect-Failure { Invoke-BbwSetup @f -Mode Install -NeuralModelPath $local } 'changed after installation'
        Assert ([IO.File]::ReadAllText($model) -eq 'user-edited-model') 'edited installed model preserved'
    }
    Case 'local NR refuses AMD Intel path and unknown ownership' {
        $f=New-Fixture 'local-nr-bridge'; Add-NrFixture $f
        $local=Join-Path $f.DependencyDirectory 'new-model.dll'; Put $local 'local-3109-model-fixture'
        $lockPath=Join-Path $f.PackagePath 'packaging/dependencies.lock.json'
        $lock=Get-Content $lockPath -Raw | ConvertFrom-Json
        @($lock.Dependencies | Where-Object { $_.Id -eq 'nvidia_nr' })[0] | Add-Member NoteProperty LocalModelSHA256 (Get-FileHash $local).Hash
        Put $lockPath ($lock | ConvertTo-Json -Depth 8)
        $f.GpuPath='Bridge'
        Expect-Failure { Invoke-BbwSetup @f -Mode Install -NeuralModelPath $local } 'requires the NVIDIA GPU path'
        Assert (-not (Test-Path (Join-Path $f.GamePath 'winhttp.dll'))) 'bridge rejection leaves core untouched'
        $f.GpuPath='Nvidia'
        $model=Join-Path $f.GamePath 'OptiScaler/nvngx_dlssnr.dll'; Put $model 'other-mod-model'
        Expect-Failure { Invoke-BbwSetup @f -Mode Install -NeuralModelPath $local } 'Existing file collision'
        Assert ([IO.File]::ReadAllText($model) -eq 'other-mod-model') 'another installation is never overwritten'
    }
    Case 'FG-only repair never opts into available NR model' {
        $f=New-Fixture 'fg-without-nr'; Add-NrFixture $f
        Invoke-BbwSetup @f -Mode Install -Features fg | Out-Null
        Install $f | Out-Null
        Assert (-not (Test-Path (Join-Path $f.GamePath 'OptiScaler/nvngx_dlssnr.dll'))) 'available NR model remains unselected on FG-only repair'
        $manifest=Get-Content (Join-Path $f.GamePath '.big-beautiful-walk/manifest.json') -Raw | ConvertFrom-Json
        Assert (@($manifest.Entries | Where-Object { $_.Role -eq 'nvidia_nr' }).Count -eq 0) 'FG ownership does not claim NR'
        Uninstall $f | Out-Null
    }
    Case 'optional NR preserves exact preexisting model and refuses changed owned model' {
        $f=New-Fixture 'nr-existing'; Add-NrFixture $f
        $model=Join-Path $f.GamePath 'OptiScaler/nvngx_dlssnr.dll'; Put $model 'community-model-fixture'
        $config=Join-Path $f.GamePath 'OptiScaler.ini'; Put $config 'preexisting-NR-options'
        Invoke-BbwSetup @f -Mode Install -Features nr | Out-Null
        Uninstall $f | Out-Null
        Assert ([IO.File]::ReadAllText($model) -eq 'community-model-fixture') 'exact preexisting model remains unowned and preserved'
        Assert ([IO.File]::ReadAllText($config) -eq 'preexisting-NR-options') 'preexisting NR config preserved'
        $f=New-Fixture 'nr-changed'; Add-NrFixture $f
        Invoke-BbwSetup @f -Mode Install -Features nr | Out-Null
        $model=Join-Path $f.GamePath 'OptiScaler/nvngx_dlssnr.dll'; Put $model 'changed-model'
        Expect-Failure { Install $f } 'changed after installation'
        Expect-Failure { Uninstall $f } 'changed after installation'
        Assert ([IO.File]::ReadAllText($model) -eq 'changed-model') 'modified owned model is never overwritten or removed'
        Assert (Test-Path (Join-Path $f.GamePath 'version.dll')) 'changed NR model prevents partial bridge removal'
        $f=New-Fixture 'nr-collision'; Add-NrFixture $f
        $model=Join-Path $f.GamePath 'OptiScaler/nvngx_dlssnr.dll'; Put $model 'different-preexisting-model'
        Expect-Failure { Invoke-BbwSetup @f -Mode Install -Features nr } 'Existing file collision'
        Assert ([IO.File]::ReadAllText($model) -eq 'different-preexisting-model') 'different preexisting model is preserved'
        Assert (-not (Test-Path (Join-Path $f.GamePath 'version.dll'))) 'model collision prevents partial bridge install'
    }
    Case 'AMD/Intel bridge path forces the bridge, skips NVIDIA runtimes and uses bridge defaults' {
        $f=New-Fixture 'bridge-fresh'; Add-BridgeFixture $f; $f.GpuPath='Bridge'
        # NVIDIA-vendor inputs are absent offline: the bridge path must neither require nor fetch them.
        foreach ($name in @('nvngx_dlss.file','nvidia_nr.file','streamline_fg.file')) { Remove-Item -LiteralPath (Join-Path $f.DependencyDirectory $name) }
        $message=Install $f
        Assert ($message -match 'AMD/Intel GPU path used' -and $message -match 'bridge') 'completion names the AMD/Intel path and bridge'
        Assert ($message -notmatch 'Neural Rendering model was installed') 'completion does not claim NR'
        Assert ([IO.File]::ReadAllText((Join-Path $f.GamePath 'version.dll')) -eq 'experimental-fg-bridge') 'bridge installed without FG being selected'
        Assert ([IO.File]::ReadAllText((Join-Path $f.GamePath 'BepInEx/plugins/BigWalkDLSS/BigWalkBridgeNative.dll')) -eq 'bridge-native-one') 'bridge native plugin installed'
        Assert (-not (Test-Path (Join-Path $f.GamePath 'BepInEx/plugins/BigWalkDLSS/BigWalkDLSSNative.dll'))) 'DLSS native plugin not installed'
        Assert (-not (Test-Path (Join-Path $f.GamePath 'nvngx_dlss.dll'))) 'NVIDIA DLSS runtime skipped'
        Assert (-not (Test-Path (Join-Path $f.GamePath 'OptiScaler/streamline/sl.interposer.dll'))) 'NVIDIA FG runtime skipped'
        Assert (-not (Test-Path (Join-Path $f.GamePath 'OptiScaler/nvngx_dlssnr.dll'))) 'NR model skipped'
        Assert ([IO.File]::ReadAllText((Join-Path $f.GamePath 'OptiScaler.ini')) -match 'fsr31') 'bridge OptiScaler default installed'
        $manifest=Get-Content (Join-Path $f.GamePath '.big-beautiful-walk/manifest.json') -Raw | ConvertFrom-Json
        Assert (@($manifest.Entries | Where-Object { $_.Role -in @('nvngx_dlss','streamline_fg','nvidia_nr') }).Count -eq 0) 'no NVIDIA runtime ownership recorded'
        Assert ((Invoke-BbwSetup @f -Mode Diagnose) -match 'GPU path: AMD/Intel') 'diagnosis reports the bridge path'
        Install $f | Out-Null
        Uninstall $f | Out-Null
        Assert (-not (Test-Path (Join-Path $f.GamePath 'BepInEx/plugins/BigWalkDLSS/BigWalkBridgeNative.dll'))) 'bridge native plugin removed'
        Assert (-not (Test-Path (Join-Path $f.GamePath 'version.dll'))) 'bridge removed'
        Assert (-not (Test-Path (Join-Path $f.GamePath 'winhttp.dll'))) 'own bridge native plugin does not count as another mod'
    }
    Case 'AMD/Intel bridge path rejects Neural Rendering and a release without the bridge before any change' {
        $f=New-Fixture 'bridge-nr'; Add-BridgeFixture $f; $f.GpuPath='Bridge'
        foreach ($request in @(@('nr'),@('fg','NR'))) {
            Expect-Failure { Invoke-BbwSetup @f -Mode Install -Features $request } 'Neural Rendering needs an NVIDIA RTX GPU; it is not available on AMD or Intel GPUs\. No game files were changed\.'
        }
        Assert (-not (Test-Path (Join-Path $f.GamePath '.big-beautiful-walk'))) 'NR rejection leaves no state'
        Assert (-not (Test-Path (Join-Path $f.GamePath 'BepInEx'))) 'NR rejection installs nothing'
        $core=New-Fixture 'bridge-no-runtime'; $core.GpuPath='Bridge'
        Expect-Failure { Install $core } 'no verified bridge runtime'
        Assert (-not (Test-Path (Join-Path $core.GamePath '.big-beautiful-walk'))) 'missing bridge runtime leaves no state'
    }
    Case 'AMD/Intel path switches off NVIDIA-only settings kept from an NVIDIA install' {
        $f=New-Fixture 'bridge-kept-settings'; Add-BridgeFixture $f; $f.GpuPath='Bridge'
        $config=Join-Path $f.GamePath 'OptiScaler.ini'
        $kept="[Upscalers]`r`nDx11Upscaler = dlss_12 ; chosen in F10`r`n[FrameGen]`r`nEnabled=true`r`nFGOutput=DLSSG`r`n[DlssNr]`r`nEnabled = true`r`nStyle = 2`r`n[Other]`r`nEnabled=true`r`n"
        Put $config $kept
        $message=Install $f
        $expected="[Upscalers]`r`nDx11Upscaler = fsr31 ; chosen in F10`r`n[FrameGen]`r`nEnabled=false`r`nFGOutput=DLSSG`r`n[DlssNr]`r`nEnabled = false`r`nStyle = 2`r`n[Other]`r`nEnabled=true`r`n[Spoofing]`r`nDxgi=false`r`n"
        Assert ([IO.File]::ReadAllText($config) -eq $expected) 'only DLSS, NVIDIA FG and NR values changed, formatting kept'
        Assert ($message -match 'OptiScaler.ini was updated for this GPU') 'settings update reported'
        $backups=@(Get-ChildItem (Join-Path $f.GamePath '.big-beautiful-walk/config-backups') -Filter 'OptiScaler-*.ini')
        Assert ($backups.Count -eq 1 -and [IO.File]::ReadAllText($backups[0].FullName) -eq $kept) 'previous settings backed up'
        Install $f | Out-Null
        Assert (@(Get-ChildItem (Join-Path $f.GamePath '.big-beautiful-walk/config-backups') -Filter 'OptiScaler-*.ini').Count -eq 1) 'already corrected settings left alone'
        $amd="[Upscalers]`r`nDx11Upscaler=fsr31`r`n[FrameGen]`r`nEnabled=true`r`nFGOutput=FSRFG`r`n[Spoofing]`r`nDxgi = false`r`n"
        Put $config $amd
        Install $f | Out-Null
        Assert ([IO.File]::ReadAllText($config) -eq $amd) 'AMD Frame Generation request kept'
        # An ANSI file with an accented path is left untouched and reported, never re-encoded.
        $ansi=[byte[]](@([Text.Encoding]::ASCII.GetBytes("[Upscalers]`r`nDx11Upscaler=dlss_12`r`n[Paths]`r`nLibrary=C:\Caf")) + @(0xE9) + @([Text.Encoding]::ASCII.GetBytes("`r`n")))
        [IO.File]::WriteAllBytes($config,$ansi)
        $message=Install $f
        Assert ([Linq.Enumerable]::SequenceEqual([byte[]][IO.File]::ReadAllBytes($config),$ansi)) 'non-UTF-8 settings rewritten'
        Assert ($message -match 'could not be updated for this GPU') 'unsupported settings encoding not reported'
        Assert (@(Get-ChildItem $f.GamePath -Filter 'OptiScaler.ini.*.tmp').Count -eq 0) 'temporary settings file left behind'
        # Hybrid PCs keep the NVIDIA path: only spoofing changes, inserted into an existing [Spoofing] section.
        $hybridKept="[Upscalers]`r`nDx11Upscaler=dlss_12`r`n[FrameGen]`r`nEnabled=true`r`nFGOutput=DLSSG`r`n[Spoofing]`r`nDxgiVRAM=auto`r`n"
        Put $config $hybridKept
        $module=Get-Module Setup.Core
        $hybridChanges=@(& $module { param($Game) Update-BbwBridgeSettings $Game -SpoofingOnly } $f.GamePath)
        Assert ([IO.File]::ReadAllText($config) -eq "[Upscalers]`r`nDx11Upscaler=dlss_12`r`n[FrameGen]`r`nEnabled=true`r`nFGOutput=DLSSG`r`n[Spoofing]`r`nDxgi=false`r`nDxgiVRAM=auto`r`n") 'hybrid settings changed beyond spoofing'
        Assert ($hybridChanges.Count -eq 1 -and $hybridChanges[0] -match 'spoofing') 'hybrid spoofing change not reported'
        Put $config "[Spoofing]`r`nDxgi=auto`r`n"
        [void](& $module { param($Game) Update-BbwBridgeSettings $Game -SpoofingOnly } $f.GamePath)
        Assert ([IO.File]::ReadAllText($config) -eq "[Spoofing]`r`nDxgi=false`r`n") 'automatic spoofing left on'
        Uninstall $f | Out-Null
    }
    Case 'switching an NVIDIA install to the bridge path removes the owned DLSS native plugin and back' {
        $f=New-Fixture 'bridge-switch'; Add-BridgeFixture $f
        Invoke-BbwSetup @f -Mode Install -Features nr | Out-Null
        $dlssNative=Join-Path $f.GamePath 'BepInEx/plugins/BigWalkDLSS/BigWalkDLSSNative.dll'
        $model=Join-Path $f.GamePath 'OptiScaler/nvngx_dlssnr.dll'
        $config=Join-Path $f.GamePath 'OptiScaler.ini'
        Assert ([IO.File]::ReadAllText($dlssNative) -eq 'native-one') 'NVIDIA install owns DLSS native plugin'
        Assert ([IO.File]::ReadAllText((Join-Path $f.GamePath 'OptiScaler/streamline/sl.interposer.dll')) -eq 'nvidia-only-fg-runtime') 'NVIDIA FG installs NVIDIA-vendor runtime'
        Put $config 'user-opti-settings'
        $f.GpuPath='Bridge'
        Install $f | Out-Null
        Assert (-not (Test-Path -LiteralPath $dlssNative)) 'owned DLSS native plugin removed on bridge path'
        Assert ([IO.File]::ReadAllText((Join-Path $f.GamePath 'BepInEx/plugins/BigWalkDLSS/BigWalkBridgeNative.dll')) -eq 'bridge-native-one') 'bridge native plugin kept'
        Assert ([IO.File]::ReadAllText($config) -eq "user-opti-settings`n[Spoofing]`nDxgi=false`n") 'edited OptiScaler settings preserved on switch, spoofing turned off'
        Assert ([IO.File]::ReadAllText($model) -eq 'community-model-fixture') 'owned NR model left in place on bridge repair'
        $manifest=Get-Content (Join-Path $f.GamePath '.big-beautiful-walk/manifest.json') -Raw | ConvertFrom-Json
        Assert (@($manifest.Entries | Where-Object { $_.Target -eq 'BepInEx/plugins/BigWalkDLSS/BigWalkDLSSNative.dll' }).Count -eq 0) 'DLSS native plugin ownership released'
        Assert (@($manifest.Entries | Where-Object { $_.Role -eq 'nvidia_nr' }).Count -eq 1) 'NR model stays owned for uninstall'
        Assert ((Invoke-BbwSetup @f -Mode Diagnose) -notmatch 'MISSING') 'bridge switch leaves no missing owned files'
        Install $f | Out-Null
        $f.GpuPath='Nvidia'
        Install $f | Out-Null
        Assert ([IO.File]::ReadAllText($dlssNative) -eq 'native-one') 'NVIDIA path reinstalls DLSS native plugin'
        Uninstall $f | Out-Null
        foreach ($path in @($dlssNative,$model,(Join-Path $f.GamePath 'nvngx_dlss.dll'),(Join-Path $f.GamePath 'version.dll'),(Join-Path $f.GamePath 'BepInEx/plugins/BigWalkDLSS/BigWalkBridgeNative.dll'))) {
            Assert (-not (Test-Path -LiteralPath $path)) "owned file removed after switching back: $path"
        }
        Assert ([IO.File]::ReadAllText($config) -eq "user-opti-settings`n[Spoofing]`nDxgi=false`n") 'settings preserved through switch and uninstall'
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
    Case 'game folder under a junctioned library installs and uninstalls' {
        $f=New-Fixture 'linked-library'
        $link=Join-Path $scratch 'linked-library-junction'
        [void](New-Item -ItemType Junction -Path $link -Target (Split-Path $f.GamePath -Parent))
        try {
            $linked=@{GamePath=(Join-Path $link 'game');PackagePath=(Join-Path $link 'package');DependencyDirectory=(Join-Path $link 'deps');GpuPath='Nvidia'}
            $module=Get-Module Setup.Core
            Assert ((& $module { param($Path) Get-BbwMutexName $Path } $linked.GamePath) -eq (& $module { param($Path) Get-BbwMutexName $Path } $f.GamePath)) 'alias and physical path share one installer lock'
            Assert ((& $module { param($Path) Get-BbwFinalPath $Path } $linked.GamePath) -eq (& $module { param($Path) Get-BbwFinalPath $Path } $f.GamePath)) 'alias resolves to the physical game folder'
            Install $linked | Out-Null
            Assert ([IO.File]::ReadAllText((Join-Path $f.GamePath 'BepInEx/plugins/BigWalkDLSS/BigWalkDLSS.dll')) -eq 'plugin-one') 'installed through the junctioned library'
            Uninstall $linked | Out-Null
            Assert (-not (Test-Path (Join-Path $f.GamePath 'BepInEx/plugins/BigWalkDLSS/BigWalkDLSS.dll'))) 'removed through the junctioned library'
        }
        finally { [IO.Directory]::Delete($link) }
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
    if ($script:passed -eq 0) { throw 'No installer cases matched the requested filter.' }
    Write-Output "PASS $script:passed disposable installer integration cases."
} finally {
    $resolved=[IO.Path]::GetFullPath($scratch)
    $expected=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../work')).TrimEnd('\') + '\'
    if (-not $resolved.StartsWith($expected,[StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe scratch cleanup path.' }
    if (Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Recurse -Force }
}
