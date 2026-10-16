$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0
Import-Module (Join-Path $PSScriptRoot '../scripts/Setup.Core.psm1') -Force
# Exercise production selection without installing files or deleting a DLL.
& (Get-Module Setup.Core) {
    $dependencies = @(
        [pscustomobject]@{Id='bepinex';Feature='core'},
        [pscustomobject]@{Id='optiscaler_fg';Feature='fg'},
        [pscustomobject]@{Id='xess';Feature='fg'},
        [pscustomobject]@{Id='nvidia_nr';Feature='nr'})
    $cases = @(
        @{Name='fresh core stays core';Requested=@();Roles=@();Expected=''},
        @{Name='explicit bridge opt-in';Requested=@('fg');Roles=@();Expected='fg'},
        @{Name='explicit NR includes bridge';Requested=@('nr');Roles=@();Expected='nr,fg'},
        @{Name='owned NR repair includes bridge';Requested=@();Roles=@('nvidia_nr');Expected='nr,fg'},
        @{Name='owned NR retained with bridge request';Requested=@('fg');Roles=@('nvidia_nr');Expected='fg,nr'},
        @{Name='owned bridge repaired with unchecked box';Requested=@();Roles=@('optiscaler_fg');Expected='fg'},
        @{Name='missing owned XeSS still selects its pack';Requested=@();Roles=@('xess');Expected='fg'},
        @{Name='core ownership does not opt in';Requested=@();Roles=@('bepinex');Expected=''},
        @{Name='unknown roles do not opt in';Requested=@();Roles=@('unrelated','config');Expected=''},
        @{Name='repeated roles and requests deduplicate';Requested=@('fg','FG');Roles=@('optiscaler_fg','xess');Expected='fg'},
        @{Name='repeated NR requests deduplicate bridge';Requested=@('nr','NR');Roles=@('nvidia_nr');Expected='nr,fg'},
        @{Name='unknown explicit request retained for validation';Requested=@('unavailable');Roles=@();Expected='unavailable'})
    foreach ($case in $cases) {
        $actual = @(Get-BbwRepairFeatures $dependencies $case.Requested $case.Roles)
        if (($actual -join ',') -ne $case.Expected) { throw "Feature selection failed: $($case.Name)" }
    }
    Write-Output "PASS $($cases.Count) memory-only feature-selection cases; missing files do not erase optional ownership."
    $gpuCases = @(
        @{Name='forced NVIDIA ignores AMD adapters';Override='Nvidia';Ids=@('PCI\VEN_1002&DEV_744C');Expected='nvidia'},
        @{Name='forced bridge ignores NVIDIA adapters';Override='Bridge';Ids=@('pci\ven_10de&dev_2684');Expected='bridge'},
        @{Name='NVIDIA MatchingDeviceId any case';Override='Auto';Ids=@('pci\ven_10de&dev_2684');Expected='nvidia'},
        @{Name='NVIDIA HardwareID beside an Intel iGPU';Override='Auto';Ids=@('PCI\VEN_8086&DEV_A780','PCI\VEN_10DE&DEV_2684&SUBSYS_00000000');Expected='nvidia'},
        @{Name='AMD and Intel only use the bridge';Override='Auto';Ids=@('PCI\VEN_1002&DEV_744C','PCI\VEN_8086&DEV_56A0');Expected='bridge'},
        @{Name='no adapters keep the NVIDIA path';Override='Auto';Ids=@();Expected='nvidia'},
        @{Name='only non-PCI adapters keep the NVIDIA path';Override='Auto';Ids=@('ROOT\BasicDisplay');Expected='nvidia'})
    foreach ($case in $gpuCases) {
        if ((Get-BbwGpuPath $case.Override $case.Ids) -ne $case.Expected) { throw "GPU path selection failed: $($case.Name)" }
    }
    $hybridCases = @(
        @{Name='NVIDIA beside an Intel iGPU';Ids=@('PCI\VEN_8086&DEV_A780','PCI\VEN_10DE&DEV_2684');Expected=$true},
        @{Name='NVIDIA beside an AMD iGPU';Ids=@('PCI\VEN_1002&dev_164e','PCI\VEN_10de&dev_2684');Expected=$true},
        @{Name='NVIDIA only';Ids=@('PCI\VEN_10de&dev_2684');Expected=$false},
        @{Name='AMD only';Ids=@('PCI\VEN_1002&dev_744c');Expected=$false},
        @{Name='no adapters';Ids=@();Expected=$false})
    foreach ($case in $hybridCases) {
        if ((Test-BbwHybridGpu $case.Ids) -ne $case.Expected) { throw "Hybrid GPU detection failed: $($case.Name)" }
    }
    if ((Get-BbwGpuPath) -notin @('nvidia','bridge')) { throw 'GPU path detection on this machine returned an unknown path.' }
    if (-not (Get-Command Get-BbwGpuPath -Module Setup.Core -ErrorAction SilentlyContinue)) { throw 'Get-BbwGpuPath is not exported for Setup.ps1.' }
    Write-Output "PASS $($gpuCases.Count) GPU path selection cases; this machine resolves to '$(Get-BbwGpuPath)'."
}
