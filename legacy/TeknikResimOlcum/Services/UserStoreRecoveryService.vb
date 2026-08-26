Imports System.IO
Imports System.Linq
Imports System.Security.Cryptography
Imports System.Text

Public NotInheritable Class UserStoreRecoveryService
    Private Const MaxBackupCount As Integer = 30
    Private Const MarkerVersion As Integer = 2

    Private NotInheritable Class UserStoreMarkerState
        Public Property Version As Integer
        Public Property UserCount As Integer
        Public Property UserStoreSha256 As String = ""
    End Class

    Private Sub New()
    End Sub

    Public Shared Sub PrepareBeforeDataInitialization()
        Directory.CreateDirectory(AppPaths.DataDir)
        Directory.CreateDirectory(AppPaths.UserStoreBackupsDir)

        ExecuteWithUserStoreLocks(AddressOf PrepareBeforeDataInitializationCore)
    End Sub

    Private Shared Sub PrepareBeforeDataInitializationCore()
        Dim currentCount As Integer = ValidUserCount(AppPaths.UsersCsv)
        Dim establishedInstallation = File.Exists(AppPaths.UserStoreMarkerFile) OrElse HasBusinessData()
        Dim currentLooksLikeUnexpectedBootstrap = CurrentStoreLooksLikeBootstrapOnly()
        Dim marker = ReadMarkerState()
        Dim referenceBackup = FindMostCompleteBackup()
        Dim referenceBackupCount = ValidUserCount(referenceBackup)
        Dim expectedCount = marker.UserCount
        Dim expectedHash = marker.UserStoreSha256
        Dim unexpectedReduction =
            expectedCount > 0 AndAlso currentCount < expectedCount
        Dim legacyMarkerReduction =
            expectedCount <= 0 AndAlso
            establishedInstallation AndAlso
            referenceBackupCount > currentCount
        Dim suspiciousBootstrap =
            establishedInstallation AndAlso
            currentLooksLikeUnexpectedBootstrap AndAlso
            (expectedCount > currentCount OrElse referenceBackupCount > currentCount)

        If currentCount <= 0 OrElse unexpectedReduction OrElse legacyMarkerReduction OrElse suspiciousBootstrap Then
            Dim requiredCount = Math.Max(expectedCount, referenceBackupCount)
            Dim backupPath = FindRecoveryBackup(expectedHash, requiredCount)
            If backupPath = "" Then
                If establishedInstallation Then
                    Throw New InvalidOperationException(
                        "Kullanıcı dosyası kayıp, boş veya beklenmedik şekilde eksilmiş görünüyor." & Environment.NewLine &
                        "Güvenlik nedeniyle eksik kullanıcı dosyası sağlam kabul edilmedi ve yeni Admin hesabı oluşturulmadı." & Environment.NewLine &
                        "Beklenen kullanıcı sayısı: " & Math.Max(expectedCount, referenceBackupCount).ToString() & Environment.NewLine &
                        "Bulunan kullanıcı sayısı: " & currentCount.ToString() & Environment.NewLine &
                        "Kontrol edilen dosya: " & AppPaths.UsersCsv & Environment.NewLine &
                        "Programın doğru ortak klasörden açıldığını ve Data klasörünün erişilebilir olduğunu kontrol edin.")
                End If
                Return
            End If

            RestoreBackup(backupPath)
            currentCount = ValidUserCount(AppPaths.UsersCsv)
        End If

        If currentCount > 0 Then CreateBackupCore()
    End Sub

    Public Shared Sub CreateBackup()
        Try
            ExecuteWithUserStoreLocks(AddressOf CreateBackupCore)
        Catch ex As Exception
            ErrorLogService.Log("UserStoreRecoveryService.CreateBackup", ex)
        End Try
    End Sub

    Private Shared Sub ExecuteWithUserStoreLocks(action As Action)
        If action Is Nothing Then Throw New ArgumentNullException(NameOf(action))

        ' Ortak klasördeki bütün bilgisayarlar aynı kilit sırasını kullanır.
        ' Böylece kurtarma, Users.csv değişimi, yedek ve işaret güncellemesi
        ' birbirinin arasına giremez ve ters kilit sırası kaynaklı deadlock oluşmaz.
        CsvUtil.ExecuteWithExclusiveLock(
            AppPaths.UserStoreMarkerFile,
            Sub()
                CsvUtil.ExecuteWithExclusiveLock(
                    AppPaths.UsersCsv,
                    action)
            End Sub)
    End Sub

    Private Shared Sub CreateBackupCore()
        Dim userCount = ValidUserCount(AppPaths.UsersCsv)
        If userCount <= 0 Then Return

        Directory.CreateDirectory(AppPaths.UserStoreBackupsDir)
        Dim sourceHash = FileSha256(AppPaths.UsersCsv)
        Dim stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff")
        Dim safeMachine = FileNameUtil.SafeFileName(Environment.MachineName)
        Dim backupPath = Path.Combine(
            AppPaths.UserStoreBackupsDir,
            $"Users_{stamp}_{safeMachine}_{userCount}_{Guid.NewGuid().ToString("N").Substring(0, 8)}.csv")

        File.Copy(AppPaths.UsersCsv, backupPath, False)
        If Not String.Equals(FileSha256(backupPath), sourceHash, StringComparison.OrdinalIgnoreCase) Then
            Try
                File.Delete(backupPath)
            Catch
            End Try
            Throw New IOException("Kullanıcı yedeği doğrulanamadı: " & backupPath)
        End If

        WriteMarkerState(userCount, sourceHash)
        TrimOldBackups()
    End Sub

    Private Shared Function ValidUserCount(filePath As String) As Integer
        Try
            If String.IsNullOrWhiteSpace(filePath) OrElse Not File.Exists(filePath) Then Return 0

            Return CsvUtil.ReadRows(filePath).
                Where(Function(row)
                          Dim username = DataService.GetValue(row, "Username")
                          Dim hash = DataService.GetValue(row, "PasswordHash")
                          Dim salt = DataService.GetValue(row, "PasswordSalt")
                          Dim plain = DataService.GetValue(row, "PasswordPlain")
                          Return username.Trim() <> "" AndAlso
                                 ((hash.Trim() <> "" AndAlso salt.Trim() <> "") OrElse plain <> "")
                      End Function).
                Count()
        Catch ex As Exception
            ErrorLogService.Log("UserStoreRecoveryService.ValidUserCount", ex, "Path=" & If(filePath, ""))
            Return 0
        End Try
    End Function

    Private Shared Function CurrentStoreLooksLikeBootstrapOnly() As Boolean
        Try
            If ValidUserCount(AppPaths.UsersCsv) <> 1 Then Return False

            Dim rows = CsvUtil.ReadRows(AppPaths.UsersCsv)
            Dim admin = rows.FirstOrDefault(
                Function(row) String.Equals(DataService.GetValue(row, "Username"), "admin", StringComparison.OrdinalIgnoreCase))
            If admin Is Nothing Then Return False

            Return String.Equals(DataService.GetValue(admin, "MustChangePassword"), "YES", StringComparison.OrdinalIgnoreCase) AndAlso
                   String.IsNullOrWhiteSpace(DataService.GetValue(admin, "PasswordChangedAt"))
        Catch ex As Exception
            ErrorLogService.Log("UserStoreRecoveryService.CurrentStoreLooksLikeBootstrapOnly", ex)
            Return False
        End Try
    End Function

    Private Shared Function HasBusinessData() As Boolean
        Dim paths = {
            AppPaths.ProductsCsv,
            AppPaths.ControlPointsCsv,
            AppPaths.MeasurementGroupAreasCsv,
            AppPaths.MeasurementsCsv,
            AppPaths.MeasurementCorrectionsCsv,
            AppPaths.VisualControlsCsv,
            AppPaths.ProductionTicketsCsv,
            AppPaths.MoldBindingRecordsCsv,
            AppPaths.MoldConnectionPlanCsv,
            AppPaths.MechanismQualityControlRecordsCsv,
            AppPaths.PlasticShiftTrackingRecordsCsv,
            AppPaths.MechanismShiftTrackingRecordsCsv,
            AppPaths.PlasticShiftErrorReportsCsv,
            AppPaths.PlasticShiftErrorReportEvaluatorAssignmentsCsv,
            AppPaths.PlasticShiftErrorReportEvaluationsCsv,
            AppPaths.PlasticShiftErrorReportEmailEventsCsv,
            AppPaths.PlasticShiftEmailRecipientsCsv,
            AppPaths.MechanismShiftEmailRecipientsCsv,
            AppPaths.MechanismQualityEmailRecipientsCsv,
            AppPaths.TestRequestEmailRecipientsCsv,
            AppPaths.TestRequestEmailEventsCsv,
            AppPaths.TestRequestRecordsCsv,
            AppPaths.TestRequestStepsCsv,
            AppPaths.TestCatalogCsv,
            AppPaths.TestGroupsCsv,
            AppPaths.PackageMeterControlsCsv,
            AppPaths.PackageMeterControlLinesCsv,
            AppPaths.PackageMeterEmailRecipientsCsv
        }

        For Each path In paths
            Try
                If File.Exists(path) AndAlso CsvUtil.ReadRows(path).Count > 0 Then Return True
            Catch ex As Exception
                ErrorLogService.Log("UserStoreRecoveryService.HasBusinessData", ex, "Path=" & path)
            End Try
        Next

        Return False
    End Function

    Private Shared Function GetBackupCandidates() As List(Of String)
        Dim candidates As New List(Of String)()

        If Directory.Exists(AppPaths.UserStoreBackupsDir) Then
            candidates.AddRange(Directory.GetFiles(AppPaths.UserStoreBackupsDir, "Users_*.csv", SearchOption.TopDirectoryOnly))
        End If

        If Directory.Exists(AppPaths.BackupsDir) Then
            Try
                candidates.AddRange(Directory.GetFiles(AppPaths.BackupsDir, "Users.csv", SearchOption.AllDirectories))
            Catch ex As Exception
                ErrorLogService.Log("UserStoreRecoveryService.FindHistoricalBackups", ex)
            End Try
        End If

        Return candidates.
            Where(Function(path) ValidUserCount(path) > 0).
            Distinct(StringComparer.OrdinalIgnoreCase).
            ToList()
    End Function

    Private Shared Function FindMostCompleteBackup() As String
        Return GetBackupCandidates().
            OrderByDescending(Function(path) ValidUserCount(path)).
            ThenByDescending(Function(path) File.GetLastWriteTimeUtc(path)).
            FirstOrDefault()
    End Function

    Private Shared Function FindRecoveryBackup(expectedHash As String, minimumCount As Integer) As String
        Dim candidates = GetBackupCandidates()
        expectedHash = If(expectedHash, "").Trim()

        If expectedHash <> "" Then
            Dim exactMatch = candidates.
                Where(Function(path) String.Equals(FileSha256(path), expectedHash, StringComparison.OrdinalIgnoreCase)).
                OrderByDescending(Function(path) File.GetLastWriteTimeUtc(path)).
                FirstOrDefault()
            If exactMatch <> "" Then Return exactMatch
        End If

        Return candidates.
            Where(Function(path) ValidUserCount(path) >= Math.Max(1, minimumCount)).
            OrderByDescending(Function(path) ValidUserCount(path)).
            ThenByDescending(Function(path) File.GetLastWriteTimeUtc(path)).
            FirstOrDefault()
    End Function

    Private Shared Sub RestoreBackup(backupPath As String)
        Dim tempPath = AppPaths.UsersCsv & "." & Guid.NewGuid().ToString("N") & ".restore"
        Dim rollbackPath = AppPaths.UsersCsv & ".recovery.bak"
        Try
            File.Copy(backupPath, tempPath, True)
            If ValidUserCount(tempPath) <= 0 Then Throw New InvalidDataException("Kullanıcı yedeği geçersiz.")

            If File.Exists(AppPaths.UsersCsv) Then
                File.Replace(tempPath, AppPaths.UsersCsv, rollbackPath, True)
            Else
                File.Move(tempPath, AppPaths.UsersCsv)
            End If

            ErrorLogService.Log(
                "UserStoreRecoveryService.RestoreBackup",
                New InvalidOperationException("Kullanıcı dosyası yedekten otomatik kurtarıldı."),
                "Backup=" & backupPath & "; Target=" & AppPaths.UsersCsv)
        Finally
            Try
                If File.Exists(tempPath) Then File.Delete(tempPath)
            Catch cleanupEx As Exception
                ErrorLogService.Log("UserStoreRecoveryService.RestoreBackup.Cleanup", cleanupEx, "Path=" & tempPath)
            End Try
        End Try
    End Sub

    Private Shared Function ReadMarkerState() As UserStoreMarkerState
        Dim state As New UserStoreMarkerState()
        If Not File.Exists(AppPaths.UserStoreMarkerFile) Then Return state

        Try
            For Each line In File.ReadAllLines(AppPaths.UserStoreMarkerFile, Encoding.UTF8)
                Dim separatorIndex = line.IndexOf("="c)
                If separatorIndex <= 0 Then Continue For

                Dim key = line.Substring(0, separatorIndex).Trim()
                Dim value = line.Substring(separatorIndex + 1).Trim()
                Select Case key.ToUpperInvariant()
                    Case "VERSION"
                        Integer.TryParse(value, state.Version)
                    Case "USERCOUNT"
                        Integer.TryParse(value, state.UserCount)
                    Case "USERSTORESHA256"
                        state.UserStoreSha256 = value
                End Select
            Next
        Catch ex As Exception
            ErrorLogService.Log("UserStoreRecoveryService.ReadMarkerState", ex)
        End Try

        Return state
    End Function

    Private Shared Sub WriteMarkerState(userCount As Integer, userStoreSha256 As String)
        Dim markerText =
            "Version=" & MarkerVersion.ToString() & Environment.NewLine &
            "InitializedAt=" & DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") & Environment.NewLine &
            "DataDirectory=" & AppPaths.DataDir & Environment.NewLine &
            "UserCount=" & userCount.ToString() & Environment.NewLine &
            "UserStoreSha256=" & If(userStoreSha256, "") & Environment.NewLine
        Dim tempPath = AppPaths.UserStoreMarkerFile & "." & Guid.NewGuid().ToString("N") & ".tmp"

        Try
            File.WriteAllText(tempPath, markerText, New UTF8Encoding(False))
            If File.Exists(AppPaths.UserStoreMarkerFile) Then
                Try
                    File.Replace(tempPath, AppPaths.UserStoreMarkerFile, Nothing, True)
                Catch
                    File.Copy(tempPath, AppPaths.UserStoreMarkerFile, True)
                    File.Delete(tempPath)
                End Try
            Else
                File.Move(tempPath, AppPaths.UserStoreMarkerFile)
            End If
        Finally
            If File.Exists(tempPath) Then
                Try
                    File.Delete(tempPath)
                Catch
                End Try
            End If
        End Try
    End Sub

    Private Shared Function FileSha256(filePath As String) As String
        If String.IsNullOrWhiteSpace(filePath) OrElse Not File.Exists(filePath) Then Return ""

        Using input = New FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite Or FileShare.Delete)
            Using sha = SHA256.Create()
                Return Convert.ToHexString(sha.ComputeHash(input))
            End Using
        End Using
    End Function

    Private Shared Sub TrimOldBackups()
        Dim backups = Directory.GetFiles(AppPaths.UserStoreBackupsDir, "Users_*.csv", SearchOption.TopDirectoryOnly).
            OrderByDescending(Function(path) File.GetLastWriteTimeUtc(path)).
            ToList()

        For Each oldPath In backups.Skip(MaxBackupCount)
            Try
                File.Delete(oldPath)
            Catch ex As Exception
                ErrorLogService.Log("UserStoreRecoveryService.TrimOldBackups", ex, "Path=" & oldPath)
            End Try
        Next
    End Sub
End Class
