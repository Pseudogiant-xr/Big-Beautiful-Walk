param([string]$GamePath, [ValidateSet('Install','Uninstall','Diagnose')][string]$Mode='Install', [switch]$Headless, [string]$DependencyDirectory, [string[]]$Features=@())
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'Setup.Core.psm1') -Force
if ($Headless) {
    try { Invoke-BbwSetup -GamePath $GamePath -Mode $Mode -DependencyDirectory $DependencyDirectory -Features $Features; exit 0 }
    catch { Write-Error $_; exit 1 }
}
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[Windows.Forms.Application]::EnableVisualStyles()
$hasFg=$false
try {
    $releaseLock=Get-Content -LiteralPath (Join-Path (Split-Path $PSScriptRoot -Parent) 'packaging/dependencies.lock.json') -Raw | ConvertFrom-Json
    $hasFg=@($releaseLock.Dependencies | Where-Object { $_.Feature -eq 'fg' }).Count -gt 0
} catch { }
$optionalHeight=0
if ($hasFg) { $optionalHeight=32 }
$form = New-Object Windows.Forms.Form
$form.Text = 'Big Beautiful Walk - Setup 0.7.0'
$form.ClientSize = New-Object Drawing.Size(690,(470+$optionalHeight))
$form.StartPosition = 'CenterScreen'
$form.MinimumSize = $form.Size
$label = New-Object Windows.Forms.Label
$label.Text = 'Select your Big Walk folder (the one containing Big Walk.exe):'
$label.SetBounds(20,18,640,24)
$pathBox = New-Object Windows.Forms.ComboBox
$pathBox.SetBounds(20,46,550,28)
$pathBox.Anchor = 'Top,Left,Right'
$pathBox.TabIndex=0
foreach ($found in @(Get-BbwSteamGames)) { [void]$pathBox.Items.Add($found) }
if ($GamePath) { $pathBox.Text=$GamePath } elseif ($pathBox.Items.Count) { $pathBox.SelectedIndex=0 }
$browse = New-Object Windows.Forms.Button
$browse.Text='Browse...'
$browse.SetBounds(580,45,90,28)
$browse.Anchor='Top,Right'
$browse.TabIndex=1
$browse.Add_Click({
    $dialog=New-Object Windows.Forms.FolderBrowserDialog
    $dialog.Description='Select the Big Walk game folder'
    $dialog.ShowNewFolderButton=$false
    if ($pathBox.Text) { $dialog.SelectedPath=$pathBox.Text }
    if ($dialog.ShowDialog() -eq 'OK') { $pathBox.Text=$dialog.SelectedPath }
    $dialog.Dispose()
})
$hint=New-Object Windows.Forms.Label
$hint.Text='Core: DLSS Super Resolution + Advanced Graphics. Close the game to install or remove. Install the x64 Visual C++ runtime first; see the installation guide. Existing settings and saves are preserved.'
$hint.SetBounds(20,84,650,38)
$status=New-Object Windows.Forms.TextBox
$status.Multiline=$true
$status.ReadOnly=$true
$status.TabIndex=8
$status.ScrollBars='Vertical'
$status.SetBounds(20,(170+$optionalHeight),650,270)
$status.Anchor='Top,Bottom,Left,Right'
$status.Text='Ready. Install/Repair installs core Super Resolution + Advanced Graphics and verifies dependencies before changing files. Optional Frame Generation and Neural Rendering require manual setup; open the FG / NR guide. Diagnose only reads files.'
$fgCheck=New-Object Windows.Forms.CheckBox
$fgCheck.Text='Install alternative upscalers and Frame Generation bridge (experimental)'
$fgCheck.SetBounds(20,122,650,28)
$fgCheck.Checked=$false
$fgCheck.TabIndex=2
if ($hasFg) {
    $form.Controls.Add($fgCheck)
    $status.Text='Ready. The optional bridge adds NVIDIA, AMD and Intel Frame Generation runtimes and Home controls; fresh defaults leave FG and NR off. Neural Rendering needs a separately obtained model. Install/Repair also repairs previously installed optional components when this box is off. Diagnose only reads files. Read the guides and licences before installing.'
}
$guide=New-Object Windows.Forms.Button
$guide.Text='FG / NR guide'
$guide.TabIndex=6
$guide.SetBounds(460,(125+$optionalHeight),100,32)
$guide.Add_Click({
    try { Start-Process -FilePath (Join-Path (Split-Path $PSScriptRoot -Parent) 'docs/FRAME-GENERATION.md') }
    catch { $status.Text='Open docs/FRAME-GENERATION.md from the extracted release folder with a text editor for FG / NR setup and restart instructions.' }
})
$form.Controls.Add($guide)
$licences=New-Object Windows.Forms.Button
$licences.Text='Licences'
$licences.TabIndex=7
$licences.SetBounds(568,(125+$optionalHeight),102,32)
$licences.Add_Click({
    try { Start-Process -FilePath (Join-Path (Split-Path $PSScriptRoot -Parent) 'THIRD_PARTY_NOTICES.md') }
    catch { $status.Text='Open THIRD_PARTY_NOTICES.md from the extracted release folder with a text editor. Full licence texts are in docs/licenses.' }
})
$form.Controls.Add($licences)
$buttons=@()
$script:setupWorker=$null
$script:setupOperation=$null
$timer=New-Object Windows.Forms.Timer
$timer.Interval=200
$timer.Add_Tick({
    if (-not $script:setupOperation -or -not $script:setupOperation.IsCompleted) { return }
    $timer.Stop()
    try {
        $result=$script:setupWorker.EndInvoke($script:setupOperation)
        if ($script:setupWorker.HadErrors) { throw [string]$script:setupWorker.Streams.Error[0] }
        $status.Text=($result | Out-String).Trim()
    } catch { $status.Text='Setup stopped safely.' + [Environment]::NewLine + $_.Exception.Message }
    finally {
        $script:setupWorker.Dispose(); $script:setupWorker=$null; $script:setupOperation=$null
        foreach ($control in $buttons) { $control.Enabled=$true }
        $browse.Enabled=$true; $pathBox.Enabled=$true; $fgCheck.Enabled=$true
    }
})
foreach ($spec in @(@('Install / Repair','Install',20,155),@('Uninstall','Uninstall',190,120),@('Diagnose','Diagnose',325,120))) {
    $button=New-Object Windows.Forms.Button
    $button.Text=$spec[0]; $button.Tag=$spec[1]
    $button.TabIndex=3+$buttons.Count
    $button.SetBounds([int]$spec[2],(125+$optionalHeight),[int]$spec[3],32)
    $button.Add_Click({
        param($sender,$eventArgs)
        foreach ($control in $buttons) { $control.Enabled=$false }
        $browse.Enabled=$false; $pathBox.Enabled=$false; $fgCheck.Enabled=$false
        $status.Text='Working - checking files and downloading verified dependencies if needed. Please leave Setup open; this may take a few minutes.'
        try {
            $selectedFeatures=@($Features | Where-Object { $_ -ne 'fg' })
            if ($hasFg -and $fgCheck.Checked) { $selectedFeatures+='fg' }
            $script:setupWorker=[PowerShell]::Create()
            [void]$script:setupWorker.AddScript({
                param($Module,$Game,$Action,$Dependencies,$SelectedFeatures)
                $ErrorActionPreference='Stop'
                Import-Module $Module -Force
                Invoke-BbwSetup -GamePath $Game -Mode $Action -DependencyDirectory $Dependencies -Features $SelectedFeatures
            }).AddArgument((Join-Path $PSScriptRoot 'Setup.Core.psm1')).AddArgument($pathBox.Text).AddArgument([string]$sender.Tag).AddArgument($DependencyDirectory).AddArgument($selectedFeatures)
            $script:setupOperation=$script:setupWorker.BeginInvoke()
            $timer.Start()
        } catch {
            $status.Text='Setup could not start.' + [Environment]::NewLine + $_.Exception.Message
            if ($script:setupWorker) { $script:setupWorker.Dispose(); $script:setupWorker=$null }
            foreach ($control in $buttons) { $control.Enabled=$true }
            $browse.Enabled=$true; $pathBox.Enabled=$true; $fgCheck.Enabled=$true
        }
    })
    $buttons += $button
    $form.Controls.Add($button)
}
$form.Controls.AddRange(@($label,$pathBox,$browse,$hint,$status))
$form.Add_FormClosing({
    param($sender,$eventArgs)
    if ($script:setupOperation -and -not $script:setupOperation.IsCompleted) {
        $eventArgs.Cancel=$true
        $status.AppendText([Environment]::NewLine + 'Setup is still working. Wait for its result before closing this window.')
    }
})
[void]$form.ShowDialog()
$timer.Dispose()
$form.Dispose()
