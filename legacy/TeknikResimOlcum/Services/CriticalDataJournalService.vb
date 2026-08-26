Imports System.Diagnostics
Imports System.Globalization
Imports System.IO
Imports System.Linq
Imports System.Reflection
Imports System.Security.Cryptography
Imports System.Text

Public NotInheritable Class CriticalDataJournalService
    Private Sub New()
    End Sub

    Public Shared ReadOnly Headers As String() = {
        "EntryId",
        "EventAt",
        "FileName",
        "FilePath",
        "Operation",
        "BeforeRows",
        "AfterRows",
        "RowDelta",
        "BeforeBytes",
        "AfterBytes",
        "ByteDelta",
        "BeforeHash",
        "AfterHash",
        "UserName",
        "Role",
        "WindowsUser",
        "ComputerName",
        "ProcessId",
        "Version",
        "Note"
    }

    Private Const LockRetryCount As Integer = 40
    Private Const LockRetryDelayMs As Integer = 50

    Public Shared Function CaptureSnapshot(filePath As String) As CsvFileSnapshot
        Dim snapshot As New CsvFileSnapshot()
        If String.IsNullOrWhiteSpace(filePath) Then Return snapshot

        Try
            If Not File.Exists(filePath) Then Return snapshot

            Dim info As New FileInfo(filePath)
            snapshot.Exists = True
            snapshot.Length = info.Length
            snapshot.LastWriteUtc = info.LastWriteTimeUtc

            Dim bytes = ReadAllBytesShared(filePath)
            snapshot.Hash = ComputeHash(bytes)
            snapshot.RowCount = CountCsvDataRows(bytes)
        Catch ex As Exception
            ErrorLogService.Log("CriticalDataJournalService.CaptureSnapshot", ex, "Path=" & filePath)
        End Try

        Return snapshot
    End Function

    Public Shared Sub LogCsvChange(filePath As String,
                                   operation As String,
                                   beforeState As CsvFileSnapshot,
                                   afterState As CsvFileSnapshot,
                                   Optional note As String = "")
        Try
            If String.IsNullOrWhiteSpace(filePath) OrElse IsJournalFile(filePath) Then Return

            beforeState = If(beforeState, New CsvFileSnapshot())
            afterState = If(afterState, New CsvFileSnapshot())

            Dim beforeHash = If(beforeState.Hash, "")
            Dim afterHash = If(afterState.Hash, "")
            If beforeState.Exists = afterState.Exists AndAlso
               beforeState.Length = afterState.Length AndAlso
               beforeState.RowCount = afterState.RowCount AndAlso
               String.Equals(beforeHash, afterHash, StringComparison.OrdinalIgnoreCase) Then
                Return
            End If

            EnsureJournalFile()

            Dim entry As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
                {"EntryId", DateTime.UtcNow.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture) & "-" & Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant()},
                {"EventAt", DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss", CultureInfo.CurrentCulture)},
                {"FileName", Path.GetFileName(filePath)},
                {"FilePath", filePath},
                {"Operation", If(operation, "")},
                {"BeforeRows", beforeState.RowCount.ToString(CultureInfo.InvariantCulture)},
                {"AfterRows", afterState.RowCount.ToString(CultureInfo.InvariantCulture)},
                {"RowDelta", (afterState.RowCount - beforeState.RowCount).ToString(CultureInfo.InvariantCulture)},
                {"BeforeBytes", beforeState.Length.ToString(CultureInfo.InvariantCulture)},
                {"AfterBytes", afterState.Length.ToString(CultureInfo.InvariantCulture)},
                {"ByteDelta", (afterState.Length - beforeState.Length).ToString(CultureInfo.InvariantCulture)},
                {"BeforeHash", beforeHash},
                {"AfterHash", afterHash},
                {"UserName", ResolveAppStateValue({"CurrentUsername", "CurrentUserName", "CurrentUser", "UserName", "Username"})},
                {"Role", ResolveAppStateValue({"CurrentRole", "Role", "CurrentUserRole"})},
                {"WindowsUser", Environment.UserName},
                {"ComputerName", Environment.MachineName},
                {"ProcessId", Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture)},
                {"Version", ResolveVersion()},
                {"Note", If(note, "")}
            }

            Dim values As New List(Of String)()
            For Each header In Headers
                values.Add(If(entry.ContainsKey(header), entry(header), ""))
            Next

            Using AcquireJournalLock()
                AppendLineShared(AppPaths.CriticalDataJournalCsv, CsvUtil.ToCsvLine(values))
            End Using
        Catch ex As Exception
            ErrorLogService.Log("CriticalDataJournalService.LogCsvChange", ex, "Path=" & filePath)
        End Try
    End Sub

    Public Shared Function GetRecentEntries(Optional take As Integer = 1000) As List(Of Dictionary(Of String, String))
        Dim result As New List(Of Dictionary(Of String, String))()
        If take <= 0 Then Return result

        Try
            EnsureJournalFile()
            Dim journalPath = AppPaths.CriticalDataJournalCsv
            If Not File.Exists(journalPath) Then Return result

            Dim text = ReadAllTextShared(journalPath)
            If String.IsNullOrWhiteSpace(text) Then Return result

            Dim lines = text.Replace(vbCrLf, vbLf).Replace(vbCr, vbLf).
                Split(New String() {vbLf}, StringSplitOptions.None).
                Where(Function(line) Not String.IsNullOrWhiteSpace(line)).
                ToList()
            If lines.Count <= 1 Then Return result

            Dim headers = CsvUtil.ParseLine(lines(0))
            For i As Integer = lines.Count - 1 To 1 Step -1
                Dim values = CsvUtil.ParseLine(lines(i))
                Dim row As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
                For c As Integer = 0 To headers.Count - 1
                    row(headers(c)) = If(c < values.Count, values(c), "")
                Next
                result.Add(row)
                If result.Count >= take Then Exit For
            Next
        Catch ex As Exception
            ErrorLogService.Log("CriticalDataJournalService.GetRecentEntries", ex)
        End Try

        Return result
    End Function

    Public Shared Sub EnsureJournalFile()
        Try
            Dim journalPath = AppPaths.CriticalDataJournalCsv
            Dim dirName = Path.GetDirectoryName(journalPath)
            If Not String.IsNullOrWhiteSpace(dirName) Then Directory.CreateDirectory(dirName)

            Dim needsCreate As Boolean = Not File.Exists(journalPath) OrElse New FileInfo(journalPath).Length = 0
            If Not needsCreate Then
                Dim firstLine = ReadFirstLineShared(journalPath)
                needsCreate = Not String.Equals(firstLine, CsvUtil.ToCsvLine(Headers), StringComparison.Ordinal)
            End If

            If Not needsCreate Then Return

            Using AcquireJournalLock()
                Dim stillNeedsCreate As Boolean = Not File.Exists(journalPath) OrElse New FileInfo(journalPath).Length = 0
                If Not stillNeedsCreate Then
                    Dim firstLineAfterLock = ReadFirstLineShared(journalPath)
                    stillNeedsCreate = Not String.Equals(firstLineAfterLock, CsvUtil.ToCsvLine(Headers), StringComparison.Ordinal)
                End If
                If Not stillNeedsCreate Then Return

                If File.Exists(journalPath) AndAlso New FileInfo(journalPath).Length > 0 Then
                    Dim damagedPath = journalPath & ".damaged_" & DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)
                    Try
                        File.Move(journalPath, damagedPath)
                    Catch ex As Exception
                        ErrorLogService.Log("CriticalDataJournalService.EnsureJournalFile.MoveDamaged", ex, "Path=" & journalPath)
                    End Try
                End If

                Using fs As New FileStream(journalPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite Or FileShare.Delete)
                    Dim headerBytes = New UTF8Encoding(True).GetBytes(CsvUtil.ToCsvLine(Headers) & Environment.NewLine)
                    fs.Write(headerBytes, 0, headerBytes.Length)
                    fs.Flush(True)
                End Using
            End Using
        Catch ex As Exception
            ErrorLogService.Log("CriticalDataJournalService.EnsureJournalFile", ex)
        End Try
    End Sub

    Private Shared Function AcquireJournalLock() As IDisposable
        Dim journalPath = AppPaths.CriticalDataJournalCsv
        Dim lockPath = journalPath & ".lock"
        Dim dirName = Path.GetDirectoryName(journalPath)
        If Not String.IsNullOrWhiteSpace(dirName) Then Directory.CreateDirectory(dirName)

        For attempt As Integer = 1 To LockRetryCount
            Try
                Dim stream = New FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)
                Return New JournalLockHandle(stream, lockPath)
            Catch ex As IOException
                If attempt = LockRetryCount Then Throw
                Threading.Thread.Sleep(LockRetryDelayMs)
            Catch ex As UnauthorizedAccessException
                If attempt = LockRetryCount Then Throw
                Threading.Thread.Sleep(LockRetryDelayMs)
            End Try
        Next

        Throw New IOException("Kritik veri gunlugu kilidi alinamadi: " & lockPath)
    End Function

    Private Shared Function IsJournalFile(filePath As String) As Boolean
        Try
            Return String.Equals(Path.GetFullPath(filePath), Path.GetFullPath(AppPaths.CriticalDataJournalCsv), StringComparison.OrdinalIgnoreCase)
        Catch
            Return False
        End Try
    End Function

    Private Shared Function ReadAllBytesShared(filePath As String) As Byte()
        Using fs As New FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite Or FileShare.Delete)
            Using ms As New MemoryStream()
                fs.CopyTo(ms)
                Return ms.ToArray()
            End Using
        End Using
    End Function

    Private Shared Function ReadAllTextShared(filePath As String) As String
        Using fs As New FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite Or FileShare.Delete)
            Using sr As New StreamReader(fs, Encoding.UTF8, True)
                Return sr.ReadToEnd()
            End Using
        End Using
    End Function

    Private Shared Function ReadFirstLineShared(filePath As String) As String
        Using fs As New FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite Or FileShare.Delete)
            Using sr As New StreamReader(fs, Encoding.UTF8, True)
                Return If(sr.ReadLine(), "")
            End Using
        End Using
    End Function

    Private Shared Sub AppendLineShared(filePath As String, line As String)
        Dim bytes = Encoding.UTF8.GetBytes(line & Environment.NewLine)
        Using fs As New FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read Or FileShare.Delete)
            fs.Write(bytes, 0, bytes.Length)
            fs.Flush(True)
        End Using
    End Sub

    Private Shared Function CountCsvDataRows(bytes As Byte()) As Integer
        If bytes Is Nothing OrElse bytes.Length = 0 Then Return 0
        Dim text = Encoding.UTF8.GetString(bytes)
        If String.IsNullOrWhiteSpace(text) Then Return 0

        Dim recordCount As Integer = 0
        Dim hasRecordText As Boolean = False
        Dim inQuotes As Boolean = False

        Dim i As Integer = 0
        While i < text.Length
            Dim ch = text(i)

            If ch = """"c Then
                If inQuotes AndAlso i + 1 < text.Length AndAlso text(i + 1) = """"c Then
                    i += 1
                    hasRecordText = True
                Else
                    inQuotes = Not inQuotes
                    hasRecordText = True
                End If
            ElseIf (ch = ControlChars.Cr OrElse ch = ControlChars.Lf) AndAlso Not inQuotes Then
                If hasRecordText Then
                    recordCount += 1
                    hasRecordText = False
                End If

                If ch = ControlChars.Cr AndAlso i + 1 < text.Length AndAlso text(i + 1) = ControlChars.Lf Then
                    i += 1
                End If
            ElseIf Not Char.IsWhiteSpace(ch) Then
                hasRecordText = True
            End If

            i += 1
        End While

        If hasRecordText Then recordCount += 1
        Return Math.Max(0, recordCount - 1)
    End Function

    Private Shared Function ComputeHash(bytes As Byte()) As String
        If bytes Is Nothing OrElse bytes.Length = 0 Then Return ""
        Using sha = SHA256.Create()
            Dim hash = sha.ComputeHash(bytes)
            Return BitConverter.ToString(hash).Replace("-", "")
        End Using
    End Function

    Private Shared Function ResolveAppStateValue(names As IEnumerable(Of String)) As String
        For Each name In names
            Dim value = TryGetAppStateMember(name)
            If Not String.IsNullOrWhiteSpace(value) Then Return value
        Next
        Return ""
    End Function

    Private Shared Function TryGetAppStateMember(name As String) As String
        Try
            Dim flags = BindingFlags.Public Or BindingFlags.Static Or BindingFlags.IgnoreCase
            Dim t = GetType(AppState)
            Dim prop = t.GetProperty(name, flags)
            If prop IsNot Nothing Then
                Dim value = prop.GetValue(Nothing)
                If value IsNot Nothing Then Return Convert.ToString(value, CultureInfo.CurrentCulture)
            End If

            Dim field = t.GetField(name, flags)
            If field IsNot Nothing Then
                Dim value = field.GetValue(Nothing)
                If value IsNot Nothing Then Return Convert.ToString(value, CultureInfo.CurrentCulture)
            End If
        Catch
        End Try

        Return ""
    End Function

    Private Shared Function ResolveVersion() As String
        Dim fromAppState = ResolveAppStateValue({"CurrentVersion", "Version", "ProgramVersion"})
        If Not String.IsNullOrWhiteSpace(fromAppState) Then Return fromAppState

        Try
            Dim asm = Assembly.GetExecutingAssembly()
            Dim info = FileVersionInfo.GetVersionInfo(asm.Location)
            Return If(info.ProductVersion, "")
        Catch
            Return ""
        End Try
    End Function

    Private NotInheritable Class JournalLockHandle
        Implements IDisposable

        Private ReadOnly stream As FileStream
        Private ReadOnly lockPath As String
        Private disposed As Boolean

        Public Sub New(stream As FileStream, lockPath As String)
            Me.stream = stream
            Me.lockPath = lockPath
        End Sub

        Public Sub Dispose() Implements IDisposable.Dispose
            If disposed Then Return
            disposed = True

            Try
                stream.Dispose()
            Catch
            End Try

            Try
                If Not String.IsNullOrWhiteSpace(lockPath) AndAlso File.Exists(lockPath) Then
                    File.Delete(lockPath)
                End If
            Catch
                ' AÄŸ paylaÅŸÄ±mÄ±nda aynÄ± anda baÅŸka bir bilgisayar kilidi aldÄ±ysa
                ' bir sonraki yazma denemesinde tekrar temizlenir.
            End Try
        End Sub
    End Class
End Class

Public Class CsvFileSnapshot
    Public Property Exists As Boolean = False
    Public Property Length As Long = 0
    Public Property RowCount As Integer = 0
    Public Property Hash As String = ""
    Public Property LastWriteUtc As DateTime = DateTime.MinValue
End Class
