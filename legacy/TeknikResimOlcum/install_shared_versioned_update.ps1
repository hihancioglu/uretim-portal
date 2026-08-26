param(
    [Parameter(Mandatory = $false)]
    [string]$PackagePath = "",

    [Parameter(Mandatory = $false)]
    [string]$SharedRoot = ""
)

$ErrorActionPreference = 'Stop'

function Resolve-FullPath {
    param([string]$PathValue)
    return [System.IO.Path]::GetFullPath((Resolve-Path -LiteralPath $PathValue).Path)
}

function Get-ManifestValue {
    param(
        [string]$ManifestPath,
        [string]$Key
    )

    $prefix = "$Key="
    foreach ($line in Get-Content -LiteralPath $ManifestPath -Encoding UTF8) {
        $trimmed = $line.Trim()
        if ($trimmed.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
            return $trimmed.Substring($prefix.Length).Trim()
        }
    }

    return ''
}

function Copy-DirectoryContent {
    param(
        [string]$SourceRoot,
        [string]$DestinationRoot
    )

    New-Item -ItemType Directory -Force -Path $DestinationRoot | Out-Null
    foreach ($item in @(Get-ChildItem -LiteralPath $SourceRoot -Force)) {
        Copy-Item -LiteralPath $item.FullName -Destination $DestinationRoot -Recurse -Force
    }
}

function Test-SameFileContent {
    param(
        [string]$SourcePath,
        [string]$DestinationPath
    )

    if (-not (Test-Path -LiteralPath $SourcePath -PathType Leaf) -or
        -not (Test-Path -LiteralPath $DestinationPath -PathType Leaf)) {
        return $false
    }

    $source = Get-Item -LiteralPath $SourcePath
    $destination = Get-Item -LiteralPath $DestinationPath
    if ($source.Length -ne $destination.Length) { return $false }

    return (Get-FileHash -Algorithm SHA256 -LiteralPath $SourcePath).Hash -eq
           (Get-FileHash -Algorithm SHA256 -LiteralPath $DestinationPath).Hash
}

function Publish-LauncherAlias {
    param(
        [string]$SourcePath,
        [string]$DestinationPath
    )

    if (Test-SameFileContent -SourcePath $SourcePath -DestinationPath $DestinationPath) {
        return $true
    }

    for ($attempt = 1; $attempt -le 20; $attempt++) {
        try {
            Copy-Item -LiteralPath $SourcePath -Destination $DestinationPath -Force
            return $true
        }
        catch {
            if ($attempt -lt 20) { Start-Sleep -Milliseconds 300 }
        }
    }

    return $false
}

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

if ([string]::IsNullOrWhiteSpace($PackagePath)) {
    $PackagePath = Join-Path $scriptRoot 'UpdatePackages\TeknikResimOlcum_Update_LATEST.zip'
}

if ([string]::IsNullOrWhiteSpace($SharedRoot)) {
    $SharedRoot = $scriptRoot
}

if (-not (Test-Path -LiteralPath $PackagePath -PathType Leaf)) {
    throw "Guncelleme paketi bulunamadi: $PackagePath"
}

