Imports System.IO
Imports System.Linq
Imports System.Text
Imports System.Diagnostics

Public NotInheritable Class CsvUtil
    Private Sub New()
    End Sub

    Private NotInheritable Class MutationLockHandle
        Implements IDisposable

        Private ReadOnly lockStream As FileStream
        Private ReadOnly lockPath As String
        Private disposed As Boolean = False

        Public Sub New(stream As FileStream, path As String)
            lockStream = stream
            lockPath = path
        End Sub

        Public Sub Dispose() Implements IDisposable.Dispose
            If disposed Then Return
            disposed = True

            Try
                lockStream.Dispose()
            Catch
            End Try

            Try
                If File.Exists(lockPath) Then File.Delete(lockPath)
            Catch
                ' Başka bir bilgisayar kilidi hemen aldıysa veya ağ paylaşımı silmeye izin vermediyse
                ' bu dosya bir sonraki kilit bırakmada tekrar temizlenmeye çalışılır.
            End Try
        End Sub
    End Class

    Private NotInheritable Class CsvRecoveryCandidate
        Public Property FilePath As String = ""
        Public Property IsValid As Boolean = False
        Public Property DataRowCount As Integer = -1
        Public Property Length As Long = 0
        Public Property LastWriteUtc As DateTime = DateTime.MinValue
    End Class

    Private NotInheritable Class CsvReadCacheEntry
        Public Property CachedUtc As DateTime
        Public Property LastWriteUtc As DateTime
        Public Property Length As Long
        Public Property Rows As List(Of Dictionary(Of String, String))
    End Class

    Private Const RetryCount As Integer = 30
    Private Const RetryDelayMs As Integer = 100
    Private Const MutationLockRetryCount As Integer = 100
    Private Const RecoveryBackupKeepCount As Integer = 30
    Private Const MinimumTempRecoveryAgeSeconds As Integer = 10
    Private Const AutoRecoveryMinimumRowsForLargeDrop As Integer = 25
    Private Const AutoRecoveryLargeDropRatio As Double = 0.25R
    Private Const AutoRecoveryLargeByteDropRatio As Double = 0.6R
    Private Const IntentionalEmptyMarkerSuffix As String = ".intentional-empty"
    Private Const SlowReadThresholdMs As Long = 500
    Private Const SlowWriteThresholdMs As Long = 750
    Private Const PerformanceLogMaxBytes As Long = 2L * 1024L * 1024L
    Private Shared ReadOnly ReadCacheLifetime As TimeSpan = TimeSpan.FromSeconds(3)
    Private Shared ReadOnly readCacheLock As New Object()
    Private Shared ReadOnly performanceLogLock As New Object()
    Private Shared ReadOnly readCache As New Dictionary(Of String, CsvReadCacheEntry)(StringComparer.OrdinalIgnoreCase)

    Private Shared ReadOnly QuoteChar As Char = ChrW(34)
    Private Shared ReadOnly QuoteText As String = ChrW(34).ToString()
    Private Shared ReadOnly DoubleQuoteText As String = New String(QuoteChar, 2)

    Public Shared Sub EnsureFile(filePath As String, headers As String())
        Dim recoveryArtifactsFound = HasRecoveryArtifacts(filePath)
        If Not recoveryArtifactsFound AndAlso FileLooksReadyForHeaders(filePath, headers) Then Return

        Try
            Using mutationLock = AcquireMutationLock(filePath)
                TryRecoverResetCsvLocked(filePath, headers)
                EnsureFileCore(filePath, headers)
            End Using
        Catch ex As Exception When TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException
            ' Aynı anda başka bir bilgisayar dosyayı hazırlıyor olabilir. Kilit alınamadığında
            ' dosyanın bu arada hazır hale gelip gelmediğini tekrar kontrol et; hazırsa açılışı düşürme.
            If CanDeferEnsureAfterLockFailure(filePath, headers) Then
                ErrorLogService.Log("CsvUtil.EnsureFile.Deferred", ex, "Path=" & filePath)
                Return
            End If
            Throw
        End Try
    End Sub

    Private Shared Function CanDeferEnsureAfterLockFailure(filePath As String, headers As String()) As Boolean
        Try
            If FileLooksReadyForHeaders(filePath, headers) Then Return True
            If HasRecoveryArtifacts(filePath) Then Return True
            If Not String.IsNullOrWhiteSpace(filePath) AndAlso File.Exists(filePath) Then Return True
            If Not String.IsNullOrWhiteSpace(filePath) AndAlso File.Exists(GetMutationLockPath(filePath)) Then Return True
        Catch ex As Exception
            ErrorLogService.Log("CsvUtil.CanDeferEnsureAfterLockFailure", ex, "Path=" & filePath)
        End Try

        Return False
    End Function

    Private Shared Function FileLooksReadyForHeaders(filePath As String, headers As String()) As Boolean
        If String.IsNullOrWhiteSpace(filePath) OrElse headers Is Nothing OrElse headers.Length = 0 Then Return False
        If Not File.Exists(filePath) Then Return False

        Try
            Dim firstLine = ReadFirstLineWithRetry(filePath)
            If String.IsNullOrWhiteSpace(firstLine) Then Return False

            Dim currentHeaders = ParseLine(firstLine)
            If currentHeaders.Count = 0 Then Return False

            Return headers.All(Function(h) currentHeaders.Any(Function(x) String.Equals(x, h, StringComparison.OrdinalIgnoreCase)))
        Catch ex As Exception When TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException
            ErrorLogService.Log("CsvUtil.FileLooksReadyForHeaders", ex, "Path=" & filePath)
            Return False
        End Try
    End Function

    Private Shared Function HasRecoveryArtifacts(filePath As String) As Boolean
        Try
            If String.IsNullOrWhiteSpace(filePath) Then Return False
            If File.Exists(filePath & ".bak") Then Return True

            Dim dirName = Path.GetDirectoryName(filePath)
            If String.IsNullOrWhiteSpace(dirName) OrElse Not Directory.Exists(dirName) Then Return False

            Dim fileName = Path.GetFileName(filePath)
            Return Directory.EnumerateFiles(dirName, fileName & ".*.tmp", SearchOption.TopDirectoryOnly).Any()
        Catch ex As Exception
            ErrorLogService.Log("CsvUtil.HasRecoveryArtifacts", ex, "Path=" & filePath)
            Return False
        End Try
    End Function

    Private Shared Sub TryRecoverResetCsvLocked(filePath As String, headers As String())
        Try
            Dim current = InspectCsvCandidate(filePath, headers)
            If current.IsValid AndAlso current.DataRowCount > 0 Then Return
            If current.IsValid AndAlso
               current.DataRowCount = 0 AndAlso
               File.Exists(GetIntentionalEmptyMarkerPath(filePath)) Then
                Return
            End If

            Dim recoveryCandidates = GetRecoveryCandidates(filePath, headers).
                Where(Function(candidate) candidate.IsValid AndAlso candidate.DataRowCount > Math.Max(0, current.DataRowCount)).
                OrderByDescending(Function(candidate) candidate.DataRowCount).
                ThenByDescending(Function(candidate) candidate.LastWriteUtc).
                ToList()

            If recoveryCandidates.Count = 0 Then Return

            Dim best = recoveryCandidates(0)
            PreserveDamagedCsvBeforeRecovery(filePath)
            File.Copy(best.FilePath, filePath, True)

            ErrorLogService.Log(
                "CsvUtil.TryRecoverResetCsvLocked",
                New IOException("CSV dosyası sıfırlanmış veya geçersiz göründüğü için yedekten/tmp dosyasından kurtarıldı."),
                "Target=" & filePath & "; Source=" & best.FilePath & "; Rows=" & best.DataRowCount.ToString())
        Catch ex As Exception
            ErrorLogService.Log("CsvUtil.TryRecoverResetCsvLocked", ex, "Path=" & filePath)
        End Try
    End Sub

    Private Shared Function GetRecoveryCandidates(filePath As String, headers As String()) As List(Of CsvRecoveryCandidate)
        Dim candidates As New List(Of CsvRecoveryCandidate)()

        Try
            Dim backupPath = filePath & ".bak"
            If File.Exists(backupPath) Then
                candidates.Add(InspectCsvCandidate(backupPath, headers))
            End If

            Dim dirName = Path.GetDirectoryName(filePath)
            If String.IsNullOrWhiteSpace(dirName) OrElse Not Directory.Exists(dirName) Then Return candidates

            Dim fileName = Path.GetFileName(filePath)
            For Each tempPath In Directory.EnumerateFiles(dirName, fileName & ".*.tmp", SearchOption.TopDirectoryOnly)
                If Not IsOldEnoughForRecovery(tempPath) Then Continue For
                candidates.Add(InspectCsvCandidate(tempPath, headers))
            Next

            Dim recoveryBackupDir = Path.Combine(AppPaths.BackupsDir, "CsvRecovery", SafeBackupName(fileName))
            If Directory.Exists(recoveryBackupDir) Then
                For Each backupCandidate In Directory.EnumerateFiles(recoveryBackupDir, "*", SearchOption.TopDirectoryOnly)
                    candidates.Add(InspectCsvCandidate(backupCandidate, headers))
                Next
            End If
        Catch ex As Exception
            ErrorLogService.Log("CsvUtil.GetRecoveryCandidates", ex, "Path=" & filePath)
        End Try

        Return candidates
    End Function

    Private Shared Function GetGenericRecoveryCandidates(filePath As String) As List(Of CsvRecoveryCandidate)
        Dim candidates As New List(Of CsvRecoveryCandidate)()

        Try
            Dim backupPath = filePath & ".bak"
            If File.Exists(backupPath) Then
                candidates.Add(InspectGenericCsvCandidate(backupPath))
            End If

            Dim dirName = Path.GetDirectoryName(filePath)
            If String.IsNullOrWhiteSpace(dirName) OrElse Not Directory.Exists(dirName) Then Return candidates

            Dim fileName = Path.GetFileName(filePath)
            For Each tempPath In Directory.EnumerateFiles(dirName, fileName & ".*.tmp", SearchOption.TopDirectoryOnly)
                If Not IsOldEnoughForRecovery(tempPath) Then Continue For
                candidates.Add(InspectGenericCsvCandidate(tempPath))
            Next

            Dim recoveryBackupDir = Path.Combine(AppPaths.BackupsDir, "CsvRecovery", SafeBackupName(fileName))
            If Directory.Exists(recoveryBackupDir) Then
                For Each backupCandidate In Directory.EnumerateFiles(recoveryBackupDir, "*", SearchOption.TopDirectoryOnly)
                    candidates.Add(InspectGenericCsvCandidate(backupCandidate))
                Next
            End If
        Catch ex As Exception
            ErrorLogService.Log("CsvUtil.GetGenericRecoveryCandidates", ex, "Path=" & filePath)
        End Try

        Return candidates
    End Function

    Private Shared Function InspectCsvCandidate(filePath As String, headers As String()) As CsvRecoveryCandidate
        Dim info As New CsvRecoveryCandidate With {.FilePath = If(filePath, "")}

        Try
            If String.IsNullOrWhiteSpace(filePath) OrElse Not File.Exists(filePath) Then Return info

            Dim fileInfo As New FileInfo(filePath)
            info.Length = fileInfo.Length
            info.LastWriteUtc = fileInfo.LastWriteTimeUtc
            If fileInfo.Length <= 0 Then Return info

            Dim text = ReadAllTextWithRetry(filePath)
            If String.IsNullOrWhiteSpace(text) Then Return info

            Dim records = ParseRecords(text)
            If records.Count = 0 OrElse records(0).Count = 0 Then Return info

            Dim currentHeaders = records(0)
            Dim hasAllHeaders = headers.All(Function(h) currentHeaders.Any(Function(x) String.Equals(x, h, StringComparison.OrdinalIgnoreCase)))
            If Not hasAllHeaders Then Return info

            info.IsValid = True
            info.DataRowCount = Math.Max(0, records.Count - 1)
        Catch ex As Exception When TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException
            ErrorLogService.Log("CsvUtil.InspectCsvCandidate", ex, "Path=" & filePath)
        End Try

        Return info
    End Function

    Private Shared Function InspectGenericCsvCandidate(filePath As String) As CsvRecoveryCandidate
        Dim info As New CsvRecoveryCandidate With {.FilePath = If(filePath, "")}

        Try
            If String.IsNullOrWhiteSpace(filePath) OrElse Not File.Exists(filePath) Then Return info

            Dim fileInfo As New FileInfo(filePath)
            info.Length = fileInfo.Length
            info.LastWriteUtc = fileInfo.LastWriteTimeUtc
            If fileInfo.Length <= 0 Then Return info

            Dim text = ReadAllTextWithRetry(filePath)
            If String.IsNullOrWhiteSpace(text) Then Return info

            Dim records = ParseRecords(text)
            If records.Count = 0 OrElse records(0).Count = 0 Then Return info

            info.IsValid = True
            info.DataRowCount = Math.Max(0, records.Count - 1)
        Catch ex As Exception When TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException
            ErrorLogService.Log("CsvUtil.InspectGenericCsvCandidate", ex, "Path=" & filePath)
        End Try

        Return info
    End Function

    Private Shared Function IsOldEnoughForRecovery(filePath As String) As Boolean
        Try
            Dim age = DateTime.UtcNow - File.GetLastWriteTimeUtc(filePath)
            Return age.TotalSeconds >= MinimumTempRecoveryAgeSeconds
        Catch
            Return False
        End Try
    End Function

    Private Shared Sub PreserveDamagedCsvBeforeRecovery(filePath As String)
        Try
            If String.IsNullOrWhiteSpace(filePath) OrElse Not File.Exists(filePath) Then Return

            Dim damagedPath = filePath & ".damaged_" & DateTime.Now.ToString("yyyyMMdd_HHmmss_fff")
            File.Copy(filePath, damagedPath, True)
        Catch ex As Exception
            ErrorLogService.Log("CsvUtil.PreserveDamagedCsvBeforeRecovery", ex, "Path=" & filePath)
        End Try
    End Sub

    Private Shared Sub EnsureFileCore(filePath As String, headers As String())
        TryRecoverResetCsvLocked(filePath, headers)

        If Not File.Exists(filePath) Then
            WriteTextAtomicLocked(filePath, ToCsvLine(headers) & Environment.NewLine, headers, 0)
            Return
        End If

        Dim text = ReadAllTextWithRetry(filePath)
        If String.IsNullOrWhiteSpace(text) Then
            WriteTextAtomicLocked(filePath, ToCsvLine(headers) & Environment.NewLine, headers, 0)
            Return
        End If

        Dim firstLine = text.Replace(vbCrLf, vbLf).Replace(vbCr, vbLf).Split({vbLf}, StringSplitOptions.None)(0)
        Dim currentHeaders = ParseLine(firstLine)
        Dim missingHeader As Boolean = False
        For Each h In headers
            If Not currentHeaders.Any(Function(x) String.Equals(x, h, StringComparison.OrdinalIgnoreCase)) Then
                missingHeader = True
                Exit For
            End If
        Next

        If Not missingHeader Then Return

        Dim rows = ReadRowsCore(filePath)
        WriteRowsAtomic(filePath, headers, rows)
    End Sub

    Public Shared Function ReadRows(filePath As String) As List(Of Dictionary(Of String, String))
        Dim cacheKey = NormalizeCachePath(filePath)
        Dim nowUtc = DateTime.UtcNow
        Dim cached As CsvReadCacheEntry = Nothing

        SyncLock readCacheLock
            If readCache.TryGetValue(cacheKey, cached) AndAlso
               nowUtc - cached.CachedUtc <= ReadCacheLifetime Then
                Return CloneRows(cached.Rows)
            End If
        End SyncLock

        Dim lastWriteUtc As DateTime
        Dim length As Long
        If cached IsNot Nothing AndAlso
           TryGetFileStamp(filePath, lastWriteUtc, length) AndAlso
           lastWriteUtc = cached.LastWriteUtc AndAlso
           length = cached.Length Then
            SyncLock readCacheLock
                cached.CachedUtc = nowUtc
            End SyncLock
            Return CloneRows(cached.Rows)
        End If

        Dim beforeLastWriteUtc As DateTime
        Dim beforeLength As Long
        Dim hasBeforeStamp = TryGetFileStamp(filePath, beforeLastWriteUtc, beforeLength)
        Dim operationTimer As System.Diagnostics.Stopwatch = System.Diagnostics.Stopwatch.StartNew()
        Dim rows = ReadRowsCore(filePath)
        operationTimer.Stop()
        LogSlowCsvOperation("CSV_READ", filePath, operationTimer.ElapsedMilliseconds, rows.Count, beforeLength, SlowReadThresholdMs)

        Dim afterLastWriteUtc As DateTime
        Dim afterLength As Long
        Dim hasAfterStamp = TryGetFileStamp(filePath, afterLastWriteUtc, afterLength)
        If hasBeforeStamp AndAlso hasAfterStamp AndAlso
           beforeLastWriteUtc = afterLastWriteUtc AndAlso
           beforeLength = afterLength Then
            Dim entry As New CsvReadCacheEntry With {
                .CachedUtc = DateTime.UtcNow,
                .LastWriteUtc = afterLastWriteUtc,
                .Length = afterLength,
                .Rows = CloneRows(rows)
            }
            SyncLock readCacheLock
                readCache(cacheKey) = entry
            End SyncLock
        Else
            InvalidateReadCache(filePath)
        End If

        Return rows
    End Function

    Public Shared Function ReadRowsLocked(filePath As String) As List(Of Dictionary(Of String, String))
        Using mutationLock = AcquireMutationLock(filePath)
            Return ReadRowsCore(filePath)
        End Using
    End Function

    Private Shared Function ReadRowsCore(filePath As String) As List(Of Dictionary(Of String, String))
        Dim result As New List(Of Dictionary(Of String, String))()
        If Not File.Exists(filePath) Then Return TryReadRowsFromRecoveryArtifacts(filePath, "missing")

        Dim text = ReadAllTextWithRetry(filePath)
        If String.IsNullOrWhiteSpace(text) Then Return TryReadRowsFromRecoveryArtifacts(filePath, "blank")

        Dim records = ParseRecords(text)
        If records.Count = 0 OrElse records(0).Count = 0 Then Return TryReadRowsFromRecoveryArtifacts(filePath, "invalid")

        Return BuildRowsFromRecords(records)
    End Function

    Private Shared Function BuildRowsFromRecords(records As List(Of List(Of String))) As List(Of Dictionary(Of String, String))
        Dim result As New List(Of Dictionary(Of String, String))()
        If records Is Nothing OrElse records.Count = 0 OrElse records(0).Count = 0 Then Return result

        Dim headers = records(0)
        For i As Integer = 1 To records.Count - 1
            If records(i).Count = 0 OrElse records(i).All(Function(v) String.IsNullOrWhiteSpace(v)) Then Continue For
            Dim values = records(i)
            Dim row As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
            For c As Integer = 0 To headers.Count - 1
                Dim value As String = If(c < values.Count, values(c), "")
                row(headers(c)) = value
            Next
            result.Add(row)
        Next
        Return result
    End Function

    Private Shared Function NormalizeCachePath(filePath As String) As String
        Try
            Return Path.GetFullPath(If(filePath, ""))
        Catch
            Return If(filePath, "")
        End Try
    End Function

    Private Shared Function TryGetFileStamp(filePath As String,
                                            ByRef lastWriteUtc As DateTime,
                                            ByRef length As Long) As Boolean
        lastWriteUtc = DateTime.MinValue
        length = 0
        Try
            If String.IsNullOrWhiteSpace(filePath) OrElse Not File.Exists(filePath) Then Return False
            Dim info As New FileInfo(filePath)
            info.Refresh()
            lastWriteUtc = info.LastWriteTimeUtc
            length = info.Length
            Return True
        Catch
            Return False
        End Try
    End Function

    Private Shared Sub InvalidateReadCache(filePath As String)
        Dim cacheKey = NormalizeCachePath(filePath)
        SyncLock readCacheLock
            readCache.Remove(cacheKey)
        End SyncLock
    End Sub

    Private Shared Sub LogSlowCsvOperation(operation As String,
                                           filePath As String,
                                           elapsedMs As Long,
                                           rowCount As Integer,
                                           byteCount As Long,
                                           thresholdMs As Long)
        If elapsedMs < thresholdMs Then Return

        Try
            SyncLock performanceLogLock
                Dim logDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "TeknikResimOlcum",
                    "Logs")
                Directory.CreateDirectory(logDir)

                Dim logPath = Path.Combine(logDir, "Performance.log")
                Dim previousPath = Path.Combine(logDir, "Performance.previous.log")
                If File.Exists(logPath) AndAlso New FileInfo(logPath).Length >= PerformanceLogMaxBytes Then
                    File.Move(logPath, previousPath, True)
                End If

                Dim line = String.Join(
                    " | ",
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                    If(operation, ""),
                    elapsedMs.ToString() & " ms",
                    "Rows=" & Math.Max(0, rowCount).ToString(),
                    "Bytes=" & Math.Max(0, byteCount).ToString(),
                    "File=" & Path.GetFileName(If(filePath, "")))
                File.AppendAllText(logPath, line & Environment.NewLine, New UTF8Encoding(False))
            End SyncLock
        Catch
            ' Performans günlüğü hiçbir koşulda ana işlemi engellememelidir.
        End Try
    End Sub

    Private Shared Function TryReadRowsFromRecoveryArtifacts(filePath As String, reason As String) As List(Of Dictionary(Of String, String))
        Try
            Dim best = GetGenericRecoveryCandidates(filePath).
                Where(Function(candidate) candidate.IsValid AndAlso candidate.DataRowCount >= 0).
                OrderByDescending(Function(candidate) candidate.DataRowCount).
                ThenByDescending(Function(candidate) candidate.LastWriteUtc).
                FirstOrDefault()

            If best Is Nothing Then Return New List(Of Dictionary(Of String, String))()

            Dim text = ReadAllTextWithRetry(best.FilePath)
            Dim records = ParseRecords(text)
            ErrorLogService.Log(
                "CsvUtil.TryReadRowsFromRecoveryArtifacts",
                New IOException("CSV ana dosyasi okunamadigi icin kurtarma adayindan okundu."),
                "Target=" & filePath & "; Source=" & best.FilePath & "; Reason=" & If(reason, "") & "; Rows=" & best.DataRowCount.ToString())

            Return BuildRowsFromRecords(records)
        Catch ex As Exception
            ErrorLogService.Log("CsvUtil.TryReadRowsFromRecoveryArtifacts", ex, "Path=" & filePath)
            Return New List(Of Dictionary(Of String, String))()
        End Try
    End Function

    Public Shared Sub AppendRowLocked(filePath As String, headers As String(), row As Dictionary(Of String, String))
        Using mutationLock = AcquireMutationLock(filePath)
            EnsureFileCore(filePath, headers)
            AppendRowCore(filePath, headers, row)
        End Using
    End Sub

    Public Shared Sub AppendRowFastLocked(filePath As String, headers As String(), row As Dictionary(Of String, String))
        Using mutationLock = AcquireMutationLock(filePath)
            EnsureFileForAppendCore(filePath, headers)
            AppendRowCore(filePath, headers, row)
        End Using
    End Sub

    Public Shared Sub AppendRowLocked(
        filePath As String,
        headers As String(),
        rowFactory As Func(Of List(Of Dictionary(Of String, String)), Dictionary(Of String, String)))

        If rowFactory Is Nothing Then Throw New ArgumentNullException(NameOf(rowFactory))

        Using mutationLock = AcquireMutationLock(filePath)
            EnsureFileCore(filePath, headers)
            Dim row = rowFactory(ReadRowsCore(filePath))
            If row Is Nothing Then Throw New InvalidOperationException("CSV'ye eklenecek satır oluşturulamadı.")
            AppendRowCore(filePath, headers, row)
        End Using
    End Sub

    Public Shared Sub UpdateRowsLocked(filePath As String,
                                       headers As String(),
                                       updateAction As Action(Of List(Of Dictionary(Of String, String))))
        If updateAction Is Nothing Then Throw New ArgumentNullException(NameOf(updateAction))

        Using mutationLock = AcquireMutationLock(filePath)
            EnsureFileCore(filePath, headers)
            Dim rows = ReadRowsCore(filePath)
            updateAction(rows)
            WriteRowsAtomic(filePath, headers, rows)
        End Using
    End Sub

    Public Shared Function UpdateRowsLocked(Of TResult)(
        filePath As String,
        headers As String(),
        updateFunction As Func(Of List(Of Dictionary(Of String, String)), TResult)) As TResult

        If updateFunction Is Nothing Then Throw New ArgumentNullException(NameOf(updateFunction))

        Using mutationLock = AcquireMutationLock(filePath)
            EnsureFileCore(filePath, headers)
            Dim rows = ReadRowsCore(filePath)
            Dim result = updateFunction(rows)
            WriteRowsAtomic(filePath, headers, rows)
            Return result
        End Using
    End Function

    Public Shared Function UpdateRowsLockedIfChanged(
        filePath As String,
        headers As String(),
        updateFunction As Func(Of List(Of Dictionary(Of String, String)), Boolean),
        Optional allowIntentionalRowReduction As Boolean = False) As Boolean

        If updateFunction Is Nothing Then Throw New ArgumentNullException(NameOf(updateFunction))

        Using mutationLock = AcquireMutationLock(filePath)
            EnsureFileCore(filePath, headers)
            Dim rows = ReadRowsCore(filePath)
            Dim changed = updateFunction(rows)
            If changed Then
                WriteRowsAtomic(
                    filePath,
                    headers,
                    rows,
                    allowIntentionalRowReduction:=allowIntentionalRowReduction)
            End If
            Return changed
        End Using
    End Function

    Public Shared Function DeleteRowsLocked(
        filePath As String,
        headers As String(),
        predicate As Predicate(Of Dictionary(Of String, String))) As Integer

        If predicate Is Nothing Then Throw New ArgumentNullException(NameOf(predicate))

        Using mutationLock = AcquireMutationLock(filePath)
            EnsureFileCore(filePath, headers)
            Dim rows = ReadRowsCore(filePath)
            Dim removedCount = rows.RemoveAll(predicate)
            If removedCount > 0 Then
                WriteRowsAtomic(filePath, headers, rows, allowIntentionalRowReduction:=True)
            End If
            Return removedCount
        End Using
    End Function

    Public Shared Sub UpdateTwoFilesLocked(
        firstFilePath As String,
        firstHeaders As String(),
        secondFilePath As String,
        secondHeaders As String(),
        updateAction As Action(Of List(Of Dictionary(Of String, String)),
                                  List(Of Dictionary(Of String, String))))

        If updateAction Is Nothing Then Throw New ArgumentNullException(NameOf(updateAction))

        Dim firstFullPath = Path.GetFullPath(firstFilePath)
        Dim secondFullPath = Path.GetFullPath(secondFilePath)
        If String.Equals(firstFullPath, secondFullPath, StringComparison.OrdinalIgnoreCase) Then
            Throw New ArgumentException("İki dosyalı işlem için farklı CSV dosyaları gereklidir.")
        End If

        Dim lockOrder = {firstFullPath, secondFullPath}.
            OrderBy(Function(path) path, StringComparer.OrdinalIgnoreCase).
            ToArray()

        Using firstLock = AcquireMutationLock(lockOrder(0))
            Using secondLock = AcquireMutationLock(lockOrder(1))
                EnsureFileCore(firstFullPath, firstHeaders)
                EnsureFileCore(secondFullPath, secondHeaders)

                Dim firstRows = ReadRowsCore(firstFullPath)
                Dim secondRows = ReadRowsCore(secondFullPath)
                Dim originalFirstRows = CloneRows(firstRows)
                Dim originalSecondRows = CloneRows(secondRows)

                updateAction(firstRows, secondRows)

                Dim firstWritten As Boolean = False
                Try
                    WriteRowsAtomic(firstFullPath, firstHeaders, firstRows)
                    firstWritten = True
                    WriteRowsAtomic(secondFullPath, secondHeaders, secondRows)
                Catch operationEx As Exception
                    If firstWritten Then
                        Try
                            WriteRowsAtomic(firstFullPath, firstHeaders, originalFirstRows)
                        Catch rollbackEx As Exception
                            ErrorLogService.Log(
                                "CsvUtil.UpdateTwoFilesLocked.RollbackFirst",
                                rollbackEx,
                                "OriginalError=" & operationEx.Message & "; Path=" & firstFullPath)
                        End Try
                    End If

                    Try
                        WriteRowsAtomic(secondFullPath, secondHeaders, originalSecondRows)
                    Catch rollbackEx As Exception
                        ErrorLogService.Log(
                            "CsvUtil.UpdateTwoFilesLocked.RollbackSecond",
                            rollbackEx,
                            "OriginalError=" & operationEx.Message & "; Path=" & secondFullPath)
                    End Try
                    Throw
                End Try
            End Using
        End Using
    End Sub

    Public Shared Sub ExecuteWithExclusiveLock(lockOwnerPath As String, action As Action)
        If action Is Nothing Then Throw New ArgumentNullException(NameOf(action))
        Using transactionLock = AcquireMutationLock(lockOwnerPath)
            action()
        End Using
    End Sub

    Private Shared Function CloneRows(rows As List(Of Dictionary(Of String, String))) As List(Of Dictionary(Of String, String))
        Return rows.Select(
            Function(row) New Dictionary(Of String, String)(row, StringComparer.OrdinalIgnoreCase)).
            ToList()
    End Function

    Private Shared Sub EnsureFileForAppendCore(filePath As String, headers As String())
        TryRecoverResetCsvLocked(filePath, headers)

        If Not File.Exists(filePath) Then
            WriteTextAtomicLocked(filePath, ToCsvLine(headers) & Environment.NewLine, headers, 0)
            Return
        End If

        Dim firstLine = ReadFirstLineWithRetry(filePath)
        If String.IsNullOrWhiteSpace(firstLine) Then
            WriteTextAtomicLocked(filePath, ToCsvLine(headers) & Environment.NewLine, headers, 0)
            Return
        End If

        Dim currentHeaders = ParseLine(firstLine)
        Dim missingHeader As Boolean = False
        For Each h In headers
            If Not currentHeaders.Any(Function(x) String.Equals(x, h, StringComparison.OrdinalIgnoreCase)) Then
                missingHeader = True
                Exit For
            End If
        Next

        If missingHeader Then EnsureFileCore(filePath, headers)
    End Sub

    Private Shared Sub WriteRowsAtomic(filePath As String,
                                       headers As String(),
                                       rows As List(Of Dictionary(Of String, String)),
                                       Optional allowIntentionalRowReduction As Boolean = False)
        Dim sb As New StringBuilder()
        sb.AppendLine(ToCsvLine(headers))
        For Each row In rows
            Dim vals As New List(Of String)()
            For Each h In headers
                vals.Add(If(row.ContainsKey(h), row(h), ""))
            Next
            sb.AppendLine(ToCsvLine(vals))
        Next

        WriteTextAtomicLocked(
            filePath,
            sb.ToString(),
            headers,
            rows.Count,
            allowIntentionalRowReduction)
    End Sub

    Private Shared Sub AppendRowCore(filePath As String,
                                     headers As String(),
                                     row As Dictionary(Of String, String))
        Dim operationTimer As System.Diagnostics.Stopwatch = System.Diagnostics.Stopwatch.StartNew()
        Dim beforeSnapshot = CriticalDataJournalService.CaptureSnapshot(filePath)
        Dim lineValues As New List(Of String)()
        For Each h In headers
            lineValues.Add(If(row.ContainsKey(h), row(h), ""))
        Next
        Dim line = ToCsvLine(lineValues) & Environment.NewLine
        Dim bytes = Encoding.UTF8.GetBytes(line)

        For attempt As Integer = 1 To RetryCount
            Try
                ' Okuyucular CSV'yi görüntülerken ekleme işlemini gereksiz yere engellemesin.
                ' Yazıcılar ayrıca .lock dosyasıyla tekilleştirildiği için burada yazma paylaşımı gerekmez.
                Using fs As New FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read Or FileShare.Delete)
                    fs.Write(bytes, 0, bytes.Length)
                    fs.Flush(True)
                End Using
                ClearIntentionalEmptyMarker(filePath)
                CriticalDataJournalService.LogCsvChange(filePath, "APPEND", beforeSnapshot, CriticalDataJournalService.CaptureSnapshot(filePath))
                InvalidateReadCache(filePath)
                operationTimer.Stop()
                LogSlowCsvOperation("CSV_APPEND", filePath, operationTimer.ElapsedMilliseconds, 1, bytes.Length, SlowWriteThresholdMs)
                Return
            Catch ex As IOException
                If attempt = RetryCount Then Throw
                Threading.Thread.Sleep(RetryDelayMs)
            Catch ex As UnauthorizedAccessException
                If attempt = RetryCount Then Throw
                Threading.Thread.Sleep(RetryDelayMs)
            End Try
        Next

        Throw New IOException("CSV dosyası kilitli olduğu için kayıt yapılamadı: " & filePath)
    End Sub

    Private Shared Function AcquireMutationLock(filePath As String) As IDisposable
        Dim dirName As String = Path.GetDirectoryName(filePath)
        If Not String.IsNullOrWhiteSpace(dirName) Then Directory.CreateDirectory(dirName)

        Dim lockPath = GetMutationLockPath(filePath)
        For attempt As Integer = 1 To MutationLockRetryCount
            Try
                Dim stream = New FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)
                Return New MutationLockHandle(stream, lockPath)
            Catch ex As IOException
                If attempt = MutationLockRetryCount Then Throw
                Threading.Thread.Sleep(RetryDelayMs)
            Catch ex As UnauthorizedAccessException
                If attempt = MutationLockRetryCount Then Throw
                Threading.Thread.Sleep(RetryDelayMs)
            End Try
        Next

        Throw New IOException("CSV güncelleme kilidi alınamadı: " & filePath)
    End Function

    Private Shared Function GetMutationLockPath(filePath As String) As String
        Return If(filePath, "") & ".lock"
    End Function

    Private Shared Function ReadFirstLineWithRetry(filePath As String) As String
        For attempt As Integer = 1 To RetryCount
            Try
                Using fs As New FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite Or FileShare.Delete)
                    Using sr As New StreamReader(fs, Encoding.UTF8, True)
                        Return If(sr.ReadLine(), "")
                    End Using
                End Using
            Catch ex As IOException
                If attempt = RetryCount Then Throw
                Threading.Thread.Sleep(RetryDelayMs)
            Catch ex As UnauthorizedAccessException
                If attempt = RetryCount Then Throw
                Threading.Thread.Sleep(RetryDelayMs)
            End Try
        Next

        Throw New IOException("CSV dosyası okunamadı: " & filePath)
    End Function

    Public Shared Function ToCsvLine(values As IEnumerable(Of String)) As String
        Dim parts As New List(Of String)()
        For Each v In values
            parts.Add(Escape(v))
        Next
        Return String.Join(",", parts)
    End Function

    Private Shared Function ReadAllTextWithRetry(filePath As String) As String
        For attempt As Integer = 1 To RetryCount
            Try
                ' FileShare.Delete atomik File.Replace işleminin aktif okuyucular varken de
                ' çalışabilmesini sağlar. Açık akış kendi dosya görünümünü okumaya devam eder.
                Using fs As New FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite Or FileShare.Delete)
                    Using sr As New StreamReader(fs, Encoding.UTF8, True)
                        Return sr.ReadToEnd()
                    End Using
                End Using
            Catch ex As IOException
                If attempt = RetryCount Then Throw
                Threading.Thread.Sleep(RetryDelayMs)
            Catch ex As UnauthorizedAccessException
                If attempt = RetryCount Then Throw
                Threading.Thread.Sleep(RetryDelayMs)
            End Try
        Next

        Throw New IOException("CSV dosyası okunamadı: " & filePath)
    End Function

    Private Shared Sub WriteTextLocked(filePath As String, text As String)
        WriteTextAtomicLocked(filePath, text)
    End Sub

    Private Shared Sub WriteTextAtomicLocked(filePath As String,
                                             text As String,
                                             Optional headers As String() = Nothing,
                                             Optional expectedDataRows As Integer = -1,
                                             Optional allowIntentionalRowReduction As Boolean = False)
        Dim operationTimer As System.Diagnostics.Stopwatch = System.Diagnostics.Stopwatch.StartNew()
        Dim dirName As String = Path.GetDirectoryName(filePath)
        If Not String.IsNullOrWhiteSpace(dirName) Then Directory.CreateDirectory(dirName)

        Dim beforeSnapshot = CriticalDataJournalService.CaptureSnapshot(filePath)
        Dim bytes = New UTF8Encoding(True).GetBytes(text)
        For attempt As Integer = 1 To RetryCount
            Dim tempPath = filePath & "." & Guid.NewGuid().ToString("N") & ".tmp"
            Dim backupPath = filePath & ".bak"
            Try
                Using fs As New FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None)
                    fs.Write(bytes, 0, bytes.Length)
                    fs.Flush(True)
                End Using

                VerifyTempFileBeforeReplace(tempPath, bytes.Length)

                If File.Exists(filePath) Then
                    If attempt = 1 Then CreateCsvRecoveryBackup(filePath)

                    Try
                        File.Replace(tempPath, filePath, backupPath, True)
                    Catch replaceEx As Exception
                        ErrorLogService.Log("CsvUtil.WriteTextAtomicLocked.FileReplaceFallback", replaceEx, "Path=" & filePath)

                        Try
                            If File.Exists(backupPath) Then File.Delete(backupPath)
                            File.Copy(filePath, backupPath, True)
                        Catch backupEx As Exception
                            ErrorLogService.Log("CsvUtil.WriteTextAtomicLocked.SidecarBackup", backupEx, "Path=" & backupPath)
                        End Try

                        Try
                            File.Move(tempPath, filePath, True)
                        Catch moveEx As Exception
                            ErrorLogService.Log("CsvUtil.WriteTextAtomicLocked.MoveFallback", moveEx, "Path=" & filePath)
                            Throw
                        End Try
                    End Try
                Else
                    File.Move(tempPath, filePath)
                End If

                VerifyWrittenFileLengthWithRetry(filePath, bytes.Length)
                Dim afterSnapshot = CriticalDataJournalService.CaptureSnapshot(filePath)
                Dim recoveryNote As String = ""
                Dim recoveredSnapshot As CsvFileSnapshot = Nothing
                If Not allowIntentionalRowReduction Then
                    recoveredSnapshot = TryAutoRecoverAfterSuspiciousWrite(
                        filePath,
                        headers,
                        beforeSnapshot,
                        afterSnapshot,
                        expectedDataRows,
                        recoveryNote)
                End If
                If recoveredSnapshot IsNot Nothing Then
                    CriticalDataJournalService.LogCsvChange(filePath, "AUTO_RECOVERY", afterSnapshot, recoveredSnapshot, recoveryNote)
                    afterSnapshot = recoveredSnapshot
                End If

                UpdateIntentionalEmptyMarker(filePath, expectedDataRows, allowIntentionalRowReduction)

                CriticalDataJournalService.LogCsvChange(filePath, "WRITE_ATOMIC", beforeSnapshot, afterSnapshot)
                InvalidateReadCache(filePath)
                operationTimer.Stop()
                LogSlowCsvOperation(
                    "CSV_WRITE",
                    filePath,
                    operationTimer.ElapsedMilliseconds,
                    Math.Max(0, expectedDataRows),
                    bytes.Length,
                    SlowWriteThresholdMs)
                Return
            Catch ex As IOException
                ErrorLogService.Log("CsvUtil.WriteRowsAtomic.IOException", ex, "Path=" & filePath & "; Temp=" & tempPath)
                If attempt = RetryCount Then Throw
                Threading.Thread.Sleep(RetryDelayMs)
            Catch ex As UnauthorizedAccessException
                ErrorLogService.Log("CsvUtil.WriteRowsAtomic.Unauthorized", ex, "Path=" & filePath & "; Temp=" & tempPath)
                If attempt = RetryCount Then Throw
                Threading.Thread.Sleep(RetryDelayMs)
            End Try
        Next

        Throw New IOException("CSV dosyası kilitli olduğu için atomik güncelleme yapılamadı: " & filePath)
    End Sub

    Private Shared Function GetIntentionalEmptyMarkerPath(filePath As String) As String
        Return If(filePath, "") & IntentionalEmptyMarkerSuffix
    End Function

    Private Shared Sub UpdateIntentionalEmptyMarker(filePath As String,
                                                    expectedDataRows As Integer,
                                                    allowIntentionalRowReduction As Boolean)
        Try
            If allowIntentionalRowReduction AndAlso expectedDataRows = 0 Then
                File.WriteAllText(
                    GetIntentionalEmptyMarkerPath(filePath),
                    DateTime.UtcNow.ToString("O"),
                    New UTF8Encoding(False))
            ElseIf expectedDataRows > 0 Then
                ClearIntentionalEmptyMarker(filePath)
            End If
        Catch ex As Exception
            ErrorLogService.Log("CsvUtil.UpdateIntentionalEmptyMarker", ex, "Path=" & filePath)
        End Try
    End Sub

    Private Shared Sub ClearIntentionalEmptyMarker(filePath As String)
        Try
            Dim markerPath = GetIntentionalEmptyMarkerPath(filePath)
            If File.Exists(markerPath) Then File.Delete(markerPath)
        Catch ex As Exception
            ErrorLogService.Log("CsvUtil.ClearIntentionalEmptyMarker", ex, "Path=" & filePath)
        End Try
    End Sub

    Private Shared Function TryAutoRecoverAfterSuspiciousWrite(filePath As String,
                                                               headers As String(),
                                                               beforeSnapshot As CsvFileSnapshot,
                                                               afterSnapshot As CsvFileSnapshot,
                                                               expectedDataRows As Integer,
                                                               ByRef recoveryNote As String) As CsvFileSnapshot
        recoveryNote = ""

        If ShouldSkipAutoRecovery(filePath) Then Return Nothing
        If headers Is Nothing OrElse headers.Length = 0 Then Return Nothing
        If beforeSnapshot Is Nothing OrElse Not beforeSnapshot.Exists Then Return Nothing

        Dim current = InspectCsvCandidate(filePath, headers)
        Dim reason = GetSuspiciousWriteReason(filePath, current, beforeSnapshot, afterSnapshot, expectedDataRows)
        If String.IsNullOrWhiteSpace(reason) Then Return Nothing

        Dim best = GetRecoveryCandidates(filePath, headers).
            Where(Function(candidate) candidate.IsValid AndAlso
                                      candidate.DataRowCount >= Math.Max(0, beforeSnapshot.RowCount)).
            OrderByDescending(Function(candidate) candidate.DataRowCount).
            ThenByDescending(Function(candidate) candidate.LastWriteUtc).
            FirstOrDefault()

        If best Is Nothing Then
            best = GetRecoveryCandidates(filePath, headers).
                Where(Function(candidate) candidate.IsValid AndAlso
                                          candidate.DataRowCount > Math.Max(0, current.DataRowCount)).
                OrderByDescending(Function(candidate) candidate.DataRowCount).
                ThenByDescending(Function(candidate) candidate.LastWriteUtc).
                FirstOrDefault()
        End If

        If best Is Nothing Then
            Dim noCandidateMessage = "CSV otomatik veri kaybi korumasi risk algiladi fakat uygun yedek bulunamadi."
            ErrorLogService.Log(
                "CsvUtil.TryAutoRecoverAfterSuspiciousWrite.NoCandidate",
                New IOException(noCandidateMessage),
                "Path=" & filePath & "; Reason=" & reason)
            Throw New IOException(noCandidateMessage & " Dosya: " & filePath & " / Sebep: " & reason)
        End If

        PreserveDamagedCsvBeforeRecovery(filePath)
        File.Copy(best.FilePath, filePath, True)
        VerifyWrittenFileLengthWithRetry(filePath, best.Length)

        recoveryNote = "Sebep=" & reason &
            "; Kaynak=" & best.FilePath &
            "; KurtarilanSatir=" & best.DataRowCount.ToString()

        ErrorLogService.Log(
            "CsvUtil.TryAutoRecoverAfterSuspiciousWrite",
            New IOException("CSV otomatik veri kaybi korumasi devreye girdi ve dosya yedekten kurtarildi."),
            "Target=" & filePath & "; " & recoveryNote)

        Return CriticalDataJournalService.CaptureSnapshot(filePath)
    End Function

    Private Shared Function GetSuspiciousWriteReason(filePath As String,
                                                     current As CsvRecoveryCandidate,
                                                     beforeSnapshot As CsvFileSnapshot,
                                                     afterSnapshot As CsvFileSnapshot,
                                                     expectedDataRows As Integer) As String
        If current Is Nothing OrElse Not current.IsValid Then
            Return "Yazma sonrasi CSV header/format dogrulanamadi"
        End If

        Dim beforeRows = Math.Max(0, beforeSnapshot.RowCount)
        Dim afterRows = Math.Max(0, current.DataRowCount)
        Dim beforeBytes = Math.Max(0, beforeSnapshot.Length)
        Dim afterBytes = If(afterSnapshot Is Nothing, current.Length, Math.Max(0, afterSnapshot.Length))

        If beforeRows > 0 AndAlso afterRows = 0 Then
            Return "Satir sayisi " & beforeRows.ToString() & " iken 0'a dustu"
        End If

        If beforeRows >= AutoRecoveryMinimumRowsForLargeDrop AndAlso
           afterRows < CInt(Math.Floor(beforeRows * AutoRecoveryLargeDropRatio)) AndAlso
           beforeBytes > 0 AndAlso
           afterBytes < CLng(Math.Floor(beforeBytes * AutoRecoveryLargeByteDropRatio)) Then
            Return "Satir ve dosya boyutu anormal kuculdu (" &
                beforeRows.ToString() & " -> " & afterRows.ToString() & " satir)"
        End If

        If expectedDataRows >= 0 AndAlso current.IsValid AndAlso current.DataRowCount <> expectedDataRows Then
            ErrorLogService.Log(
                "CsvUtil.GetSuspiciousWriteReason.RowCountMismatch",
                New IOException("CSV yazma sonrasi beklenen satir sayisi ile okunan satir sayisi farkli."),
                "Path=" & filePath &
                "; Expected=" & expectedDataRows.ToString() &
                "; Actual=" & current.DataRowCount.ToString())
        End If

        Return ""
    End Function

    Private Shared Function ShouldSkipAutoRecovery(filePath As String) As Boolean
        If String.IsNullOrWhiteSpace(filePath) Then Return True

        Dim fileName = Path.GetFileName(filePath)
        Dim volatileFiles = {
            "ActiveSessions.csv",
            "RunningInstances.csv",
            "SessionEndRequests.csv",
            "CriticalDataJournal.csv"
        }

        Return volatileFiles.Any(Function(name) String.Equals(name, fileName, StringComparison.OrdinalIgnoreCase))
    End Function

    Private Shared Sub VerifyTempFileBeforeReplace(tempPath As String, expectedLength As Long)
        Dim info As New FileInfo(tempPath)
        If Not info.Exists OrElse info.Length <> expectedLength Then
            Throw New IOException("CSV geÃ§ici dosya doÄŸrulamasÄ± baÅŸarÄ±sÄ±z: " & tempPath)
        End If
    End Sub

    Private Shared Sub VerifyWrittenFileLengthWithRetry(filePath As String, expectedLength As Long)
        For attempt As Integer = 1 To RetryCount
            Try
                If File.Exists(filePath) Then
                    Dim info As New FileInfo(filePath)
                    If info.Length = expectedLength Then Return
                End If
            Catch ex As IOException
                If attempt = RetryCount Then Throw
            Catch ex As UnauthorizedAccessException
                If attempt = RetryCount Then Throw
            End Try

            Threading.Thread.Sleep(RetryDelayMs)
        Next

        Throw New IOException("CSV yazma doÄŸrulamasÄ± baÅŸarÄ±sÄ±z: " & filePath)
    End Sub

    Private Shared Sub CreateCsvRecoveryBackup(filePath As String)
        Try
            If String.IsNullOrWhiteSpace(filePath) OrElse Not File.Exists(filePath) Then Return

            Dim info As New FileInfo(filePath)
            If info.Length <= 0 Then Return

            Dim bucketName = SafeBackupName(Path.GetFileName(filePath))
            Dim backupDir = Path.Combine(AppPaths.BackupsDir, "CsvRecovery", bucketName)
            Directory.CreateDirectory(backupDir)

            Dim backupPath = Path.Combine(
                backupDir,
                DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") & "_" & bucketName)

            File.Copy(filePath, backupPath, False)
            TrimCsvRecoveryBackups(backupDir)
        Catch ex As Exception
            ErrorLogService.Log("CsvUtil.CreateCsvRecoveryBackup", ex, "Path=" & filePath)
        End Try
    End Sub

    Private Shared Sub TrimCsvRecoveryBackups(backupDir As String)
        Try
            If String.IsNullOrWhiteSpace(backupDir) OrElse Not Directory.Exists(backupDir) Then Return

            Dim backups = Directory.GetFiles(backupDir, "*", SearchOption.TopDirectoryOnly).
                OrderByDescending(Function(path) File.GetLastWriteTimeUtc(path)).
                ToList()

            For Each oldBackup In backups.Skip(RecoveryBackupKeepCount)
                Try
                    File.Delete(oldBackup)
                Catch ex As Exception
                    ErrorLogService.Log("CsvUtil.TrimCsvRecoveryBackups", ex, "Path=" & oldBackup)
                End Try
            Next
        Catch ex As Exception
            ErrorLogService.Log("CsvUtil.TrimCsvRecoveryBackups", ex, "Dir=" & backupDir)
        End Try
    End Sub

    Private Shared Function SafeBackupName(value As String) As String
        Dim safe = If(value, "").Trim()
        If safe = "" Then safe = "csv"

        For Each invalidChar In Path.GetInvalidFileNameChars()
            safe = safe.Replace(invalidChar, "_"c)
        Next

        Return safe
    End Function

    Private Shared Function Escape(value As String) As String
        If value Is Nothing Then value = ""

        Dim mustQuote As Boolean = value.Contains(",") OrElse
                                   value.Contains(QuoteText) OrElse
                                   value.Contains(vbCr) OrElse
                                   value.Contains(vbLf)

        value = value.Replace(QuoteText, DoubleQuoteText)

        If mustQuote Then
            Return QuoteText & value & QuoteText
        End If

        Return value
    End Function

    Public Shared Function ParseLine(line As String) As List(Of String)
        Dim result As New List(Of String)()
        Dim sb As New StringBuilder()
        Dim inQuotes As Boolean = False
        Dim i As Integer = 0

        While i < line.Length
            Dim ch As Char = line(i)

            If inQuotes Then
                If ch = QuoteChar Then
                    If i + 1 < line.Length AndAlso line(i + 1) = QuoteChar Then
                        sb.Append(QuoteChar)
                        i += 1
                    Else
                        inQuotes = False
                    End If
                Else
                    sb.Append(ch)
                End If
            Else
                If ch = ","c Then
                    result.Add(sb.ToString())
                    sb.Clear()
                ElseIf ch = QuoteChar Then
                    inQuotes = True
                Else
                    sb.Append(ch)
                End If
            End If

            i += 1
        End While

        result.Add(sb.ToString())
        Return result
    End Function

    Private Shared Function ParseRecords(text As String) As List(Of List(Of String))
        Dim records As New List(Of List(Of String))()
        Dim currentRecord As New List(Of String)()
        Dim currentField As New StringBuilder()
        Dim inQuotes As Boolean = False
        Dim i As Integer = 0

        While i < text.Length
            Dim ch As Char = text(i)

            If inQuotes Then
                If ch = QuoteChar Then
                    If i + 1 < text.Length AndAlso text(i + 1) = QuoteChar Then
                        currentField.Append(QuoteChar)
                        i += 1
                    Else
                        inQuotes = False
                    End If
                Else
                    currentField.Append(ch)
                End If
            Else
                If ch = QuoteChar Then
                    inQuotes = True
                ElseIf ch = ","c Then
                    currentRecord.Add(currentField.ToString())
                    currentField.Clear()
                ElseIf ch = vbCr Then
                    currentRecord.Add(currentField.ToString())
                    currentField.Clear()
                    records.Add(currentRecord)
                    currentRecord = New List(Of String)()
                    If i + 1 < text.Length AndAlso text(i + 1) = vbLf Then i += 1
                ElseIf ch = vbLf Then
                    currentRecord.Add(currentField.ToString())
                    currentField.Clear()
                    records.Add(currentRecord)
                    currentRecord = New List(Of String)()
                Else
                    currentField.Append(ch)
                End If
            End If

            i += 1
        End While

        If currentField.Length > 0 OrElse currentRecord.Count > 0 Then
            currentRecord.Add(currentField.ToString())
            records.Add(currentRecord)
        End If

        Return records
    End Function
End Class
