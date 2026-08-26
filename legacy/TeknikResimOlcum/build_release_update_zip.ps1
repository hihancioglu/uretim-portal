$ErrorActionPreference = 'Stop'

$appName = 'TeknikResimOlcum'
$projectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectFile = Join-Path $projectDir 'TeknikResimOlcum.vbproj'
$launcherProjectFile = Join-Path (Split-Path -Parent $projectDir) 'TeknikResimOlcumLauncher\TeknikResimOlcumLauncher.vbproj'
$privateKeyPath = Join-Path $projectDir 'UpdateSigningPrivateKey.xml'
$config = 'Release'
$runtime = 'win-x64'
$framework = 'net8.0-windows'
$publishDir = Join-Path $projectDir "bin\$config\$framework\$runtime\publish"
$launcherPublishDir = Join-Path (Split-Path -Parent $projectDir) "TeknikResimOlcumLauncher\bin\$config\$framework\$runtime\publish"
$packageDir = Join-Path $projectDir 'UpdatePackages'
$stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$versionFile = Join-Path $projectDir 'VERSION.txt'
$version = $stamp

if (Test-Path -LiteralPath $versionFile) {
    $rawVersion = (Get-Content -LiteralPath $versionFile -TotalCount 1).Trim()
    if (-not [string]::IsNullOrWhiteSpace($rawVersion)) {
        $version = ($rawVersion -replace '\s+', '_')
    }
}

Write-Host ''
Write-Host '============================================================'
Write-Host " $appName Release Build + Guncelleme Paketi"
Write-Host '============================================================'
Write-Host " Proje    : $projectFile"
Write-Host " Baslatici: $launcherProjectFile"
Write-Host " Konfig   : $config"
Write-Host " Runtime  : $runtime"
Write-Host " Versiyon : $version"
Write-Host ''

if (-not (Test-Path -LiteralPath $projectFile)) {
    throw "Proje dosyasi bulunamadi: $projectFile"
}

if (-not (Test-Path -LiteralPath $launcherProjectFile)) {
    throw "Baslatici proje dosyasi bulunamadi: $launcherProjectFile"
}

if (-not (Test-Path -LiteralPath $privateKeyPath)) {
    throw "Guncelleme imza anahtari bulunamadi: $privateKeyPath. UpdateSigningPrivateKey.xml dosyasini guvenli sekilde saklayin ve yayin paketlerini bu anahtarla imzalayin."
}

$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if ($null -eq $dotnet) {
    throw 'dotnet komutu bulunamadi. .NET 8 SDK kurulu olmalidir.'
}

Write-Host '[1/4] Release publish hazirlaniyor...'
& dotnet publish $projectFile -c $config -r $runtime --self-contained false --no-restore /p:PublishSingleFile=true
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish basarisiz oldu. ExitCode=$LASTEXITCODE"
}

& dotnet publish $launcherProjectFile -c $config -r $runtime --self-contained false --no-restore /p:PublishSingleFile=true
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish baslatici icin basarisiz oldu. ExitCode=$LASTEXITCODE"
}

$exePath = Join-Path $publishDir "$appName.exe"
if (-not (Test-Path -LiteralPath $exePath)) {
    throw "Publish klasorunde $appName.exe bulunamadi: $publishDir"
}

$pdfiumPath = Join-Path $publishDir 'pdfium.dll'
if (-not (Test-Path -LiteralPath $pdfiumPath)) {
    throw "PDF goruntuleme motoru publish klasorunde bulunamadi: $pdfiumPath"
}

$requiredPublishFiles = @(
    'WebView2Loader.dll',
    'Resources\app_icon.png',
    'Resources\INO\INO_Database.seed.csv',
    'Sql\Schema.sql'
)
foreach ($relativeFile in $requiredPublishFiles) {
    $requiredPath = Join-Path $publishDir $relativeFile
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw "Zorunlu publish dosyasi bulunamadi: $requiredPath"
    }
}

$launcherExePath = Join-Path $launcherPublishDir 'TeknikResimOlcum.exe'
if (-not (Test-Path -LiteralPath $launcherExePath)) {
    throw "Baslatici publish klasorunde TeknikResimOlcum.exe bulunamadi: $launcherPublishDir"
}

New-Item -ItemType Directory -Force -Path $packageDir | Out-Null

