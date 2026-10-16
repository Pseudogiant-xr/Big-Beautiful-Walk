$ErrorActionPreference='Stop'
$scratch=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot ('../work/launcher-test-'+[guid]::NewGuid().ToString('N'))))
$package=Join-Path $scratch 'package with spaces & literal'
$process=$null
try {
    [void][IO.Directory]::CreateDirectory((Join-Path $package 'scripts'))
    & (Join-Path $PSScriptRoot '../scripts/Build-Setup.ps1') -Destination (Join-Path $package 'Setup.exe')
    $bytes=[IO.File]::ReadAllBytes((Join-Path $package 'Setup.exe'))
    $pe=[BitConverter]::ToInt32($bytes,60)
    if ([BitConverter]::ToUInt16($bytes,$pe+24+68) -ne 2) { throw 'Setup.exe must use the Windows GUI subsystem.' }
    $stub=@'
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetDirectoryName([IO.Path]::GetDirectoryName($PSCommandPath))
$hash=(Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash
[IO.File]::WriteAllText([IO.Path]::Combine($root,'launcher-result.txt'),('script-completed;'+$PSVersionTable.PSVersion.Major+';'+$hash.Length))
'@
    [IO.File]::WriteAllText((Join-Path $package 'scripts/Setup.ps1'),$stub)
    $start=New-Object Diagnostics.ProcessStartInfo
    $start.FileName=Join-Path $package 'Setup.exe'
    $start.UseShellExecute=$false
    # Started from a PowerShell 7 console, the launcher inherits PowerShell 7's module path.
    $start.EnvironmentVariables['PSModulePath']=(Join-Path $PSHOME 'Modules')+';'+$env:PSModulePath
    $process=[Diagnostics.Process]::Start($start)
    if (-not $process.WaitForExit(30000)) { throw 'Launcher test did not finish; inspect the owned test process.' }
    if ($process.ExitCode -ne 0) { throw "Launcher exited with code $($process.ExitCode)." }
    if ([IO.File]::ReadAllText((Join-Path $package 'launcher-result.txt')) -ne 'script-completed;5;64') { throw 'Launcher did not complete the matching Windows PowerShell script with its built-in modules.' }
    # Console handles do not establish whether a terminal is visible; visual acceptance is separate.
    Write-Output 'PASS GUI launcher: GUI subsystem, literal paths with spaces/ampersand, Windows PowerShell script completed with built-in modules.'

    # Setup.cmd started from a PowerShell 7 console inherits PowerShell 7's PSModulePath. Windows
    # PowerShell 5.1 must still load its own built-in modules (Get-FileHash), and the console
    # wrapper must return the script's exit code after its optional pause.
    $console=Join-Path $scratch 'console package & spaces'
    [void][IO.Directory]::CreateDirectory((Join-Path $console 'scripts'))
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot '../Setup.cmd') -Destination (Join-Path $console 'Setup.cmd')
    $consoleStub=@'
param([int]$ExitCode=0)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetDirectoryName([IO.Path]::GetDirectoryName($PSCommandPath))
$hash=(Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash
[IO.File]::WriteAllText([IO.Path]::Combine($root,'console-result.txt'),('hashed;'+$PSVersionTable.PSVersion.Major+';'+$hash.Length))
exit $ExitCode
'@
    [IO.File]::WriteAllText((Join-Path $console 'scripts/Setup.ps1'),$consoleStub)
    $savedModulePath=$env:PSModulePath
    try {
        $env:PSModulePath=(Join-Path $PSHOME 'Modules')+';'+$env:PSModulePath
        $setupCmd=Join-Path $console 'Setup.cmd'
        & cmd.exe /d /c "`"$setupCmd`" < NUL" | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "Setup.cmd failed under an inherited PowerShell 7 module path (exit $LASTEXITCODE)." }
        if ([IO.File]::ReadAllText((Join-Path $console 'console-result.txt')) -ne 'hashed;5;64') { throw 'Setup.cmd did not run Windows PowerShell with its built-in modules.' }
        & cmd.exe /d /c "`"$setupCmd`" -ExitCode 7 < NUL" | Out-Null
        if ($LASTEXITCODE -ne 7) { throw "Setup.cmd returned $LASTEXITCODE instead of the script's exit code 7." }
    } finally { $env:PSModulePath=$savedModulePath }
    Write-Output 'PASS console wrapper: built-in modules load under an inherited PowerShell 7 module path; script exit code returned.'
} finally {
    $canRemove=$true
    if ($process) {
        $canRemove=$process.HasExited
        $process.Dispose()
    }
    $expected=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../work')).TrimEnd('\')+'\'
    if (-not $scratch.StartsWith($expected,[StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($scratch) -notlike 'launcher-test-*') { throw 'Launcher fixture cleanup escaped its workspace.' }
    if ($canRemove -and (Test-Path -LiteralPath $scratch)) { Remove-Item -LiteralPath $scratch -Recurse -Force }
}
