$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0
Import-Module (Join-Path $PSScriptRoot '../scripts/Setup.Core.psm1') -Force
# Exercise production selection without installing files or deleting a DLL.
& (Get-Module Setup.Core) {
    $dependencies = @(
        [pscustomobject]@{Id='bepinex';Feature='core'},
        [pscustomobject]@{Id='optiscaler_fg';Feature='fg'},
        [pscustomobject]@{Id='xess';Feature='fg'})
    $cases = @(
        @{Name='fresh core stays core';Requested=@();Roles=@();Expected=''},
        @{Name='explicit bridge opt-in';Requested=@('fg');Roles=@();Expected='fg'},
        @{Name='owned bridge repaired with unchecked box';Requested=@();Roles=@('optiscaler_fg');Expected='fg'},
        @{Name='missing owned XeSS still selects its pack';Requested=@();Roles=@('xess');Expected='fg'},
        @{Name='core ownership does not opt in';Requested=@();Roles=@('bepinex');Expected=''},
        @{Name='unknown roles do not opt in';Requested=@();Roles=@('unrelated','config');Expected=''},
        @{Name='repeated roles and requests deduplicate';Requested=@('fg','FG');Roles=@('optiscaler_fg','xess');Expected='fg'},
        @{Name='unknown explicit request retained for validation';Requested=@('nr');Roles=@();Expected='nr'})
    foreach ($case in $cases) {
        $actual = @(Get-BbwRepairFeatures $dependencies $case.Requested $case.Roles)
        if (($actual -join ',') -ne $case.Expected) { throw "Feature selection failed: $($case.Name)" }
    }
    Write-Output "PASS $($cases.Count) memory-only feature-selection cases; missing files do not erase optional ownership."
}
