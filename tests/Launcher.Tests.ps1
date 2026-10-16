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
[IO.File]::WriteAllText([IO.Path]::Combine($root,'launcher-result.txt'),('script-completed;'+$PSVersionTable.PSVersion.Major))
'@
    [IO.File]::WriteAllText((Join-Path $package 'scripts/Setup.ps1'),$stub)
    $start=New-Object Diagnostics.ProcessStartInfo
    $start.FileName=Join-Path $package 'Setup.exe'
    $start.UseShellExecute=$false
    $process=[Diagnostics.Process]::Start($start)
    if (-not $process.WaitForExit(30000)) { throw 'Launcher test did not finish; inspect the owned test process.' }
    if ($process.ExitCode -ne 0) { throw "Launcher exited with code $($process.ExitCode)." }
    if ([IO.File]::ReadAllText((Join-Path $package 'launcher-result.txt')) -ne 'script-completed;5') { throw 'Launcher did not complete the matching Windows PowerShell script.' }
    # Console handles do not establish whether a terminal is visible; visual acceptance is separate.
    Write-Output 'PASS GUI launcher: GUI subsystem, literal paths with spaces/ampersand, Windows PowerShell script completed.'
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
