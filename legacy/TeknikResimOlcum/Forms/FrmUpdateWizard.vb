Imports System.Diagnostics
Imports System.Drawing
Imports System.IO
Imports System.IO.Compression
Imports System.Linq
Imports System.Security.Cryptography
Imports System.Text
Imports System.Windows.Forms

Public Class FrmUpdateWizard
    Inherits Form

    Private ReadOnly txtPackage As New TextBox()
    Private ReadOnly txtAppDir As New TextBox()
    Private ReadOnly txtBackupDir As New TextBox()
    Private ReadOnly txtInfo As New TextBox()
    Private lastLauncherPublishWarning As String = ""

    Public Sub New(Optional initialPackagePath As String = "")
        AuthorizationService.Require(AppState.CanOpenUserAdmin, "Program Guncelleme Sihirbazi")
        AppIconService.Apply(Me)

        Text = "Program Güncelleme Sihirbazı"
        StartPosition = FormStartPosition.CenterParent
        Size = New Size(860, 580)
        MinimumSize = New Size(700, 500)
        BackColor = Color.White

        Dim main As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 4,
            .Padding = New Padding(12),
            .BackColor = Color.White
        }
        main.RowStyles.Add(New RowStyle(SizeType.Absolute, 58.0F))
        main.RowStyles.Add(New RowStyle(SizeType.Absolute, 174.0F))
        main.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        main.RowStyles.Add(New RowStyle(SizeType.Absolute, 58.0F))
        Controls.Add(main)

        Dim title As New Label() With {
            .Text = "Program Güncelleme Sihirbazı",
            .Dock = DockStyle.Fill,
            .Font = New Font("Segoe UI", 16.0F, FontStyle.Bold),
            .TextAlign = ContentAlignment.MiddleLeft,
            .BackColor = Color.White
        }
        main.Controls.Add(title, 0, 0)

        Dim top As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .BackColor = SystemColors.Control,
            .Padding = New Padding(10),
            .ColumnCount = 3,
            .RowCount = 4
        }
        top.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 155.0F))
        top.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        top.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 115.0F))
        top.RowStyles.Add(New RowStyle(SizeType.Absolute, 38.0F))
        top.RowStyles.Add(New RowStyle(SizeType.Absolute, 38.0F))
        top.RowStyles.Add(New RowStyle(SizeType.Absolute, 38.0F))
        top.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        main.Controls.Add(top, 0, 1)

        top.Controls.Add(CreateUpdateLabel("Güncelleme Paketi (.zip)"), 0, 0)
        txtPackage.Dock = DockStyle.Fill
        txtPackage.Margin = New Padding(4, 5, 8, 5)
        txtPackage.ReadOnly = True
        top.Controls.Add(txtPackage, 1, 0)

        Dim btnBrowse As New Button() With {.Text = "Paket Seç", .Dock = DockStyle.Fill, .Margin = New Padding(0, 4, 0, 4)}
        EnsureButtonTextFits(btnBrowse, 115)
        AddHandler btnBrowse.Click, AddressOf Browse_Click
        top.Controls.Add(btnBrowse, 2, 0)

        top.Controls.Add(CreateUpdateLabel("Ortak Kök Klasörü"), 0, 1)
        txtAppDir.Dock = DockStyle.Fill
        txtAppDir.Margin = New Padding(4, 5, 0, 5)
        txtAppDir.ReadOnly = True
        txtAppDir.Text = AppPaths.SharedRootDir
        top.SetColumnSpan(txtAppDir, 2)
        top.Controls.Add(txtAppDir, 1, 1)

        top.Controls.Add(CreateUpdateLabel("Sürümler Klasörü"), 0, 2)
        txtBackupDir.Dock = DockStyle.Fill
        txtBackupDir.Margin = New Padding(4, 5, 0, 5)
        txtBackupDir.ReadOnly = True
        txtBackupDir.Text = AppPaths.VersionsDir
        top.SetColumnSpan(txtBackupDir, 2)
        top.Controls.Add(txtBackupDir, 1, 2)

        Dim folderButtons As New FlowLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .FlowDirection = FlowDirection.LeftToRight,
            .WrapContents = False,
            .AutoScroll = True,
            .Margin = New Padding(4, 2, 0, 0)
        }
        top.SetColumnSpan(folderButtons, 2)
        top.Controls.Add(folderButtons, 1, 3)

        Dim btnOpenUpdates As New Button() With {.Text = "Updates Klasörü", .Width = 135, .Height = 28, .Margin = New Padding(0, 0, 10, 0)}
        EnsureButtonTextFits(btnOpenUpdates, 135)
        AddHandler btnOpenUpdates.Click, Sub() Process.Start(New ProcessStartInfo(AppPaths.UpdatesDir) With {.UseShellExecute = True})

        Dim btnOpenBackups As New Button() With {.Text = "Versions Klasörü", .Width = 135, .Height = 28, .Margin = New Padding(0)}
        EnsureButtonTextFits(btnOpenBackups, 135)
        AddHandler btnOpenBackups.Click, Sub() Process.Start(New ProcessStartInfo(AppPaths.VersionsDir) With {.UseShellExecute = True})

        folderButtons.Controls.AddRange({btnOpenUpdates, btnOpenBackups})

        txtInfo.Dock = DockStyle.Fill
        txtInfo.Multiline = True
        txtInfo.ReadOnly = True
        txtInfo.ScrollBars = ScrollBars.Vertical
        txtInfo.Font = New Font("Consolas", 10.0F, FontStyle.Regular)
        txtInfo.Text =
            "Kullanım:" & Environment.NewLine &
            "1. Yeni sürüm paketini .zip olarak seçin." & Environment.NewLine &
            "2. Paket ortak klasörde Versions altına ayrı bir sürüm olarak açılır." & Environment.NewLine &
            "3. Canlı program klasörü silinmez; açık kullanıcılar güncellemeyi engellemez." & Environment.NewLine &
            "4. TeknikResimOlcum.exe başlatıcı olarak ortak köke kopyalanır." & Environment.NewLine &
            "5. Kullanıcılar programı kapatıp başlatıcıdan açtığında yeni sürüme geçer." & Environment.NewLine & Environment.NewLine &
            "Not: Güncelleme paketi, build_release_update_zip.bat ile oluşturulan yayın ZIP'i olmalıdır." & Environment.NewLine &
            "Kaynak kod ZIP'i veya içinde _update_manifest.txt olmayan paket kabul edilmez." & Environment.NewLine &
            "Paket içinde TeknikResimOlcum.exe ve _update_manifest.txt bulunmalıdır."

        main.Controls.Add(txtInfo, 0, 2)

        Dim bottom As New FlowLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .BackColor = Color.White,
            .FlowDirection = FlowDirection.RightToLeft,
            .WrapContents = False,
            .AutoScroll = False,
            .Padding = New Padding(0, 8, 0, 4)
        }
        main.Controls.Add(bottom, 0, 3)

        Dim btnStart As New Button() With {.Text = "Sürümü Yayına Al", .Width = 180, .Height = 38, .Margin = New Padding(8, 0, 0, 0)}
        btnStart.Font = New Font("Segoe UI", 9.0F, FontStyle.Bold)
        EnsureButtonTextFits(btnStart, 180)
        btnStart.MinimumSize = New Size(btnStart.Width, btnStart.Height)
        AddHandler btnStart.Click, AddressOf StartUpdate_Click

        Dim btnClose As New Button() With {.Text = "Kapat", .Width = 100, .Height = 38, .Margin = New Padding(8, 0, 0, 0)}
        EnsureButtonTextFits(btnClose, 100)
        AddHandler btnClose.Click, Sub() Close()

        bottom.Controls.AddRange({btnClose, btnStart})

        If Not String.IsNullOrWhiteSpace(initialPackagePath) AndAlso File.Exists(initialPackagePath) Then
            txtPackage.Text = initialPackagePath
            txtInfo.Text = "Yeni güncelleme paketi otomatik seçildi:" & Environment.NewLine &
                           initialPackagePath & Environment.NewLine & Environment.NewLine &
                           txtInfo.Text
        End If
    End Sub

    Private Shared Sub EnsureButtonTextFits(button As Button, minimumWidth As Integer)
        If button Is Nothing Then Return

        Dim measured = TextRenderer.MeasureText(
            button.Text & "  ",
            button.Font,
            New Size(Integer.MaxValue, Integer.MaxValue),
            TextFormatFlags.SingleLine)

        button.AutoEllipsis = False
        button.Width = Math.Max(minimumWidth, measured.Width + button.Padding.Horizontal + 18)
    End Sub

    Private Function CreateUpdateLabel(text As String) As Label
        Return New Label() With {
            .Text = text,
            .Dock = DockStyle.Fill,
            .TextAlign = ContentAlignment.MiddleLeft,
            .Margin = New Padding(0, 3, 8, 3),
            .BackColor = Color.Transparent,
            .AutoEllipsis = True
        }
    End Function

    Private Sub Browse_Click(sender As Object, e As EventArgs)
        Using dlg As New OpenFileDialog()
            dlg.Title = "Güncelleme paketi seç"
            dlg.Filter = "ZIP Güncelleme Paketi (*.zip)|*.zip"
            dlg.InitialDirectory = AppPaths.UpdatesDir

            If dlg.ShowDialog(Me) = DialogResult.OK Then
                txtPackage.Text = dlg.FileName
            End If
        End Using
    End Sub

    Private Sub StartUpdate_Click(sender As Object, e As EventArgs)
        Try
            Dim packagePath = txtPackage.Text.Trim()

            If packagePath = "" OrElse Not File.Exists(packagePath) Then
                MessageBox.Show("Lütfen geçerli bir güncelleme paketi seçiniz.", "Paket seçilmedi", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            If Not packagePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) Then
                MessageBox.Show("Güncelleme paketi .zip formatında olmalıdır.", "Hatalı paket", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            Dim validationMessage As String = ""
            If Not UpdatePackageSecurity.ValidatePackage(packagePath, validationMessage) Then
                MessageBox.Show(validationMessage, "Geçersiz güncelleme paketi", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            Dim packageBuildStamp As String = ""
            Dim packageVersionMessage As String = ""
            If Not UpdatePackageSecurity.TryGetValidatedBuildStamp(packagePath, packageBuildStamp, packageVersionMessage) Then
                MessageBox.Show(packageVersionMessage, "Geçersiz güncelleme paketi", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            Dim confirm = MessageBox.Show("Seçilen paket ortak klasörde yeni sürüm olarak yayına alınacaktır." & Environment.NewLine &
                                          "Açık kullanıcıların program dosyaları silinmeyecek veya değiştirilmeyecektir." & Environment.NewLine &
                                          "Kullanıcılar programı kapatıp ortak klasördeki TeknikResimOlcum.exe ile açtığında yeni sürüme geçer." & Environment.NewLine & Environment.NewLine &
                                          "Devam edilsin mi?",
                                          "Sürümü yayına al", MessageBoxButtons.YesNo, MessageBoxIcon.Question)

            If confirm <> DialogResult.Yes Then Return

            Dim deployedVersion = DeployVersionedUpdate(packagePath, packageBuildStamp)
            If Not String.IsNullOrWhiteSpace(lastLauncherPublishWarning) Then
                MessageBox.Show(lastLauncherPublishWarning & Environment.NewLine & Environment.NewLine &
                                "Bu gecis tamamlanana kadar ortak klasordeki TeknikResimOlcumBaslat.exe dosyasini kullanin.",
                                "Baslatici kullanimda", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            End If
            txtInfo.Text = "Sürüm yayına alındı." & Environment.NewLine &
                           "Sürüm: " & deployedVersion & Environment.NewLine &
                           "Ortak kök: " & AppPaths.SharedRootDir & Environment.NewLine &
                           "Başlatıcı: " & AppPaths.LauncherPath & Environment.NewLine & Environment.NewLine &
                           "Bundan sonra kullanıcılar programı ortak klasördeki TeknikResimOlcum.exe üzerinden açmalıdır." & Environment.NewLine &
                           "Açık kullanıcılar programı kapatıp tekrar açtığında yeni sürüme geçer."

            MessageBox.Show("Sürüm yayına alındı." & Environment.NewLine & Environment.NewLine &
                            "Kullanıcılara artık ortak klasördeki TeknikResimOlcum.exe üzerinden açmalarını söyleyebilirsiniz.",
                            "Güncelleme Hazır", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return

            Dim stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss")
            Dim updateWorkDir = Path.Combine(AppPaths.TempDir, "ProgramUpdate_" & stamp)
            Dim backupDir = Path.Combine(AppPaths.BackupsDir, "Backup_" & stamp)
            Directory.CreateDirectory(updateWorkDir)
            Directory.CreateDirectory(backupDir)

            Dim stagedPackagePath = Path.Combine(updateWorkDir, "update_package.zip")
            File.Copy(packagePath, stagedPackagePath, True)

            Dim stagedValidationMessage As String = ""
            If Not UpdatePackageSecurity.ValidatePackage(stagedPackagePath, stagedValidationMessage) Then
                MessageBox.Show(stagedValidationMessage, "GeÃ§ersiz gÃ¼ncelleme paketi", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If
            Dim stagedPackageSha256 = FileSha256Hex(stagedPackagePath)

            Dim psPath = Path.Combine(updateWorkDir, "run_update.ps1")
            Dim batPath = Path.Combine(updateWorkDir, "run_update.bat")
            Dim logPath = Path.Combine(updateWorkDir, "update_log.txt")

            File.WriteAllText(psPath, BuildPowerShellScript(Application.StartupPath, stagedPackagePath, backupDir, logPath, stagedPackageSha256), New UTF8Encoding(False))
            File.WriteAllText(batPath, BuildBatchScript(psPath), Encoding.Default)

            AuditService.Log("PROGRAM_UPDATE_PREPARE", "", "", "Package=" & packagePath & "; StagedPackage=" & stagedPackagePath & "; Backup=" & backupDir)

            Process.Start(New ProcessStartInfo(batPath) With {
                .UseShellExecute = True,
                .WindowStyle = ProcessWindowStyle.Normal
            })

            Application.Exit()
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Güncelleme başlatılamadı", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Function DeployVersionedUpdate(packagePath As String, buildStamp As String) As String
        Dim version = SafePathSegment(buildStamp)
        If version = "" Then Throw New InvalidDataException("Paket sürüm bilgisi okunamadı.")

        AppPaths.EnsureFolders()

        Dim stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss")
        Dim workDir = Path.Combine(AppPaths.TempDir, "VersionDeploy_" & stamp)
        Dim extractDir = Path.Combine(workDir, "payload")
        Dim targetVersionDir = Path.Combine(AppPaths.VersionsDir, version)
        Dim stagingVersionDir = Path.Combine(AppPaths.VersionsDir, "_deploying_" & version & "_" & stamp)

        Directory.CreateDirectory(workDir)

        Try
            If Directory.Exists(extractDir) Then Directory.Delete(extractDir, True)
            Directory.CreateDirectory(extractDir)
            ZipFile.ExtractToDirectory(packagePath, extractDir, True)

            Dim sourceDir = ResolveExtractedPayloadRoot(extractDir)
            Dim sourceExe = Path.Combine(sourceDir, "TeknikResimOlcum.exe")
            Dim sourceManifest = Path.Combine(sourceDir, UpdatePackageSecurity.ManifestFileName)
            Dim sourceSignature = Path.Combine(sourceDir, UpdatePackageSecurity.SignatureFileName)

            If Not File.Exists(sourceExe) Then Throw New FileNotFoundException("Paket içinde TeknikResimOlcum.exe bulunamadı.", sourceExe)
            If Not File.Exists(sourceManifest) Then Throw New FileNotFoundException("Paket içinde manifest bulunamadı.", sourceManifest)
            If Not File.Exists(sourceSignature) Then Throw New FileNotFoundException("Paket içinde imza dosyası bulunamadı.", sourceSignature)

            If Directory.Exists(stagingVersionDir) Then Directory.Delete(stagingVersionDir, True)

            If Directory.Exists(targetVersionDir) Then
                If Not IsUsableVersionDirectory(targetVersionDir) Then
                    Throw New IOException("Hedef sürüm klasörü zaten var ancak eksik görünüyor: " & targetVersionDir)
                End If
                EnsureLauncherPayload(sourceDir, targetVersionDir)
            Else
                CopyDirectory(sourceDir, stagingVersionDir)
                File.WriteAllText(Path.Combine(stagingVersionDir, AppPaths.SharedRootConfigFileName),
                                  AppPaths.SharedRootDir,
                                  New UTF8Encoding(False))
                Directory.Move(stagingVersionDir, targetVersionDir)
            End If

            Dim launcherWarning = PublishLauncher(targetVersionDir)
            lastLauncherPublishWarning = launcherWarning

            If Not File.Exists(AppPaths.LauncherPath) AndAlso
               Not File.Exists(Path.Combine(AppPaths.SharedRootDir, "TeknikResimOlcumBaslat.exe")) Then
                Throw New IOException("Ortak klasorde kullanilabilir bir program baslaticisi olusturulamadi. " & launcherWarning)
            End If

            WriteCurrentVersion(version)

            AuditService.Log("PROGRAM_VERSION_DEPLOY", "", "", "Version=" & version & "; Package=" & packagePath & "; Target=" & targetVersionDir & "; LauncherWarning=" & launcherWarning)

            Return version
        Finally
            Try
                If Directory.Exists(stagingVersionDir) Then Directory.Delete(stagingVersionDir, True)
            Catch
            End Try

            Try
                If Directory.Exists(workDir) Then Directory.Delete(workDir, True)
            Catch
            End Try
        End Try
    End Function

    Private Function ResolveExtractedPayloadRoot(extractDir As String) As String
        Dim items = Directory.GetFileSystemEntries(extractDir)
        If items.Length = 1 AndAlso Directory.Exists(items(0)) Then Return items(0)
        Return extractDir
    End Function

    Private Function IsUsableVersionDirectory(versionDir As String) As Boolean
        Return File.Exists(Path.Combine(versionDir, "TeknikResimOlcum.exe")) AndAlso
               File.Exists(Path.Combine(versionDir, UpdatePackageSecurity.ManifestFileName)) AndAlso
               File.Exists(Path.Combine(versionDir, UpdatePackageSecurity.SignatureFileName))
    End Function

    Private Function ResolveLauncherSource(rootDir As String) As String
        Dim launcherSource = Path.Combine(rootDir, AppPaths.LauncherPayloadPath)
        If Not File.Exists(launcherSource) Then
            Dim legacyLauncherSource = Path.Combine(rootDir, "TeknikResimOlcumBaslat.exe")
            If File.Exists(legacyLauncherSource) Then launcherSource = legacyLauncherSource
        End If

        If File.Exists(launcherSource) Then Return launcherSource
        Return ""
    End Function

    Private Sub EnsureLauncherPayload(sourceDir As String, targetVersionDir As String)
        Dim sourceLauncher = ResolveLauncherSource(sourceDir)
        If sourceLauncher = "" Then Return

        Dim relative = Path.GetRelativePath(sourceDir, sourceLauncher)
        Dim targetLauncher = Path.Combine(targetVersionDir, relative)
        If File.Exists(targetLauncher) Then Return

        Directory.CreateDirectory(Path.GetDirectoryName(targetLauncher))
        File.Copy(sourceLauncher, targetLauncher, True)
    End Sub

    Private Function PublishLauncher(versionDir As String) As String
        Dim launcherSource = ResolveLauncherSource(versionDir)

        If launcherSource = "" Then
            Dim sharedLegacyLauncher = Path.Combine(AppPaths.SharedRootDir, "TeknikResimOlcumBaslat.exe")
            If File.Exists(sharedLegacyLauncher) Then
                Return PublishLauncherAliases(sharedLegacyLauncher)
            End If
        End If
        If Not File.Exists(launcherSource) Then
            Throw New FileNotFoundException("Paket içinde başlatıcı bulunamadı. build_release_update_zip.ps1 ile paketi yeniden oluşturun.", launcherSource)
        End If

        Return PublishLauncherAliases(launcherSource)
    End Function

    Private Function PublishLauncherAliases(launcherSource As String) As String
        If String.IsNullOrWhiteSpace(launcherSource) OrElse Not File.Exists(launcherSource) Then Return ""

        Dim failedAliases As New List(Of String)()
        Dim aliases = {
            Path.Combine(AppPaths.SharedRootDir, "TeknikResimOlcumBaslat.exe"),
            AppPaths.LauncherPath
        }

        For Each destination In aliases
            If LauncherFilesMatch(launcherSource, destination) Then Continue For

            If Not TryCopyLauncherWithRetry(launcherSource, destination) Then
                failedAliases.Add(Path.GetFileName(destination))
            End If
        Next

        If failedAliases.Count = 0 Then Return ""

        Return String.Join(", ", failedAliases) &
               " baska bir kullanici tarafindan kullanildigi icin degistirilemedi. " &
               "Mevcut dosya korunarak yeni program surumu yine de yayina alindi."
    End Function

    Private Function TryCopyLauncherWithRetry(sourcePath As String, destinationPath As String) As Boolean
        Const maxAttempts As Integer = 20

        For attempt As Integer = 1 To maxAttempts
            Try
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath))
                File.Copy(sourcePath, destinationPath, True)
                Return True
            Catch ex As IOException
                If attempt = maxAttempts Then Return False
            Catch ex As UnauthorizedAccessException
                If attempt = maxAttempts Then Return False
            End Try

            Threading.Thread.Sleep(300)
        Next

        Return False
    End Function

    Private Function LauncherFilesMatch(sourcePath As String, destinationPath As String) As Boolean
        Try
            If String.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(destinationPath), StringComparison.OrdinalIgnoreCase) Then Return True
            If Not File.Exists(sourcePath) OrElse Not File.Exists(destinationPath) Then Return False

            Dim sourceInfo As New FileInfo(sourcePath)
            Dim destinationInfo As New FileInfo(destinationPath)
            If sourceInfo.Length <> destinationInfo.Length Then Return False

            Return String.Equals(FileSha256Hex(sourcePath), FileSha256Hex(destinationPath), StringComparison.OrdinalIgnoreCase)
        Catch
            Return False
        End Try
    End Function

    Private Sub WriteCurrentVersion(version As String)
        Directory.CreateDirectory(AppPaths.SharedRootDir)

        Dim tempPath = Path.Combine(AppPaths.SharedRootDir, AppPaths.CurrentVersionFileName & ".tmp")
        File.WriteAllText(tempPath, version, New UTF8Encoding(False))
        File.Copy(tempPath, AppPaths.CurrentVersionFile, True)

        Try
            File.Delete(tempPath)
        Catch
        End Try
    End Sub

    Private Sub CopyDirectory(sourceDir As String, destinationDir As String)
        Directory.CreateDirectory(destinationDir)

        For Each directoryPath In Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories)
            Dim relative = Path.GetRelativePath(sourceDir, directoryPath)
            Directory.CreateDirectory(Path.Combine(destinationDir, relative))
        Next

        For Each filePath In Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories)
            Dim relative = Path.GetRelativePath(sourceDir, filePath)
            Dim destinationPath = Path.Combine(destinationDir, relative)
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath))
            File.Copy(filePath, destinationPath, True)
        Next
    End Sub

    Private Function SafePathSegment(value As String) As String
        Dim raw = If(value, "").Trim()
        If raw = "" Then Return ""

        For Each ch In Path.GetInvalidFileNameChars()
            raw = raw.Replace(ch, "_"c)
        Next

        raw = raw.Replace(Path.DirectorySeparatorChar, "_"c).
                  Replace(Path.AltDirectorySeparatorChar, "_"c).
                  Trim("."c, " "c)

        Return raw
    End Function

    Private Function LegacyValidateUpdatePackage(packagePath As String, ByRef message As String) As Boolean
        Try
            Using archive = ZipFile.OpenRead(packagePath)
                If archive.Entries.Count = 0 Then
                    message = "Güncelleme paketi boş görünüyor."
                    Return False
                End If

                Dim names = archive.Entries.
                    Select(Function(entry) NormalizeZipName(entry.FullName)).
                    Where(Function(name) name <> "").
                    ToList()

                If names.Any(Function(name) name.EndsWith(".vbproj", StringComparison.OrdinalIgnoreCase) OrElse
                                            name.StartsWith("Forms/", StringComparison.OrdinalIgnoreCase) OrElse
                                            name.StartsWith("Services/", StringComparison.OrdinalIgnoreCase) OrElse
                                            name.StartsWith("Models/", StringComparison.OrdinalIgnoreCase)) Then
                    message = "Seçilen paket kaynak kod ZIP'i gibi görünüyor." & Environment.NewLine &
                              "Güncelleme için build_release_update_zip.bat ile oluşturulan yayın ZIP'i seçilmelidir."
                    Return False
                End If

                Dim manifestEntry = archive.Entries.FirstOrDefault(Function(entry) String.Equals(ZipFileName(entry.FullName), "_update_manifest.txt", StringComparison.OrdinalIgnoreCase))
                If manifestEntry Is Nothing Then
                    message = "Paket içinde _update_manifest.txt bulunamadı." & Environment.NewLine &
                              "Güncelleme paketini build_release_update_zip.bat ile yeniden oluşturunuz."
                    Return False
                End If

                Dim hasExe = archive.Entries.Any(Function(entry) String.Equals(ZipFileName(entry.FullName), "TeknikResimOlcum.exe", StringComparison.OrdinalIgnoreCase))
                If Not hasExe Then
                    message = "Paket içinde TeknikResimOlcum.exe bulunamadı." & Environment.NewLine &
                              "Kaynak kod ZIP'i değil, yayınlanmış güncelleme ZIP'i seçilmelidir."
                    Return False
                End If

                Dim manifestText As String = ""
                Using sr As New StreamReader(manifestEntry.Open(), Encoding.UTF8, True)
                    manifestText = sr.ReadToEnd()
                End Using

                If manifestText.IndexOf("PackageType=TeknikResimOlcumUpdate", StringComparison.OrdinalIgnoreCase) < 0 OrElse
                   manifestText.IndexOf("AppName=TeknikResimOlcum", StringComparison.OrdinalIgnoreCase) < 0 Then
                    message = "Paket manifest bilgisi bu programa ait değil." & Environment.NewLine &
                              "Güncelleme paketini build_release_update_zip.bat ile yeniden oluşturunuz."
                    Return False
                End If
            End Using

            message = "Paket geçerli."
            Return True
        Catch ex As InvalidDataException
            message = "ZIP paketi okunamadı veya bozuk görünüyor." & Environment.NewLine & ex.Message
            Return False
        Catch ex As Exception
            message = "Güncelleme paketi doğrulanamadı." & Environment.NewLine & ex.Message
            Return False
        End Try
    End Function

    Private Function NormalizeZipName(entryName As String) As String
        Return If(entryName, "").Replace("\"c, "/"c).TrimStart("/"c)
    End Function

    Private Function ZipFileName(entryName As String) As String
        Dim name = NormalizeZipName(entryName)
        Dim idx = name.LastIndexOf("/"c)
        If idx >= 0 Then Return name.Substring(idx + 1)
        Return name
    End Function

    Private Function FileSha256Hex(filePath As String) As String
        Using input = File.OpenRead(filePath)
            Using sha As SHA256 = SHA256.Create()
                Return BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "")
            End Using
        End Using
    End Function

    Private Shared Function PsLiteral(value As String) As String
        Return "'" & If(value, "").Replace("'", "''") & "'"
    End Function

    Private Function BuildBatchScript(psPath As String) As String
        Dim sb As New StringBuilder()
        sb.AppendLine("@echo off")
        sb.AppendLine("chcp 65001 > nul")
        sb.AppendLine("powershell -NoProfile -ExecutionPolicy Bypass -File " & """" & psPath & """")
        sb.AppendLine("exit /b %ERRORLEVEL%")
        Return sb.ToString()
    End Function

    Private Shared Function BuildPowerShellScript(appDir As String, zipPath As String, backupDir As String, logPath As String, expectedZipSha256 As String) As String
        Dim sb As New StringBuilder()

        sb.AppendLine("$ErrorActionPreference = 'Stop'")
        sb.AppendLine("$appDir = " & PsLiteral(appDir))
        sb.AppendLine("$zipPath = " & PsLiteral(zipPath))
        sb.AppendLine("$backupDir = " & PsLiteral(backupDir))
        sb.AppendLine("$logPath = " & PsLiteral(logPath))
        sb.AppendLine("$transcriptPath = $logPath + '.transcript.txt'")
        sb.AppendLine("$expectedZipSha256 = " & PsLiteral(expectedZipSha256))
        sb.AppendLine("$exclude = @('Data','Drawings','Temp','Backups','Updates')")
        sb.AppendLine("$workDir = Split-Path -Parent $zipPath")
        sb.AppendLine("$extractDir = Join-Path $workDir 'extracted_payload'")
        sb.AppendLine("$deploymentStarted = $false")
        sb.AppendLine("$backupReady = $false")
        sb.AppendLine("$updateSucceeded = $false")
        sb.AppendLine("$rollbackSucceeded = $false")
        sb.AppendLine("$showLog = $false")
        sb.AppendLine("$exitCode = 0")
        sb.AppendLine("")
        sb.AppendLine("function Write-UpdateLog {")
        sb.AppendLine("  param([string]$Message)")
        sb.AppendLine("  try { Add-Content -LiteralPath $logPath -Value ((Get-Date -Format 'yyyy-MM-dd HH:mm:ss') + ' | ' + $Message) -Encoding UTF8 } catch { }")
        sb.AppendLine("}")
        sb.AppendLine("")
        sb.AppendLine("function Invoke-WithRetry {")
        sb.AppendLine("  param([scriptblock]$Operation, [string]$Description, [int]$Attempts = 20)")
        sb.AppendLine("  for ($attempt = 1; $attempt -le $Attempts; $attempt++) {")
        sb.AppendLine("    try { & $Operation; return } catch {")
        sb.AppendLine("      if ($attempt -eq $Attempts) { throw ($Description + ': ' + $_.Exception.Message) }")
        sb.AppendLine("      Start-Sleep -Milliseconds 500")
        sb.AppendLine("    }")
        sb.AppendLine("  }")
        sb.AppendLine("}")
        sb.AppendLine("")
        sb.AppendLine("function Get-DeployItems {")
        sb.AppendLine("  param([string]$Root)")
        sb.AppendLine("  return @(Get-ChildItem -LiteralPath $Root -Force | Where-Object { $exclude -notcontains $_.Name })")
        sb.AppendLine("}")
        sb.AppendLine("")
        sb.AppendLine("function Copy-DeployItems {")
        sb.AppendLine("  param([string]$SourceRoot, [string]$DestinationRoot)")
        sb.AppendLine("  New-Item -ItemType Directory -Force -Path $DestinationRoot | Out-Null")
        sb.AppendLine("  foreach ($item in @(Get-DeployItems $SourceRoot)) {")
        sb.AppendLine("    $sourcePath = $item.FullName")
        sb.AppendLine("    Invoke-WithRetry { Copy-Item -LiteralPath $sourcePath -Destination $DestinationRoot -Recurse -Force } ('Kopyalama basarisiz: ' + $sourcePath)")
        sb.AppendLine("  }")
        sb.AppendLine("}")
        sb.AppendLine("")
        sb.AppendLine("function Remove-DeployItems {")
        sb.AppendLine("  param([string]$Root)")
        sb.AppendLine("  foreach ($item in @(Get-DeployItems $Root)) {")
        sb.AppendLine("    $targetPath = $item.FullName")
        sb.AppendLine("    Invoke-WithRetry { Remove-Item -LiteralPath $targetPath -Recurse -Force } ('Silme basarisiz: ' + $targetPath)")
        sb.AppendLine("  }")
        sb.AppendLine("}")
        sb.AppendLine("")
        sb.AppendLine("function Get-DeployFileMap {")
        sb.AppendLine("  param([string]$Root)")
        sb.AppendLine("  $map = @{}")
        sb.AppendLine("  $rootFull = [IO.Path]::GetFullPath($Root).TrimEnd('\') + '\' ")
        sb.AppendLine("  foreach ($item in @(Get-DeployItems $Root)) {")
        sb.AppendLine("    $files = if ($item.PSIsContainer) { @(Get-ChildItem -LiteralPath $item.FullName -Recurse -Force -File) } else { @($item) }")
        sb.AppendLine("    foreach ($file in $files) {")
        sb.AppendLine("      $relative = $file.FullName.Substring($rootFull.Length)")
        sb.AppendLine("      $map[$relative] = [PSCustomObject]@{ Length = $file.Length; Hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $file.FullName).Hash }")
        sb.AppendLine("    }")
        sb.AppendLine("  }")
        sb.AppendLine("  return $map")
        sb.AppendLine("}")
        sb.AppendLine("")
        sb.AppendLine("function Assert-TreeMatches {")
        sb.AppendLine("  param([string]$SourceRoot, [string]$DestinationRoot, [string]$Description)")
        sb.AppendLine("  $sourceMap = Get-DeployFileMap $SourceRoot")
        sb.AppendLine("  $destinationMap = Get-DeployFileMap $DestinationRoot")
        sb.AppendLine("  foreach ($relative in $sourceMap.Keys) {")
        sb.AppendLine("    if (-not $destinationMap.ContainsKey($relative)) { throw ($Description + ' eksik dosya: ' + $relative) }")
        sb.AppendLine("    if ($sourceMap[$relative].Length -ne $destinationMap[$relative].Length -or $sourceMap[$relative].Hash -ne $destinationMap[$relative].Hash) {")
        sb.AppendLine("      throw ($Description + ' dosya dogrulama hatasi: ' + $relative)")
        sb.AppendLine("    }")
        sb.AppendLine("  }")
        sb.AppendLine("}")
        sb.AppendLine("")
        sb.AppendLine("try { Start-Transcript -Path $transcriptPath -Force | Out-Null } catch { }")
        sb.AppendLine("try {")
        sb.AppendLine("  Write-UpdateLog 'Guncelleme islemi basladi.'")
        sb.AppendLine("  Start-Sleep -Seconds 3")
        sb.AppendLine("  $actualZipSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $zipPath).Hash.ToUpperInvariant()")
        sb.AppendLine("  if ($actualZipSha256 -ne $expectedZipSha256) { throw 'Guncelleme paketi hazirlandiktan sonra degistirilmis gorunuyor.' }")
        sb.AppendLine("")
        sb.AppendLine("  # Mevcut programa dokunmadan once paketi ayri klasore cikar ve temel dosyalari dogrula.")
        sb.AppendLine("  if (Test-Path -LiteralPath $extractDir) { Remove-Item -LiteralPath $extractDir -Recurse -Force }")
        sb.AppendLine("  New-Item -ItemType Directory -Force -Path $extractDir | Out-Null")
        sb.AppendLine("  Expand-Archive -LiteralPath $zipPath -DestinationPath $extractDir -Force")
        sb.AppendLine("  $srcDir = $extractDir")
        sb.AppendLine("  $items = @(Get-ChildItem -LiteralPath $extractDir -Force)")
        sb.AppendLine("  if ($items.Count -eq 1 -and $items[0].PSIsContainer) { $srcDir = $items[0].FullName }")
        sb.AppendLine("  $stagedExe = Join-Path $srcDir 'TeknikResimOlcum.exe'")
        sb.AppendLine("  $stagedManifest = Join-Path $srcDir '_update_manifest.txt'")
        sb.AppendLine("  $stagedSignature = Join-Path $srcDir '_update_signature.txt'")
        sb.AppendLine("  if (-not (Test-Path -LiteralPath $stagedExe -PathType Leaf)) { throw 'Cikarilan pakette TeknikResimOlcum.exe bulunamadi.' }")
        sb.AppendLine("  if (-not (Test-Path -LiteralPath $stagedManifest -PathType Leaf)) { throw 'Cikarilan pakette manifest bulunamadi.' }")
        sb.AppendLine("  if (-not (Test-Path -LiteralPath $stagedSignature -PathType Leaf)) { throw 'Cikarilan pakette imza dosyasi bulunamadi.' }")
        sb.AppendLine("")
        sb.AppendLine("  # Tam ve dogrulanmis yedek olusmadan canli dosyalari degistirme.")
        sb.AppendLine("  New-Item -ItemType Directory -Force -Path $backupDir | Out-Null")
        sb.AppendLine("  Copy-DeployItems $appDir $backupDir")
        sb.AppendLine("  Assert-TreeMatches $appDir $backupDir 'Yedek'")
        sb.AppendLine("  $backupExe = Join-Path $backupDir 'TeknikResimOlcum.exe'")
        sb.AppendLine("  if (-not (Test-Path -LiteralPath $backupExe -PathType Leaf)) { throw 'Yedekte TeknikResimOlcum.exe bulunamadi; guncelleme iptal edildi.' }")
        sb.AppendLine("  $backupReady = $true")
        sb.AppendLine("")
        sb.AppendLine("  # Bu noktadan sonraki her hata otomatik rollback tetikler.")
        sb.AppendLine("  $deploymentStarted = $true")
        sb.AppendLine("  Remove-DeployItems $appDir")
        sb.AppendLine("  Copy-DeployItems $srcDir $appDir")
        sb.AppendLine("  Assert-TreeMatches $srcDir $appDir 'Guncelleme'")
        sb.AppendLine("  $exePath = Join-Path $appDir 'TeknikResimOlcum.exe'")
        sb.AppendLine("  if (-not (Test-Path -LiteralPath $exePath -PathType Leaf)) { throw 'Guncelleme sonrasi TeknikResimOlcum.exe bulunamadi.' }")
        sb.AppendLine("  $updateSucceeded = $true")
        sb.AppendLine("  Write-UpdateLog 'Guncelleme basariyla tamamlandi.'")
        sb.AppendLine("  try { Remove-Item -LiteralPath $extractDir -Recurse -Force -ErrorAction SilentlyContinue } catch { }")
        sb.AppendLine("  Start-Process -FilePath $exePath -WorkingDirectory $appDir")
        sb.AppendLine("} catch {")
        sb.AppendLine("  $exitCode = 1")
        sb.AppendLine("  $showLog = $true")
        sb.AppendLine("  $failureMessage = $_.Exception.Message")
        sb.AppendLine("  Write-UpdateLog ('GUNCELLEME HATASI: ' + $failureMessage)")
        sb.AppendLine("")
        sb.AppendLine("  if ($deploymentStarted -and $backupReady) {")
        sb.AppendLine("    try {")
        sb.AppendLine("      Write-UpdateLog 'ROLLBACK BASLADI: Mevcut dosyalar temizlenip yedek geri yukleniyor.'")
        sb.AppendLine("      Remove-DeployItems $appDir")
        sb.AppendLine("      Copy-DeployItems $backupDir $appDir")
        sb.AppendLine("      Assert-TreeMatches $backupDir $appDir 'Rollback'")
        sb.AppendLine("      $restoredExe = Join-Path $appDir 'TeknikResimOlcum.exe'")
        sb.AppendLine("      if (-not (Test-Path -LiteralPath $restoredExe -PathType Leaf)) { throw 'Rollback sonrasi TeknikResimOlcum.exe bulunamadi.' }")
        sb.AppendLine("      $rollbackSucceeded = $true")
        sb.AppendLine("      Write-UpdateLog 'ROLLBACK TAMAMLANDI: Onceki program surumu geri yuklendi.'")
        sb.AppendLine("    } catch {")
        sb.AppendLine("      Write-UpdateLog ('ROLLBACK HATASI: ' + $_.Exception.Message)")
        sb.AppendLine("    }")
        sb.AppendLine("  } else {")
        sb.AppendLine("    Write-UpdateLog 'Canli program dosyalari degistirilmeden guncelleme durduruldu.'")
        sb.AppendLine("  }")
        sb.AppendLine("")
        sb.AppendLine("  $restartExe = Join-Path $appDir 'TeknikResimOlcum.exe'")
        sb.AppendLine("  if (Test-Path -LiteralPath $restartExe -PathType Leaf) {")
        sb.AppendLine("    try { Start-Process -FilePath $restartExe -WorkingDirectory $appDir } catch {")
        sb.AppendLine("      Write-UpdateLog ('Program yeniden baslatilamadi: ' + $_.Exception.Message)")
        sb.AppendLine("    }")
        sb.AppendLine("  }")
        sb.AppendLine("} finally {")
        sb.AppendLine("  if (-not $updateSucceeded) { try { Remove-Item -LiteralPath $extractDir -Recurse -Force -ErrorAction SilentlyContinue } catch { } }")
        sb.AppendLine("  try { Stop-Transcript | Out-Null } catch { }")
        sb.AppendLine("}")
        sb.AppendLine("if ($showLog) { try { Start-Process notepad.exe $logPath } catch { } }")
        sb.AppendLine("exit $exitCode")

        Return sb.ToString()
    End Function
End Class
