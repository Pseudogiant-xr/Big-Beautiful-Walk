param([string]$GamePath, [ValidateSet('Install','Uninstall','Diagnose')][string]$Mode='Install', [switch]$Headless, [string]$DependencyDirectory, [string[]]$Features=@(), [ValidateSet('Auto','Nvidia','Bridge')][string]$GpuPath='Auto', [string]$NeuralModelPath)
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'Setup.Core.psm1') -Force
if ($Headless) {
    try { Invoke-BbwSetup -GamePath $GamePath -Mode $Mode -DependencyDirectory $DependencyDirectory -Features $Features -GpuPath $GpuPath -NeuralModelPath $NeuralModelPath; exit 0 }
    catch { Write-Error $_; exit 1 }
}
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[Windows.Forms.Application]::EnableVisualStyles()
$gpu=Get-BbwGpuPath $GpuPath
$hasFg=$false
$hasNr=$false
try {
    $releaseLock=Get-Content -LiteralPath (Join-Path (Split-Path $PSScriptRoot -Parent) 'packaging/dependencies.lock.json') -Raw | ConvertFrom-Json
    $hasFg=@($releaseLock.Dependencies | Where-Object { $_.Feature -eq 'fg' }).Count -gt 0
    $hasNr=@($releaseLock.Dependencies | Where-Object { $_.Feature -eq 'nr' }).Count -gt 0
} catch { }
$optionalHeight=0
if ($hasFg) { $optionalHeight=32 }
if ($hasNr) { $optionalHeight+=64 }
$form = New-Object Windows.Forms.Form
$form.Text = 'Big Beautiful Walk - Setup 0.8.1'
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
$status.TabIndex=10
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
$nrCheck=New-Object Windows.Forms.CheckBox
$nrCheck.Text='Install community RHI SF-v2 Neural Rendering model (~111 MiB download; includes bridge)'
$nrCheck.SetBounds(20,(122+$(if ($hasFg) {32} else {0})),650,28)
$nrCheck.Checked=$false
$nrCheck.TabIndex=3
if ($hasNr) {
    $form.Controls.Add($nrCheck)
    $status.Text='Ready. Optional bridge and community RHI SF-v2 NR model downloads are separate choices. The unsigned model is distributed by RHI; read the guides and NVIDIA terms. Selecting NR includes the bridge. Fresh defaults leave FG and NR off. Install/Repair retains previously installed optional components. Diagnose only reads files.'
}
$script:neuralModelSelection=$NeuralModelPath
$modelBrowse=New-Object Windows.Forms.Button
$modelBrowse.Text='Use my NR 310.9.16 compatibility DLL...'
$modelBrowse.SetBounds(20,(154+$(if ($hasFg) {32} else {0})),650,28)
$modelBrowse.TabIndex=4
$modelBrowse.Add_Click({
    $dialog=New-Object Windows.Forms.OpenFileDialog
    $dialog.Title='Select NR 310.9.16 compatibility DLL (verified by SHA-256)'
    $dialog.Filter='Neural Rendering DLL (nvngx_dlssnr.dll)|nvngx_dlssnr.dll'
    $dialog.RestoreDirectory=$true
    if ($dialog.ShowDialog() -eq 'OK') {
        $script:neuralModelSelection=$dialog.FileName
        $nrCheck.Text='Install my NR 310.9.16 compatibility model (includes bridge)'
        $nrCheck.Checked=$true
        $status.Text='Local NR model selected. Install / Repair verifies its pinned SHA-256 before changing game files. NR stays disabled on a fresh install; existing settings are preserved.'
    }
    $dialog.Dispose()
})
if ($hasNr) { $form.Controls.Add($modelBrowse) }
if ($NeuralModelPath) { $nrCheck.Checked=$true; $nrCheck.Text='Install my NR 310.9.16 compatibility model (includes bridge)' }
if ($gpu -eq 'bridge') {
    # AMD/Intel: the bridge is how the plugin renders; NR needs an NVIDIA RTX GPU.
    $hint.Text='Core on AMD/Intel GPUs: FSR and other upscalers through the OptiScaler bridge + Advanced Graphics. Close the game to install or remove. Install the x64 Visual C++ runtime first; see the installation guide. Existing settings and saves are preserved.'
    $fgCheck.Checked=$true; $fgCheck.Enabled=$false; $fgCheck.Text+=' (required for AMD/Intel GPUs)'
    $nrCheck.Checked=$false; $nrCheck.Enabled=$false; $nrCheck.Text+=' (NVIDIA RTX only)'
    $modelBrowse.Enabled=$false
    $status.Text='Ready. AMD/Intel GPU path: Install/Repair installs the bridge plugin with the OptiScaler upscaler bridge (FSR by default) instead of DLSS, and skips NVIDIA-only files. Fresh defaults leave Frame Generation off. Neural Rendering needs an NVIDIA RTX GPU. Diagnose only reads files. Read the guides and licences before installing.'
}
if ($gpu -eq 'nvidia' -and $GpuPath -eq 'Auto' -and (Test-BbwHybridGpu)) {
    # Windows may run the game on the AMD/Intel GPU, which needs the bridge. Recommend it; don't force it.
    $fgCheck.Checked=$true; $fgCheck.Text+=' (recommended: this PC also has an AMD/Intel GPU)'
}
$guide=New-Object Windows.Forms.Button
$guide.Text='FG / NR guide'
$guide.TabIndex=8
$guide.SetBounds(460,(125+$optionalHeight),100,32)
$guide.Add_Click({
    try { Start-Process -FilePath (Join-Path (Split-Path $PSScriptRoot -Parent) 'docs/FRAME-GENERATION.md') }
    catch { $status.Text='Open docs/FRAME-GENERATION.md from the extracted release folder with a text editor for FG / NR setup and restart instructions.' }
})
$form.Controls.Add($guide)
$licences=New-Object Windows.Forms.Button
$licences.Text='Licences'
$licences.TabIndex=9
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
        $browse.Enabled=$true; $pathBox.Enabled=$true; $fgCheck.Enabled=($gpu -ne 'bridge'); $nrCheck.Enabled=($gpu -ne 'bridge'); $modelBrowse.Enabled=($gpu -ne 'bridge')
    }
})
foreach ($spec in @(@('Install / Repair','Install',20,155),@('Uninstall','Uninstall',190,120),@('Diagnose','Diagnose',325,120))) {
    $button=New-Object Windows.Forms.Button
    $button.Text=$spec[0]; $button.Tag=$spec[1]
    $button.TabIndex=5+$buttons.Count
    $button.SetBounds([int]$spec[2],(125+$optionalHeight),[int]$spec[3],32)
    $button.Add_Click({
        param($sender,$eventArgs)
        foreach ($control in $buttons) { $control.Enabled=$false }
        $browse.Enabled=$false; $pathBox.Enabled=$false; $fgCheck.Enabled=$false; $nrCheck.Enabled=$false; $modelBrowse.Enabled=$false
        $status.Text='Working - checking files and downloading verified dependencies if needed. Please leave Setup open; this may take a few minutes.'
        try {
            $selectedFeatures=@($Features | Where-Object { $_ -notin @('fg','nr') })
            if ($hasFg -and $fgCheck.Checked) { $selectedFeatures+='fg' }
            if ($hasNr -and $nrCheck.Checked) { $selectedFeatures+='nr' }
            $selectedModel=$null
            if ($nrCheck.Checked) { $selectedModel=$script:neuralModelSelection }
            $script:setupWorker=[PowerShell]::Create()
            [void]$script:setupWorker.AddScript({
                param($Module,$Game,$Action,$Dependencies,$SelectedFeatures,$Gpu,$Model)
                $ErrorActionPreference='Stop'
                # A background runspace does not inherit the GUI's loaded modules.
                # Use this engine's built-ins even when module autoload is unavailable.
                foreach ($name in @('Microsoft.PowerShell.Management','Microsoft.PowerShell.Utility')) {
                    Import-Module ([IO.Path]::Combine($PSHOME,'Modules',$name,($name+'.psd1'))) -ErrorAction Stop
                }
                Import-Module $Module -Force
                Invoke-BbwSetup -GamePath $Game -Mode $Action -DependencyDirectory $Dependencies -Features $SelectedFeatures -GpuPath $Gpu -NeuralModelPath $Model
            }).AddArgument((Join-Path $PSScriptRoot 'Setup.Core.psm1')).AddArgument($pathBox.Text).AddArgument([string]$sender.Tag).AddArgument($DependencyDirectory).AddArgument($selectedFeatures).AddArgument($GpuPath).AddArgument($selectedModel)
            $script:setupOperation=$script:setupWorker.BeginInvoke()
            $timer.Start()
        } catch {
            $status.Text='Setup could not start.' + [Environment]::NewLine + $_.Exception.Message
            if ($script:setupWorker) { $script:setupWorker.Dispose(); $script:setupWorker=$null }
            foreach ($control in $buttons) { $control.Enabled=$true }
            $browse.Enabled=$true; $pathBox.Enabled=$true; $fgCheck.Enabled=($gpu -ne 'bridge'); $nrCheck.Enabled=($gpu -ne 'bridge'); $modelBrowse.Enabled=($gpu -ne 'bridge')
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
