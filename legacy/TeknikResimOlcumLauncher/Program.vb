Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.IO
Imports System.Linq
Imports System.Security.Cryptography
Imports System.Text
Imports System.Threading
Imports System.Windows.Forms

Module Program
    Private Const AppName As String = "TeknikResimOlcum"
    Private Const AppExeName As String = "TeknikResimOlcum.exe"
    Private Const CurrentVersionFileName As String = "CurrentVersion.txt"
    Private Const SharedRootConfigFileName As String = "_shared_root.txt"
    Private Const SharedRootEnvironmentVariable As String = "TEKNIKRESIMOLCUM_SHARED_ROOT"
    Private Const ManifestFileName As String = "_update_manifest.txt"
    Private Const LaunchIoRetryCount As Integer = 12
    Private Const LaunchIoRetryDelayMs As Integer = 250

    <STAThread>
    Sub Main()
        Application.EnableVisualStyles()
        Application.SetCompatibleTextRenderingDefault(False)

        Try
            Dim sharedRoot = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            TrySynchronizeSharedLauncherAliases(sharedRoot)
            Dim version = ResolveCurrentVersion(sharedRoot)

            If String.IsNullOrWhiteSpace(version) Then
                version = TryBootstrapSharedRootFromPayload(sharedRoot)
            End If

            If String.IsNullOrWhiteSpace(version) Then
                MessageBox.Show("Güncel program sürümü bulunamadı." & Environment.NewLine &
                                "Ortak klasörde CurrentVersion.txt veya Versions klasörü hazırlanmalıdır." & Environment.NewLine & Environment.NewLine &
                                "İlk kurulum için güncelleme paketini Program Güncelleme Sihirbazı veya install_shared_versioned_update.ps1 ile yayına alın.",
                                "Program Başlatılamadı", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            Dim sourceDir = Path.Combine(sharedRoot, "Versions", version)
            Dim sourceExe = Path.Combine(sourceDir, AppExeName)

            If Not File.Exists(sourceExe) Then
                MessageBox.Show("Güncel sürüm klasöründe program bulunamadı:" & Environment.NewLine & sourceExe,
                                "Program Başlatılamadı", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            Dim localVersionDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                AppName,
                "Versions",
                SafePathSegment(version))

            EnsureLocalVersion(sourceDir, localVersionDir, sharedRoot)
            CleanupOldLocalVersions(localVersionDir)

            Dim localExe = Path.Combine(localVersionDir, AppExeName)
            If Not File.Exists(localExe) Then
                Throw New FileNotFoundException("Yerel program kopyası bulunamadı.", localExe)
            End If

            Dim psi As New ProcessStartInfo(localExe) With {
                .WorkingDirectory = localVersionDir,
                .UseShellExecute = False
            }
            psi.Environment(SharedRootEnvironmentVariable) = sharedRoot
            psi.Environment("TEKNIKRESIMOLCUM_LAUNCHER_VERSION") = version

            Process.Start(psi)
        Catch ex As Exception
            LogLauncherIssue("Program baslatilamadi.", ex)
            MessageBox.Show("Program başlatılamadı:" & Environment.NewLine & ex.Message,
                            "Teknik Resim Ölçüm", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub TrySynchronizeSharedLauncherAliases(sharedRoot As String)
        Try
            If String.IsNullOrWhiteSpace(sharedRoot) Then Return
            If Not File.Exists(Path.Combine(sharedRoot, CurrentVersionFileName)) AndAlso
               Not Directory.Exists(Path.Combine(sharedRoot, "Versions")) Then Return

            Dim launcherSource = Application.ExecutablePath
            If String.IsNullOrWhiteSpace(launcherSource) OrElse Not File.Exists(launcherSource) Then Return

            Dim aliases = {
                Path.Combine(sharedRoot, AppExeName),
                Path.Combine(sharedRoot, "TeknikResimOlcumBaslat.exe")
            }

            For Each destination In aliases
                If String.Equals(Path.GetFullPath(launcherSource), Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase) Then Continue For
                If FilesMatch(launcherSource, destination) Then Continue For

                Dim copied As Boolean = False
                For attempt = 1 To 3
                    Try
                        File.Copy(launcherSource, destination, True)
                        copied = True
                        Exit For
                    Catch ex As IOException
                        If attempt < 3 Then Thread.Sleep(200)
                    Catch ex As UnauthorizedAccessException
                        If attempt < 3 Then Thread.Sleep(200)
                    End Try
                Next

                If Not copied Then
                    LogLauncherIssue("Ortak baslatici kullanildigi icin henuz esitlenemedi: " & destination,
                                     New IOException("Dosya baska bir islem tarafindan kullaniliyor."))
                End If
            Next
        Catch ex As Exception
            LogLauncherIssue("Ortak baslatici adlari esitlenemedi.", ex)
        End Try
    End Sub

    Private Function FilesMatch(firstPath As String, secondPath As String) As Boolean
        Try
            If Not File.Exists(firstPath) OrElse Not File.Exists(secondPath) Then Return False

            Dim firstInfo As New FileInfo(firstPath)
            Dim secondInfo As New FileInfo(secondPath)
            If firstInfo.Length <> secondInfo.Length Then Return False

            Using sha = SHA256.Create()
                Using firstStream = File.OpenRead(firstPath)
                    Using secondStream = File.OpenRead(secondPath)
                        Dim firstHash = sha.ComputeHash(firstStream)
                        sha.Initialize()
                        Dim secondHash = sha.ComputeHash(secondStream)
                        Return firstHash.SequenceEqual(secondHash)
                    End Using
                End Using
            End Using
        Catch
            Return False
        End Try
    End Function

    Private Function ResolveCurrentVersion(sharedRoot As String) As String
        Dim currentVersionFile = Path.Combine(sharedRoot, CurrentVersionFileName)

        Try
            If File.Exists(currentVersionFile) Then
                Dim value = File.ReadAllText(currentVersionFile, Encoding.UTF8).Trim()
                If value <> "" Then Return SafePathSegment(value)
            End If
        Catch
        End Try

        Dim versionsRoot = Path.Combine(sharedRoot, "Versions")
        If Not Directory.Exists(versionsRoot) Then Return ""

        Return Directory.GetDirectories(versionsRoot).
            Select(Function(path) New DirectoryInfo(path)).
            OrderByDescending(Function(info) info.Name).
            Select(Function(info) info.Name).
            FirstOrDefault()
    End Function

    Private Function TryBootstrapSharedRootFromPayload(sharedRoot As String) As String
        Try
            Dim appExe = Path.Combine(sharedRoot, AppExeName)
            Dim manifestPath = Path.Combine(sharedRoot, ManifestFileName)

            If Not File.Exists(appExe) OrElse Not File.Exists(manifestPath) Then Return ""

            Dim manifestText = File.ReadAllText(manifestPath, Encoding.UTF8)
            Dim version = SafePathSegment(ManifestValue(manifestText, "BuildStamp"))
            If version = "" Then
                version = DateTime.Now.ToString("yyyyMMdd_HHmmss")
            End If

            Dim versionsRoot = Path.Combine(sharedRoot, "Versions")
            Dim targetVersionDir = Path.Combine(versionsRoot, version)
            Dim stagingVersionDir = Path.Combine(versionsRoot, "_deploying_" & version & "_" & Process.GetCurrentProcess().Id.ToString() & "_" & Guid.NewGuid().ToString("N"))

            Directory.CreateDirectory(versionsRoot)

            If Not Directory.Exists(targetVersionDir) Then
                SafeDeleteDirectory(stagingVersionDir)

                CopyBootstrapPayload(sharedRoot, stagingVersionDir)
                WriteSharedRootMarker(stagingVersionDir, sharedRoot)
                Directory.Move(stagingVersionDir, targetVersionDir)
            End If

            File.WriteAllText(Path.Combine(sharedRoot, CurrentVersionFileName), version, New UTF8Encoding(False))
            Return version
        Catch ex As Exception
            Try
                File.AppendAllText(
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName, "LauncherBootstrap.log"),
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") & " | " & ex.Message & Environment.NewLine,
                    Encoding.UTF8)
            Catch
            End Try

            Return ""
        End Try
    End Function

    Private Function ManifestValue(manifestText As String, key As String) As String
        Dim prefix = If(key, "").Trim() & "="
        If prefix = "=" Then Return ""

        For Each rawLine In If(manifestText, "").Replace(vbCrLf, vbLf).Replace(vbCr, vbLf).Split({vbLf}, StringSplitOptions.None)
            Dim line = rawLine.Trim()
            If line.StartsWith(ChrW(&HFEFF)) Then line = line.Substring(1).Trim()
            If line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) Then
                Return line.Substring(prefix.Length).Trim()
            End If
        Next

        Return ""
    End Function

    Private Sub CopyBootstrapPayload(sourceRoot As String, destinationRoot As String)
        Directory.CreateDirectory(destinationRoot)

        Dim excludedNames As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {
            "Data",
            "Drawings",
            "Temp",
            "Backups",
            "Updates",
            "UpdatePackages",
            "Versions",
            CurrentVersionFileName
        }

        For Each item In Directory.GetFileSystemEntries(sourceRoot)
            Dim name = Path.GetFileName(item)
            If excludedNames.Contains(name) OrElse name.StartsWith("_deploying_", StringComparison.OrdinalIgnoreCase) Then Continue For

            Dim destination = Path.Combine(destinationRoot, name)
            If Directory.Exists(item) Then
                CopyDirectory(item, destination)
            Else
                File.Copy(item, destination, True)
            End If
        Next
    End Sub

    Private Sub EnsureLocalVersion(sourceDir As String, localVersionDir As String, sharedRoot As String)
        Dim localExe = Path.Combine(localVersionDir, AppExeName)
        Dim localManifest = Path.Combine(localVersionDir, ManifestFileName)

        If File.Exists(localExe) AndAlso File.Exists(localManifest) Then
            Try
                WriteSharedRootMarker(localVersionDir, sharedRoot)
                Return
            Catch ex As Exception
                LogLauncherIssue("Hazir yerel surum kullanilamadi; yeniden kopyalanacak.", ex)
            End Try
        End If

        Dim stagingRoots = ResolveWritableStagingRoots()
        For Each stagingRoot In stagingRoots
            CleanupOldStagingDirectories(stagingRoot)
        Next

        Dim lastError As Exception = Nothing
        Dim maxAttempts = Math.Max(6, stagingRoots.Count * 4)

        For attempt = 1 To maxAttempts
            If File.Exists(localExe) AndAlso File.Exists(localManifest) Then
                ExecuteWithRetry(
                    "Yerel surum marker dosyasi guncelleniyor.",
                    Sub() WriteSharedRootMarker(localVersionDir, sharedRoot))
                Return
            End If

            Dim rootIndex = Math.Min(stagingRoots.Count - 1, CInt(Math.Floor((attempt - 1) / 3.0R)))
            Dim stagingRoot = stagingRoots(rootIndex)
            Dim stagingDir As String = ""

            Try
                stagingDir = CreateUniqueStagingDirectory(stagingRoot, Path.GetFileName(localVersionDir))

                ExecuteWithRetry("Program staging klasorune kopyalaniyor.", Sub() CopyDirectory(sourceDir, stagingDir))
                ExecuteWithRetry("Staging shared root marker yaziliyor.", Sub() WriteSharedRootMarker(stagingDir, sharedRoot))

                If Not File.Exists(Path.Combine(stagingDir, AppExeName)) Then
                    Throw New FileNotFoundException("Staging program kopyasi olusturulamadi.", Path.Combine(stagingDir, AppExeName))
                End If

                If Not File.Exists(Path.Combine(stagingDir, ManifestFileName)) Then
                    Throw New FileNotFoundException("Staging manifest kopyasi olusturulamadi.", Path.Combine(stagingDir, ManifestFileName))
                End If

                ExecuteWithRetry(
                    "Yerel surum klasoru hazirlaniyor.",
                    Sub() Directory.CreateDirectory(Path.GetDirectoryName(localVersionDir)))
                SafeDeleteDirectory(localVersionDir)

                If Not Directory.Exists(localVersionDir) Then
                    ExecuteWithRetry("Staging klasoru yerel surume tasiniyor.", Sub() Directory.Move(stagingDir, localVersionDir))
                    stagingDir = ""
                Else
                    ExecuteWithRetry("Staging mevcut yerel surume kopyalaniyor.", Sub() CopyDirectory(stagingDir, localVersionDir))
                    ExecuteWithRetry("Yerel shared root marker yaziliyor.", Sub() WriteSharedRootMarker(localVersionDir, sharedRoot))
                End If

                If File.Exists(localExe) AndAlso File.Exists(localManifest) Then Return
                Throw New IOException("Yerel program kopyasi dogrulanamadi: " & localVersionDir)
            Catch ex As Exception
                lastError = ex
                LogLauncherIssue("Yerel surum hazirlama denemesi basarisiz. Deneme=" & attempt.ToString() & "; Staging=" & stagingDir, ex)
                If attempt < maxAttempts Then Thread.Sleep(Math.Min(1500, LaunchIoRetryDelayMs * attempt))
            Finally
                SafeDeleteDirectory(stagingDir)
            End Try
        Next

        If lastError IsNot Nothing Then
            Throw New IOException("Yerel program kopyasi hazirlanamadi. Windows dosya kilidi birkaç saniye içinde çözülmüyorsa programı tekrar açmayı deneyin.", lastError)
        End If

        Throw New IOException("Yerel program kopyasi hazirlanamadi.")
    End Sub

    Private Function ResolveWritableStagingRoots() As List(Of String)
        Dim localAppDataRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
        Dim candidates = {
            Path.Combine(localAppDataRoot, AppName, "Staging"),
            Path.Combine(Path.GetTempPath(), AppName, "Staging")
        }
        Dim writableRoots As New List(Of String)()

        For Each candidate In candidates
            Try
                Directory.CreateDirectory(candidate)

                Dim probeFile = Path.Combine(candidate, ".write_test_" & Guid.NewGuid().ToString("N"))
                File.WriteAllText(probeFile, "ok", New UTF8Encoding(False))
                File.Delete(probeFile)

                writableRoots.Add(candidate)
            Catch ex As Exception
                LogLauncherIssue("Staging kok klasoru kullanilamadi: " & candidate, ex)
            End Try
        Next

        If writableRoots.Count = 0 Then
            Throw New UnauthorizedAccessException("Yerel gecici klasor hazirlanamadi. Lutfen uygulamayi yeniden deneyin veya bilgisayardaki gecici klasor izinlerini kontrol edin.")
        End If

        Return writableRoots
    End Function

    Private Function CreateUniqueStagingDirectory(stagingRoot As String, versionName As String) As String
        Dim safeVersionName = SafePathSegment(versionName)
        If safeVersionName = "" Then safeVersionName = "version"

        For attempt = 1 To 10
            Dim stagingDir = Path.Combine(
                stagingRoot,
                safeVersionName & "_" & Process.GetCurrentProcess().Id.ToString() & "_" & Guid.NewGuid().ToString("N"))

            Try
                Directory.CreateDirectory(stagingDir)
                Return stagingDir
            Catch ex As Exception
                LogLauncherIssue("Staging klasoru olusturulamadi: " & stagingDir, ex)
            End Try
        Next

        Throw New IOException("Yerel staging klasoru olusturulamadi.")
    End Function

    Private Sub CleanupOldStagingDirectories(stagingRoot As String)
        Try
            If String.IsNullOrWhiteSpace(stagingRoot) OrElse Not Directory.Exists(stagingRoot) Then Return

            For Each stagingDir In Directory.GetDirectories(stagingRoot)
                Try
                    Dim info = New DirectoryInfo(stagingDir)
                    If info.LastWriteTime < DateTime.Now.AddDays(-2) Then
                        SafeDeleteDirectory(stagingDir)
                    End If
                Catch ex As Exception
                    LogLauncherIssue("Eski staging klasoru kontrol edilemedi: " & stagingDir, ex)
                End Try
            Next
        Catch ex As Exception
            LogLauncherIssue("Staging temizligi yapilamadi.", ex)
        End Try
    End Sub

    Private Sub SafeDeleteDirectory(directoryPath As String)
        If String.IsNullOrWhiteSpace(directoryPath) Then Return

        Try
            If Directory.Exists(directoryPath) Then
                Directory.Delete(directoryPath, True)
            End If
        Catch ex As Exception
            LogLauncherIssue("Klasor temizlenemedi: " & directoryPath, ex)
        End Try
    End Sub

    Private Sub LogLauncherIssue(context As String, ex As Exception)
        Try
            Dim logRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName)
            Directory.CreateDirectory(logRoot)

            File.AppendAllText(
                Path.Combine(logRoot, "Launcher.log"),
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") & " | " &
                If(context, "") & " | " & ex.GetType().Name & ": " & ex.Message & Environment.NewLine,
                Encoding.UTF8)
        Catch
        End Try
    End Sub

    Private Sub ExecuteWithRetry(context As String, action As Action)
        If action Is Nothing Then Throw New ArgumentNullException(NameOf(action))

        Dim lastError As Exception = Nothing
        For attempt = 1 To LaunchIoRetryCount
            Try
                action()
                Return
            Catch ex As IOException
                lastError = ex
                LogLauncherIssue(context & " Deneme=" & attempt.ToString(), ex)
            Catch ex As UnauthorizedAccessException
                lastError = ex
                LogLauncherIssue(context & " Deneme=" & attempt.ToString(), ex)
            End Try

            Thread.Sleep(Math.Min(2000, LaunchIoRetryDelayMs * attempt))
        Next

        If lastError Is Nothing Then
            Throw New IOException(context & " Islemi tamamlanamadi.")
        End If

        Throw New IOException(context & " Islemi tamamlanamadi.", lastError)
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

    Private Sub WriteSharedRootMarker(targetDir As String, sharedRoot As String)
        Directory.CreateDirectory(targetDir)
        File.WriteAllText(Path.Combine(targetDir, SharedRootConfigFileName), sharedRoot, New UTF8Encoding(False))
    End Sub

    Private Sub CleanupOldLocalVersions(currentVersionDir As String)
        Try
            Dim versionsRoot = Directory.GetParent(currentVersionDir)?.FullName
            If String.IsNullOrWhiteSpace(versionsRoot) OrElse Not Directory.Exists(versionsRoot) Then Return

            For Each versionPath In Directory.GetDirectories(versionsRoot)
                If String.Equals(Path.GetFullPath(versionPath).TrimEnd(Path.DirectorySeparatorChar),
                                 Path.GetFullPath(currentVersionDir).TrimEnd(Path.DirectorySeparatorChar),
                                 StringComparison.OrdinalIgnoreCase) Then
                    Continue For
                End If

                Try
                    Directory.Delete(versionPath, True)
                Catch
                End Try
            Next
        Catch
        End Try
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
End Module