$stagingDir = Join-Path $packageDir ("_staging_{0}" -f $stamp)
$manifestPath = Join-Path $stagingDir '_update_manifest.txt'
$signaturePath = Join-Path $stagingDir '_update_signature.txt'
$zipPath = Join-Path $packageDir ("{0}_Update_{1}_{2}.zip" -f $appName, $version, $stamp)
$latestZipPath = Join-Path $packageDir ("{0}_Update_LATEST.zip" -f $appName)
$hashPath = "$zipPath.sha256.txt"
$sharedInstallScript = Join-Path $projectDir 'install_shared_versioned_update.ps1'
$sharedInstallScriptOut = Join-Path $packageDir 'install_shared_versioned_update.ps1'
$safePublishScript = Join-Path $projectDir 'safe_publish_update.ps1'
$safePublishScriptOut = Join-Path $packageDir 'safe_publish_update.ps1'
$safePublishBatch = Join-Path $projectDir 'TeknikResimOlcum_GuvenliYayinla.bat'
$safePublishBatchOut = Join-Path $packageDir 'TeknikResimOlcum_GuvenliYayinla.bat'

Write-Host '[2/4] Guncelleme staging klasoru ve manifest dosyasi olusturuluyor...'
Remove-Item -LiteralPath $stagingDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $stagingDir | Out-Null

# Publish klasorunu dogrudan ZIP'lemek bazen WebView2Loader.dll gibi dosyalarda
# PowerShell Compress-Archive erisim/yol hatasi uretir. Bu nedenle once staging klasorune kopyalanir.
$robocopyLog = Join-Path $packageDir ("robocopy_{0}.log" -f $stamp)
& robocopy $publishDir $stagingDir /MIR /R:3 /W:1 /NFL /NDL /NP /LOG:$robocopyLog | Out-Null
if ($LASTEXITCODE -ge 8) {
    throw "Publish dosyalari staging klasorune kopyalanamadi. RobocopyExitCode=$LASTEXITCODE Log=$robocopyLog"
}

if (-not (Test-Path -LiteralPath (Join-Path $stagingDir 'pdfium.dll'))) {
    throw 'PDF goruntuleme motoru guncelleme staging klasorune kopyalanamadi: pdfium.dll'
}

$launcherStagingDir = Join-Path $stagingDir '_launcher'
New-Item -ItemType Directory -Force -Path $launcherStagingDir | Out-Null
Copy-Item -LiteralPath $launcherExePath -Destination (Join-Path $launcherStagingDir 'TeknikResimOlcum.exe') -Force

# Gecis uyumlulugu: Eski guncelleme sihirbazi baslaticiyi paket kokunde
# TeknikResimOlcumBaslat.exe olarak arar. Yeni kullanimda ortak kokte
# TeknikResimOlcum.exe gorunecek, fakat eski sihirbazlar icin ayni
# baslatici bu adla da pakete eklenir.
Copy-Item -LiteralPath $launcherExePath -Destination (Join-Path $stagingDir 'TeknikResimOlcumBaslat.exe') -Force

# Eski TROP1 teknik resimlerin yeni, kurulum-bazli TROP2 anahtarina tek seferlik
# gecisi icin gereken anahtar yalnizca guncelleme paketine eklenir. Ana uygulama
# EXE'sinde yer almaz ve tum eski cizimler donusturulunce program tarafindan silinir.
$legacyMigrationKeyPath = Join-Path $stagingDir '_legacy_drawing_migration.key'
$sha256 = [System.Security.Cryptography.SHA256]::Create()
try {
    $legacyEncKey = $sha256.ComputeHash([System.Text.Encoding]::UTF8.GetBytes('TeknikResimOlcum-AES-Key-Change-This-In-Company-2026'))
    $legacyMacKey = $sha256.ComputeHash([System.Text.Encoding]::UTF8.GetBytes('TeknikResimOlcum-HMAC-Key-Change-This-In-Company-2026'))
}
finally {
    $sha256.Dispose()
}

$legacyMigrationKey = @(
    'PackageType=TeknikResimOlcumLegacyDrawingMigrationKey'
    'Version=1'
    ('EncryptionKeyBase64=' + [Convert]::ToBase64String($legacyEncKey))
    ('MacKeyBase64=' + [Convert]::ToBase64String($legacyMacKey))
)
Set-Content -LiteralPath $legacyMigrationKeyPath -Value $legacyMigrationKey -Encoding ASCII
[Array]::Clear($legacyEncKey, 0, $legacyEncKey.Length)
[Array]::Clear($legacyMacKey, 0, $legacyMacKey.Length)