New-Item -ItemType Directory -Force -Path $SharedRoot | Out-Null
$PackagePath = Resolve-FullPath $PackagePath
$SharedRoot = (Resolve-Path -LiteralPath $SharedRoot).Path.TrimEnd('\')

$workRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('TeknikResimOlcum_SharedInstall_' + [guid]::NewGuid().ToString('N'))
$extractDir = Join-Path $workRoot 'payload'

try {
    New-Item -ItemType Directory -Force -Path $extractDir | Out-Null
    Expand-Archive -LiteralPath $PackagePath -DestinationPath $extractDir -Force

    $items = @(Get-ChildItem -LiteralPath $extractDir -Force)
    $payloadRoot = $extractDir
    if ($items.Count -eq 1 -and $items[0].PSIsContainer) {
        $payloadRoot = $items[0].FullName
    }

    $manifestPath = Join-Path $payloadRoot '_update_manifest.txt'
    $appExePath = Join-Path $payloadRoot 'TeknikResimOlcum.exe'
    $launcherPath = Join-Path (Join-Path $payloadRoot '_launcher') 'TeknikResimOlcum.exe'
    $legacyLauncherPath = Join-Path $payloadRoot 'TeknikResimOlcumBaslat.exe'

    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { throw 'Paket icinde _update_manifest.txt bulunamadi.' }
    if (-not (Test-Path -LiteralPath $appExePath -PathType Leaf)) { throw 'Paket icinde TeknikResimOlcum.exe bulunamadi.' }
    if (-not (Test-Path -LiteralPath $launcherPath -PathType Leaf)) {
        if (Test-Path -LiteralPath $legacyLauncherPath -PathType Leaf) {
            $launcherPath = $legacyLauncherPath
        }
        else {
            throw 'Paket icinde baslatici bulunamadi. _launcher\TeknikResimOlcum.exe bekleniyor.'
        }
    }

    $buildStamp = Get-ManifestValue -ManifestPath $manifestPath -Key 'BuildStamp'
    if ([string]::IsNullOrWhiteSpace($buildStamp)) { throw 'Paket BuildStamp bilgisi icermiyor.' }

    foreach ($folderName in @('Data', 'Drawings', 'Updates', 'Backups', 'Versions')) {
        New-Item -ItemType Directory -Force -Path (Join-Path $SharedRoot $folderName) | Out-Null
    }

    $versionDir = Join-Path (Join-Path $SharedRoot 'Versions') $buildStamp
    if (-not (Test-Path -LiteralPath $versionDir)) {
        Copy-DirectoryContent -SourceRoot $payloadRoot -DestinationRoot $versionDir
        Set-Content -LiteralPath (Join-Path $versionDir '_shared_root.txt') -Value $SharedRoot -Encoding UTF8
    }

    $legacyLauncher = Join-Path $SharedRoot 'TeknikResimOlcumBaslat.exe'
    $mainLauncher = Join-Path $SharedRoot 'TeknikResimOlcum.exe'

    # Once alternatif baslaticiyi yayinla. Ana EXE eski paylasimli program olarak
    # calisiyor olsa bile bu dosya kilitli olmayacagi icin gecis tamamlanabilir.
    $legacyLauncherUpdated = Publish-LauncherAlias -SourcePath $launcherPath -DestinationPath $legacyLauncher
    if (-not $legacyLauncherUpdated) {
        throw 'TeknikResimOlcumBaslat.exe kullanimda oldugu icin guncellenemedi. Birkaç saniye bekleyip Guvenli Yayinla aracini yeniden calistirin.'
    }

    # Yeni surumu once aktif et; alternatif baslatici artik bu surumu acabilir.
    Set-Content -LiteralPath (Join-Path $SharedRoot 'CurrentVersion.txt') -Value $buildStamp -Encoding UTF8

    # Ana ad en son denenir. Eski program bu dosyadan calisiyorsa kilitli kalabilir;
    # bu durum yeni surumun yayinlanmasini geri almamalidir.
    $mainLauncherUpdated = Publish-LauncherAlias -SourcePath $launcherPath -DestinationPath $mainLauncher

    if (-not $mainLauncherUpdated -and -not (Test-Path -LiteralPath $mainLauncher -PathType Leaf) -and
        -not $legacyLauncherUpdated -and -not (Test-Path -LiteralPath $legacyLauncher -PathType Leaf)) {
        throw 'Ortak klasorde kullanilabilir bir program baslaticisi olusturulamadi.'
    }

    if (-not $mainLauncherUpdated -or -not $legacyLauncherUpdated) {
        Write-Warning 'Ana program dosyasi kullanimda oldugu icin degistirilemedi. Yeni surum yayina alindi; gecis icin TeknikResimOlcumBaslat.exe kullanilmalidir.'
    }

    Write-Host ''
    Write-Host 'Kurulum tamamlandi.'
    Write-Host "Ortak kok      : $SharedRoot"
    Write-Host "Yayindaki surum: $buildStamp"
    Write-Host "Baslatici      : $(Join-Path $SharedRoot 'TeknikResimOlcum.exe')"
    Write-Host "Eski kisayol   : $(Join-Path $SharedRoot 'TeknikResimOlcumBaslat.exe')"
    Write-Host ''
    Write-Host 'Kullanicilar ortak klasordeki TeknikResimOlcum.exe dosyasindan acmalidir. Eski TeknikResimOlcumBaslat.exe kisayollari da calismaya devam eder.'
}
finally {
    try {
        if (Test-Path -LiteralPath $workRoot) {
            Remove-Item -LiteralPath $workRoot -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
    catch {
    }
}
