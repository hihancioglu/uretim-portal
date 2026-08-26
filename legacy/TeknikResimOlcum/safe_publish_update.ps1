$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$installerPath = Join-Path $scriptRoot 'install_shared_versioned_update.ps1'
$defaultPackage = Join-Path $scriptRoot 'TeknikResimOlcum_Update_LATEST.zip'

function Get-SuggestedSharedRoot {
    try {
        $localVersions = Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)) 'TeknikResimOlcum\Versions'
        if (-not (Test-Path -LiteralPath $localVersions -PathType Container)) { return '' }

        foreach ($marker in @(Get-ChildItem -LiteralPath $localVersions -Filter '_shared_root.txt' -Recurse -File -ErrorAction SilentlyContinue |
                Sort-Object LastWriteTimeUtc -Descending)) {
            try {
                $candidate = (Get-Content -LiteralPath $marker.FullName -Raw -ErrorAction Stop).Trim().Trim('"')
                if (-not [string]::IsNullOrWhiteSpace($candidate)) { return $candidate }
            }
            catch {
            }
        }
    }
    catch {
    }

    return ''
}

function Select-SharedRoot {
    param([string]$SuggestedPath)

    $form = New-Object System.Windows.Forms.Form
    $form.Text = 'Ortak TeknikResimOlcum Klasoru'
    $form.StartPosition = [System.Windows.Forms.FormStartPosition]::CenterScreen
    $form.Size = New-Object System.Drawing.Size(760, 205)
    $form.MinimumSize = New-Object System.Drawing.Size(640, 205)
    $form.MaximizeBox = $false
    $form.MinimizeBox = $false
    $form.Font = New-Object System.Drawing.Font('Segoe UI', 9)

    $label = New-Object System.Windows.Forms.Label
    $label.Text = "Dosya Gezgini'nde ortak klasoru acin, adres cubugundaki yolu kopyalayip asagiya yapistirin.`r`nOrnek: \\SUNUCU\Paylasim\TeknikResimOlcum"
    $label.Location = New-Object System.Drawing.Point(14, 12)
    $label.Size = New-Object System.Drawing.Size(710, 45)
    $label.Anchor = 'Top,Left,Right'

    $pathBox = New-Object System.Windows.Forms.TextBox
    $pathBox.Location = New-Object System.Drawing.Point(16, 63)
    $pathBox.Size = New-Object System.Drawing.Size(710, 25)
    $pathBox.Anchor = 'Top,Left,Right'
    $pathBox.Text = $SuggestedPath

    $pasteButton = New-Object System.Windows.Forms.Button
    $pasteButton.Text = 'Panodan Yapistir'
    $pasteButton.Location = New-Object System.Drawing.Point(16, 101)
    $pasteButton.Size = New-Object System.Drawing.Size(130, 32)
    $pasteButton.Add_Click({
        if ([System.Windows.Forms.Clipboard]::ContainsText()) {
            $pathBox.Text = [System.Windows.Forms.Clipboard]::GetText().Trim().Trim('"')
        }
    })

    $fileButton = New-Object System.Windows.Forms.Button
    $fileButton.Text = 'Ortak Klasorden Dosya Sec'
    $fileButton.Location = New-Object System.Drawing.Point(154, 101)
    $fileButton.Size = New-Object System.Drawing.Size(190, 32)
    $fileButton.Add_Click({
        $dialog = New-Object System.Windows.Forms.OpenFileDialog
        $dialog.Title = 'Ortak klasordeki CurrentVersion.txt veya TeknikResimOlcumBaslat.exe dosyasini secin'
        $dialog.Filter = 'Ortak klasor dosyalari|CurrentVersion.txt;TeknikResimOlcum.exe;TeknikResimOlcumBaslat.exe|Tum dosyalar (*.*)|*.*'
        $dialog.CheckFileExists = $true
        if ($dialog.ShowDialog($form) -eq [System.Windows.Forms.DialogResult]::OK) {
            $pathBox.Text = [IO.Path]::GetDirectoryName($dialog.FileName)
        }
        $dialog.Dispose()
    })

    $cancelButton = New-Object System.Windows.Forms.Button
    $cancelButton.Text = 'Iptal'
    $cancelButton.Location = New-Object System.Drawing.Point(548, 101)
    $cancelButton.Size = New-Object System.Drawing.Size(85, 32)
    $cancelButton.Anchor = 'Top,Right'
    $cancelButton.DialogResult = [System.Windows.Forms.DialogResult]::Cancel

    $okButton = New-Object System.Windows.Forms.Button
    $okButton.Text = 'Devam Et'
    $okButton.Location = New-Object System.Drawing.Point(641, 101)
    $okButton.Size = New-Object System.Drawing.Size(85, 32)
    $okButton.Anchor = 'Top,Right'
    $okButton.DialogResult = [System.Windows.Forms.DialogResult]::OK

    $form.Controls.AddRange(@($label, $pathBox, $pasteButton, $fileButton, $cancelButton, $okButton))
    $form.AcceptButton = $okButton
    $form.CancelButton = $cancelButton

    $result = $form.ShowDialog()
    $selectedPath = $pathBox.Text.Trim().Trim('"').TrimEnd('\')
    $form.Dispose()

    if ($result -ne [System.Windows.Forms.DialogResult]::OK) { return '' }
    return $selectedPath
}

try {
    if (-not (Test-Path -LiteralPath $installerPath -PathType Leaf)) {
        throw "Guvenli kurulum betigi bulunamadi: $installerPath"
    }

    $packagePath = $defaultPackage
    if (-not (Test-Path -LiteralPath $packagePath -PathType Leaf)) {
        $packageDialog = New-Object System.Windows.Forms.OpenFileDialog
        $packageDialog.Title = 'TeknikResimOlcum guncelleme paketini secin'
        $packageDialog.Filter = 'ZIP Guncelleme Paketi (*.zip)|*.zip'
        $packageDialog.InitialDirectory = $scriptRoot
        if ($packageDialog.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK) { exit 2 }
        $packagePath = $packageDialog.FileName
    }

    $sharedRoot = Select-SharedRoot -SuggestedPath (Get-SuggestedSharedRoot)
    if ([string]::IsNullOrWhiteSpace($sharedRoot)) { exit 2 }
    if (-not (Test-Path -LiteralPath $sharedRoot -PathType Container)) {
        throw "Ortak klasore erisilemedi. Dosya Gezgini adres cubugundaki tam yolu yapistirin:`r`n$sharedRoot"
    }

    & $installerPath -PackagePath $packagePath -SharedRoot $sharedRoot

    [System.Windows.Forms.MessageBox]::Show(
        "Yeni surum yayina alindi.`r`n`r`nIlk acilisi ortak klasordeki TeknikResimOlcumBaslat.exe ile yapin.",
        'Guvenli yayinlama tamamlandi',
        [System.Windows.Forms.MessageBoxButtons]::OK,
        [System.Windows.Forms.MessageBoxIcon]::Information) | Out-Null
}
catch {
    [System.Windows.Forms.MessageBox]::Show(
        $_.Exception.Message,
        'Guvenli yayinlama basarisiz',
        [System.Windows.Forms.MessageBoxButtons]::OK,
        [System.Windows.Forms.MessageBoxIcon]::Error) | Out-Null
    exit 1
}
