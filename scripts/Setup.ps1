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
if ($hasFg) { $optionalHeight=72 }
if ($hasNr) { $optionalHeight+=104 }
$form = New-Object Windows.Forms.Form
$form.Text = 'Big Beautiful Walk - Setup 0.8.2'
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
$hint.Text='Core install smooths jagged edges and adds graphics controls. Close the game first. Needs the x64 Visual C++ runtime; see the guide. Your settings and saves are kept.'
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
$fgCheck.Text='Frame Generation and extra upscalers (optional bridge)'
$fgCheck.SetBounds(20,122,650,28)
$fgCheck.Checked=$false
$fgCheck.TabIndex=2
if ($hasFg) {
    $form.Controls.Add($fgCheck)
    $status.Text='Ready. The optional bridge adds NVIDIA, AMD and Intel Frame Generation runtimes and Home controls; fresh defaults leave FG and NR off. Neural Rendering needs a separately obtained model. Install/Repair also repairs previously installed optional components when this box is off. Diagnose only reads files. Read the guides and licences before installing.'
}
$nrCheck=New-Object Windows.Forms.CheckBox
$nrCheck.Text='Download AI lighting model (community RHI SF-v2; ~111 MiB; includes bridge)'
$nrCheck.SetBounds(20,(122+$(if ($hasFg) {72} else {0})),650,28)
$nrCheck.Checked=$false
$nrCheck.TabIndex=3
if ($hasNr) {
    $form.Controls.Add($nrCheck)
    $status.Text='Ready. Choose your game folder and any extras, then Install / Repair. Turn Frame Generation or AI lighting on in the game with Home when you want them. The RHI model is an unsigned community download; see Licences. Repairs keep previously installed extras. Diagnose checks files without changing them.'
}
$script:neuralModelSelection=$NeuralModelPath
$modelBrowse=New-Object Windows.Forms.Button
$modelBrowse.Text='Use my NR 310.9.16 compatibility DLL...'
$modelBrowse.SetBounds(20,(190+$(if ($hasFg) {72} else {0})),650,28)
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
# Keep the same earth-and-ochre palette as the in-game panel.
$form.SuspendLayout()
$form.MinimumSize=New-Object Drawing.Size(0,0)
$form.AutoScaleMode='None'
$form.FormBorderStyle='FixedDialog'
$form.MaximizeBox=$false
$background=[Drawing.Color]::FromArgb(23,28,23)
$surface=[Drawing.Color]::FromArgb(36,43,36)
$raised=[Drawing.Color]::FromArgb(51,59,48)
$ochre=[Drawing.Color]::FromArgb(214,171,99)
$ink=[Drawing.Color]::FromArgb(245,235,219)
$muted=[Drawing.Color]::FromArgb(196,186,168)
$form.BackColor=$background
$form.ForeColor=$ink
$form.Font=New-Object Drawing.Font('Segoe UI',10)
$form.ClientSize=New-Object Drawing.Size(960,800)
foreach ($control in $form.Controls) {
    $control.Anchor='Top,Left'
    $control.Top+=136
    $control.Left+=135
    $control.ForeColor=$ink
    if ($control -is [Windows.Forms.Button]) {
        $control.FlatStyle='Flat'
        $control.BackColor=$raised
        $control.FlatAppearance.BorderColor=[Drawing.Color]::FromArgb(102,94,75)
        $control.FlatAppearance.MouseOverBackColor=[Drawing.Color]::FromArgb(71,61,43)
    } elseif ($control -is [Windows.Forms.ComboBox]) {
        $control.FlatStyle='Flat'; $control.BackColor=$surface
    } elseif ($control -is [Windows.Forms.TextBox]) {
        $control.BackColor=$surface; $control.BorderStyle='FixedSingle'
    }
}
$status.Height=220
$hint.Font=New-Object Drawing.Font('Segoe UI',9)
$hint.ForeColor=$muted
foreach ($spec in @(@($hasFg,$fgCheck,'Adds in-between frames for smoother motion, plus other upscaling options. Install the files here; turn features on in Home after restarting.'),
                     @($hasNr,$nrCheck,'AI changes scene lighting and surfaces, with a possible frame-rate cost. For the new model, use the optional NR pack and choose its DLL below.'))) {
    if (-not $spec[0]) { continue }
    $help=New-Object Windows.Forms.Label
    $help.Text=[string]$spec[2]
    $help.SetBounds(175,($spec[1].Top+29),630,36)
    $help.ForeColor=$muted
    $help.Font=New-Object Drawing.Font('Segoe UI',9)
    $form.Controls.Add($help)
}
$heading=New-Object Windows.Forms.Label
$heading.Text='Big Beautiful Walk'
$heading.SetBounds(155,88,650,32)
$heading.Font=New-Object Drawing.Font('Segoe UI Semibold',20)
$heading.ForeColor=$ink
$subheading=New-Object Windows.Forms.Label
$subheading.Text='Picture quality, smoother motion and optional AI lighting.'
$subheading.SetBounds(157,123,650,23)
$subheading.ForeColor=$ochre
$subheading.Font=New-Object Drawing.Font('Segoe UI',9)
$form.Controls.AddRange(@($heading,$subheading))
$headerImage=$null
$artworkPath=Join-Path (Split-Path $PSScriptRoot -Parent) 'packaging/ui-landscape.png'
try {
    if ([IO.File]::Exists($artworkPath)) {
        $headerImage=[Drawing.Image]::FromFile($artworkPath)
        $illustration=New-Object Windows.Forms.PictureBox
        $illustration.SetBounds(485,88,320,60)
        $illustration.SizeMode='Zoom'
        $illustration.Image=$headerImage
        $illustration.BackColor=$background
        $form.Controls.Add($illustration)
    }
} catch { $headerImage=$null }
$frameImage=$null
$borderPath=Join-Path (Split-Path $PSScriptRoot -Parent) 'packaging/ui-border.png'
try {
    if ([IO.File]::Exists($borderPath)) { $frameImage=[Drawing.Image]::FromFile($borderPath) }
} catch { $frameImage=$null }
$form.Add_Paint({
    param($sender,$paint)
    if (-not $frameImage) { return }
    $scale=[Math]::Min($form.ClientSize.Width/[double]$frameImage.Width,$form.ClientSize.Height/[double]$frameImage.Height)
    $width=$frameImage.Width*$scale; $height=$frameImage.Height*$scale
    $destination=New-Object Drawing.RectangleF((($form.ClientSize.Width-$width)/2),(($form.ClientSize.Height-$height)/2),$width,$height)
    $paint.Graphics.DrawImage($frameImage,$destination)
})
$form.Add_Resize({ $form.Invalidate() })
$buttons[0].BackColor=$ochre
$buttons[0].ForeColor=$background
$buttons[0].FlatAppearance.MouseOverBackColor=[Drawing.Color]::FromArgb(232,191,121)
$tooltips=New-Object Windows.Forms.ToolTip
$tooltips.AutoPopDelay=12000
$tooltips.SetToolTip($buttons[0],'Installs or repairs the mod, checks downloaded files, and keeps your saved settings.')
$tooltips.SetToolTip($buttons[1],'Removes files installed by this mod and restores its original backups. Your saves and settings are kept.')
$tooltips.SetToolTip($buttons[2],'Checks the installed files and reports missing or changed files. Nothing is changed.')
$tooltips.SetToolTip($modelBrowse,'Select nvngx_dlssnr.dll from the extracted optional NR 310.9.16 pack. Setup checks that it is the supported file.')
$tooltips.SetToolTip($pathBox,'Choose the Steam folder containing Big Walk.exe. Browse if it is not listed.')
$workingHeight=[Windows.Forms.Screen]::FromControl($form).WorkingArea.Height
$workingWidth=[Windows.Forms.Screen]::FromControl($form).WorkingArea.Width
$fit=[Math]::Min(1.0,[Math]::Min(($workingHeight-24)/$form.Height,($workingWidth-24)/$form.Width))
if ($fit -lt 1) {
    foreach ($control in $form.Controls) {
        $control.SetBounds([int][Math]::Round($control.Left*$fit),[int][Math]::Round($control.Top*$fit),[int][Math]::Round($control.Width*$fit),[int][Math]::Round($control.Height*$fit))
        $control.Font=New-Object Drawing.Font($control.Font.FontFamily,([single]($control.Font.Size*$fit)),$control.Font.Style)
    }
    $form.ClientSize=New-Object Drawing.Size(([int][Math]::Round(960*$fit)),([int][Math]::Round(800*$fit)))
}
$form.MinimumSize=$form.Size
$form.ResumeLayout($true)
[void]$form.ShowDialog()
$tooltips.Dispose()
$timer.Dispose()
$form.Dispose()
if ($headerImage) { $headerImage.Dispose() }
if ($frameImage) { $frameImage.Dispose() }