$baseFullPath = (Resolve-Path -LiteralPath $stagingDir).Path.TrimEnd('\') + '\'
$fileManifestLines = Get-ChildItem -LiteralPath $stagingDir -Recurse -File |
    Where-Object { $_.Name -ne '_update_manifest.txt' -and $_.Name -ne '_update_signature.txt' } |
    ForEach-Object {
        $relativePath = $_.FullName.Substring($baseFullPath.Length).Replace('\', '/')
        $hash = Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName
        "{0}|{1}|{2}" -f $relativePath, $_.Length, $hash.Hash.ToUpperInvariant()
    } |
    Sort-Object

$manifest = @(
    'PackageType=TeknikResimOlcumUpdate'
    "AppName=$appName"
    "Version=$version"
    "BuildStamp=$stamp"
    "Runtime=$runtime"
    "Framework=$framework"
    'PublishSingleFile=true'
    ('BuiltAt=' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
    'SignatureAlgorithm=RSA-SHA256'
    'HashAlgorithm=SHA256'
    '[Files]'
)
$manifest += $fileManifestLines
Set-Content -LiteralPath $manifestPath -Value $manifest -Encoding UTF8

$privateKeyXml = Get-Content -LiteralPath $privateKeyPath -Raw
$rsa = New-Object System.Security.Cryptography.RSACryptoServiceProvider
$rsa.PersistKeyInCsp = $false
$rsa.FromXmlString($privateKeyXml)
$manifestBytes = [System.IO.File]::ReadAllBytes($manifestPath)
$signatureBytes = $rsa.SignData($manifestBytes, [System.Security.Cryptography.CryptoConfig]::MapNameToOID('SHA256'))
$rsa.Clear()

$signatureText = @(
    'Algorithm=RSA-SHA256'
    ('SignatureBase64=' + [Convert]::ToBase64String($signatureBytes))
)
Set-Content -LiteralPath $signaturePath -Value $signatureText -Encoding ASCII

Remove-Item -LiteralPath $zipPath -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $latestZipPath -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $hashPath -Force -ErrorAction SilentlyContinue

Write-Host '[3/4] ZIP guncelleme paketi hazirlaniyor...'
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($stagingDir, $zipPath, [System.IO.Compression.CompressionLevel]::Optimal, $false)
Copy-Item -LiteralPath $zipPath -Destination $latestZipPath -Force
if (Test-Path -LiteralPath $sharedInstallScript) {
    Copy-Item -LiteralPath $sharedInstallScript -Destination $sharedInstallScriptOut -Force
}
if (Test-Path -LiteralPath $safePublishScript) {
    Copy-Item -LiteralPath $safePublishScript -Destination $safePublishScriptOut -Force
}
if (Test-Path -LiteralPath $safePublishBatch) {
    Copy-Item -LiteralPath $safePublishBatch -Destination $safePublishBatchOut -Force
}
Remove-Item -LiteralPath $stagingDir -Recurse -Force -ErrorAction SilentlyContinue

Write-Host '[4/4] SHA256 ozeti olusturuluyor...'
$hash = Get-FileHash -Algorithm SHA256 -LiteralPath $zipPath
($hash.Hash + '  ' + [System.IO.Path]::GetFileName($zipPath)) | Set-Content -LiteralPath $hashPath -Encoding UTF8

Write-Host ''
Write-Host '============================================================'
Write-Host ' ISLEM TAMAMLANDI'
Write-Host '============================================================'
Write-Host " Publish klasoru : $publishDir"
Write-Host " Staging klasoru : $stagingDir"
Write-Host " Guncelleme ZIP  : $zipPath"
Write-Host " Son paket       : $latestZipPath"
Write-Host " SHA256          : $hashPath"
Write-Host ''
Write-Host 'Bu ZIP dosyasi program icindeki Program Guncelleme Sihirbazi ile uygulanmalidir.'
Write-Host 'Kaynak kod ZIP dosyasi degil, bu scriptin olusturdugu Update ZIP dosyasi secilmelidir.'
Write-Host ''
